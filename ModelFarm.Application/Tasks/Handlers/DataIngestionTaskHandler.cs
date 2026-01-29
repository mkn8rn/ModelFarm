using System.Diagnostics;
using ModelFarm.Contracts.MarketData;
using ModelFarm.Contracts.Tasks;
using ModelFarm.Infrastructure.MarketData;

namespace ModelFarm.Application.Tasks.Handlers;

/// <summary>
/// Handler for data ingestion tasks.
/// </summary>
public sealed class DataIngestionTaskHandler : IBackgroundTaskHandler
{
    private readonly IMarketDataProviderFactory _providerFactory;

    public DataIngestionTaskHandler(IMarketDataProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public BackgroundTaskType TaskType => BackgroundTaskType.DataIngestion;

    public async Task ExecuteAsync(BackgroundTask task, IProgress<TaskProgressUpdate> progress, CancellationToken cancellationToken)
    {
        var parameters = TaskParameters.FromJson<DataIngestionParameters>(task.ParametersJson);
        
        
        ValidateParameters(parameters);

        var stopwatch = Stopwatch.StartNew();
        var provider = _providerFactory.GetProvider(parameters.Exchange);
        var estimatedTotal = EstimateRecordCount(parameters);
        var recordCount = 0;
        DateTime? firstTimestamp = null;
        DateTime? lastTimestamp = null;

        // Report initial progress
        progress.Report(new TaskProgressUpdate
        {
            ProgressPercent = 0,
            Message = $"Starting... (~{estimatedTotal:N0} records expected)",
            Current = 0,
            Total = estimatedTotal
        });

        var internalProgress = new Progress<int>(count =>
        {
            recordCount = count;
            var percent = estimatedTotal > 0 ? Math.Min(99, (int)(count * 100.0 / estimatedTotal)) : 0;
            progress.Report(new TaskProgressUpdate
            {
                ProgressPercent = percent,
                Message = $"Fetched {count:N0} / ~{estimatedTotal:N0} records...",
                Current = count,
                Total = estimatedTotal
            });
        });

        await foreach (var kline in provider.GetKlinesAsync(
            parameters.Symbol,
            parameters.Interval,
            parameters.StartTimeUtc,
            parameters.EndTimeUtc,
            internalProgress,
            cancellationToken))
        {
            firstTimestamp ??= DateTimeOffset.FromUnixTimeMilliseconds(kline.OpenTime).UtcDateTime;
            lastTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(kline.CloseTime).UtcDateTime;
        }

        stopwatch.Stop();

        var result = new DataIngestionResult
        {
            TotalRecords = recordCount,
            FirstTimestampUtc = firstTimestamp ?? parameters.StartTimeUtc,
            LastTimestampUtc = lastTimestamp ?? parameters.EndTimeUtc,
            Duration = stopwatch.Elapsed
        };

        // Store the result in the task
        task.ResultJson = result.ToJson();
        task.ProgressPercent = 100;
        task.ProgressMessage = $"Completed: {recordCount:N0} records in {stopwatch.Elapsed.TotalSeconds:F1}s";
    }


    private static void ValidateParameters(DataIngestionParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.Symbol))
            throw new ArgumentException("Symbol is required");

        if (parameters.StartTimeUtc >= parameters.EndTimeUtc)
            throw new ArgumentException("Start time must be before end time");

        if (parameters.EndTimeUtc > DateTime.UtcNow)
            throw new ArgumentException("End time cannot be in the future");
    }

    private static int EstimateRecordCount(DataIngestionParameters parameters)
    {
        var duration = parameters.EndTimeUtc - parameters.StartTimeUtc;
        var intervalMinutes = parameters.Interval switch
        {
            KlineInterval.OneMinute => 1,
            KlineInterval.FiveMinutes => 5,
            KlineInterval.FifteenMinutes => 15,
            KlineInterval.OneHour => 60,
            KlineInterval.FourHours => 240,
            KlineInterval.OneDay => 1440,
            _ => 60
        };
        return (int)(duration.TotalMinutes / intervalMinutes);
    }
}
