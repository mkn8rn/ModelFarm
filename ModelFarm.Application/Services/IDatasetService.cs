using ModelFarm.Contracts.MarketData;
using ModelFarm.Contracts.Training;

namespace ModelFarm.Application.Services;

/// <summary>
/// Service for managing datasets used in ML model training.
/// </summary>
public interface IDatasetService
{
    /// <summary>
    /// Creates a new dataset definition. If data is not already downloaded,
    /// triggers a background download job.
    /// </summary>
    Task<DatasetDefinition> CreateDatasetAsync(CreateDatasetRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a dataset by ID.
    /// </summary>
    Task<DatasetDefinition?> GetDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all datasets.
    /// </summary>
    Task<IReadOnlyList<DatasetDefinition>> GetAllDatasetsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a dataset and optionally its associated data.
    /// </summary>
    Task<bool> DeleteDatasetAsync(Guid datasetId, bool deleteData = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the dataset status (checks if download is complete, etc.).
    /// </summary>
    Task<DatasetDefinition> RefreshDatasetStatusAsync(Guid datasetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the kline data for a dataset.
    /// </summary>
    Task<IReadOnlyList<Kline>> GetDatasetKlinesAsync(Guid datasetId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to create a new dataset.
/// </summary>
public sealed record CreateDatasetRequest
{
    public required DatasetType Type { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required Contracts.MarketData.Exchange Exchange { get; init; }
    public required string Symbol { get; init; }
    public required Contracts.MarketData.KlineInterval Interval { get; init; }
    public required DateTime StartTimeUtc { get; init; }
    public required DateTime EndTimeUtc { get; init; }
}
