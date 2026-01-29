using ModelFarm.Contracts.MarketData;

namespace ModelFarm.Application.Services;

/// <summary>
/// Interface for the market data ingestion service.
/// </summary>
public interface IIngestionService
{
    /// <summary>
    /// Starts an async ingestion operation and returns the operation ID.
    /// </summary>
    Task<Guid> StartIngestionAsync(IngestionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current progress of an ingestion operation.
    /// </summary>
    IngestionProgress GetProgress(Guid operationId);

    /// <summary>
    /// Cancels an ongoing ingestion operation.
    /// </summary>
    void CancelIngestion(Guid operationId);
}
