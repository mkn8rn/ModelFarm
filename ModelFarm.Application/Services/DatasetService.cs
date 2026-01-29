using Microsoft.EntityFrameworkCore;
using ModelFarm.Contracts.MarketData;
using ModelFarm.Contracts.Training;
using ModelFarm.Infrastructure.Persistence;
using ModelFarm.Infrastructure.Persistence.Entities;

namespace ModelFarm.Application.Services;

public sealed class DatasetService : IDatasetService
{
    private readonly IDbContextFactory<DataDbContext> _dbFactory;
    private readonly IIngestionService _ingestionService;

    public DatasetService(IDbContextFactory<DataDbContext> dbFactory, IIngestionService ingestionService)
    {
        _dbFactory = dbFactory;
        _ingestionService = ingestionService;
    }

    public async Task<DatasetDefinition> CreateDatasetAsync(CreateDatasetRequest request, CancellationToken cancellationToken = default)
    {
        var datasetId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Start ingestion
        var ingestionRequest = new IngestionRequest
        {
            Exchange = request.Exchange,
            Symbol = request.Symbol,
            Interval = request.Interval,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc
        };

        var operationId = await _ingestionService.StartIngestionAsync(ingestionRequest, cancellationToken);

        var dataset = new DatasetDefinition
        {
            Id = datasetId,
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            Exchange = request.Exchange,
            Symbol = request.Symbol,
            Interval = request.Interval,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc,
            Status = DatasetStatus.Downloading,
            CreatedAtUtc = now,
            IngestionOperationId = operationId
        };

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.Datasets.Add(DatasetEntity.FromDefinition(dataset));
        await db.SaveChangesAsync(cancellationToken);

        return dataset;
    }

    public async Task<DatasetDefinition?> GetDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Datasets.FindAsync([datasetId], cancellationToken);
        return entity?.ToDefinition();
    }

    public async Task<IReadOnlyList<DatasetDefinition>> GetAllDatasetsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.Datasets
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return entities.Select(e => e.ToDefinition()).ToList();
    }

    public async Task<bool> DeleteDatasetAsync(Guid datasetId, bool deleteData = false, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Datasets.FindAsync([datasetId], cancellationToken);
        
        if (entity is null)
            return false;

        if (entity.IngestionOperationId is { } operationId)
        {
            _ingestionService.CancelIngestion(operationId);
        }

        db.Datasets.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<DatasetDefinition> RefreshDatasetStatusAsync(Guid datasetId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Datasets.FindAsync([datasetId], cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Dataset {datasetId} not found");

        if (entity.IngestionOperationId is not { } operationId)
            return entity.ToDefinition();

        var progress = _ingestionService.GetProgress(operationId);

        var newStatus = progress.Status switch
        {
            IngestionStatus.InProgress => DatasetStatus.Downloading,
            IngestionStatus.Completed => DatasetStatus.Ready,
            IngestionStatus.Failed => DatasetStatus.Failed,
            _ => entity.Status
        };

        // Only update if status changed
        if (newStatus != entity.Status || progress.Result?.TotalRecords != entity.RecordCount)
        {
            entity.Status = newStatus;
            entity.RecordCount = progress.Result?.TotalRecords;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return entity.ToDefinition();
    }

    public async Task<IReadOnlyList<Kline>> GetDatasetKlinesAsync(Guid datasetId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        
        var dataset = await db.Datasets.FindAsync([datasetId], cancellationToken)
            ?? throw new KeyNotFoundException($"Dataset {datasetId} not found");

        // Query klines for the dataset's parameters
        var startTimeMs = new DateTimeOffset(dataset.StartTimeUtc).ToUnixTimeMilliseconds();
        var endTimeMs = new DateTimeOffset(dataset.EndTimeUtc).ToUnixTimeMilliseconds();

        var klineEntities = await db.Klines
            .Where(k => k.Exchange == dataset.Exchange
                && k.Symbol == dataset.Symbol
                && k.Interval == dataset.Interval
                && k.OpenTime >= startTimeMs
                && k.OpenTime <= endTimeMs)
            .OrderBy(k => k.OpenTime)
            .ToListAsync(cancellationToken);

        return klineEntities.Select(e => new Kline
        {
            OpenTime = e.OpenTime,
            Open = e.Open,
            High = e.High,
            Low = e.Low,
            Close = e.Close,
            Volume = e.Volume,
            CloseTime = e.CloseTime,
            QuoteAssetVolume = e.QuoteAssetVolume,
            NumberOfTrades = e.NumberOfTrades
        }).ToList();
    }
}
