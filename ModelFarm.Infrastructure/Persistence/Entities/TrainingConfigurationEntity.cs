using Microsoft.EntityFrameworkCore;
using ModelFarm.Contracts.Training;
using System.Text.Json;

namespace ModelFarm.Infrastructure.Persistence.Entities;

[Index(nameof(Name), IsUnique = true)]
public sealed class TrainingConfigurationEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required ConfigurationType Type { get; set; }

    // Dataset reference
    public required Guid DatasetId { get; set; }

    // Model Architecture Parameters
    public required ModelType ModelType { get; set; }
    public int MaxLags { get; set; } = 4;
    public int ForecastHorizon { get; set; } = 1;
    public required string HiddenLayerSizesJson { get; set; } // Stored as JSON array
    public double DropoutRate { get; set; } = 0.2;

    // Training Hyperparameters
    public double LearningRate { get; set; } = 0.001;
    public int BatchSize { get; set; } = 32;
    public int MaxEpochs { get; set; } = 10000;
    public int EarlyStoppingPatience { get; set; } = 50;
    public bool UseEarlyStopping { get; set; } = true;
    public double ValidationSplit { get; set; } = 0.2;
    public double TestSplit { get; set; } = 0.1;
    public int RandomSeed { get; set; } = 42;

    // Checkpoint Settings
    public bool SaveCheckpoints { get; set; } = true;
    public int CheckpointIntervalEpochs { get; set; } = 50;

    // Retry Settings
    public bool RetryUntilSuccess { get; set; } = false;
    public int MaxRetryAttempts { get; set; } = 10;
    public bool ShuffleOnRetry { get; set; } = false;
    public bool ScaleLearningRateOnRetry { get; set; } = false;
    public double LearningRateRetryScale { get; set; } = 0.5;

    // Performance Requirements (stored as JSON)
    public required string PerformanceRequirementsJson { get; set; }

    // Trading Environment (stored as JSON)
    public required string TradingEnvironmentJson { get; set; }

    // Metadata
    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }


    public TrainingConfiguration ToConfiguration() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        Type = Type,
        DatasetId = DatasetId,
        ModelType = ModelType,
        MaxLags = MaxLags,
        ForecastHorizon = ForecastHorizon,
        HiddenLayerSizes = JsonSerializer.Deserialize<int[]>(HiddenLayerSizesJson) ?? [64, 32],
        DropoutRate = DropoutRate,
        LearningRate = LearningRate,
        BatchSize = BatchSize,
        MaxEpochs = MaxEpochs,
        EarlyStoppingPatience = EarlyStoppingPatience,
        UseEarlyStopping = UseEarlyStopping,
        ValidationSplit = ValidationSplit,
        TestSplit = TestSplit,
        RandomSeed = RandomSeed,
        SaveCheckpoints = SaveCheckpoints,
        CheckpointIntervalEpochs = CheckpointIntervalEpochs,
        RetryUntilSuccess = RetryUntilSuccess,
        MaxRetryAttempts = MaxRetryAttempts,
        ShuffleOnRetry = ShuffleOnRetry,
        ScaleLearningRateOnRetry = ScaleLearningRateOnRetry,
        LearningRateRetryScale = LearningRateRetryScale,
        PerformanceRequirements = JsonSerializer.Deserialize<PerformanceRequirements>(PerformanceRequirementsJson)!,
        TradingEnvironment = JsonSerializer.Deserialize<TradingEnvironmentConfig>(TradingEnvironmentJson)!,
        CreatedAtUtc = CreatedAtUtc,
        UpdatedAtUtc = UpdatedAtUtc
    };

    public static TrainingConfigurationEntity FromConfiguration(TrainingConfiguration config) => new()
    {
        Id = config.Id,
        Name = config.Name,
        Description = config.Description,
        Type = config.Type,
        DatasetId = config.DatasetId,
        ModelType = config.ModelType,
        MaxLags = config.MaxLags,
        ForecastHorizon = config.ForecastHorizon,
        HiddenLayerSizesJson = JsonSerializer.Serialize(config.HiddenLayerSizes),
        DropoutRate = config.DropoutRate,
        LearningRate = config.LearningRate,
        BatchSize = config.BatchSize,
        MaxEpochs = config.MaxEpochs,
        EarlyStoppingPatience = config.EarlyStoppingPatience,
        UseEarlyStopping = config.UseEarlyStopping,
        ValidationSplit = config.ValidationSplit,
        TestSplit = config.TestSplit,
        RandomSeed = config.RandomSeed,
        SaveCheckpoints = config.SaveCheckpoints,
        CheckpointIntervalEpochs = config.CheckpointIntervalEpochs,
        RetryUntilSuccess = config.RetryUntilSuccess,
        MaxRetryAttempts = config.MaxRetryAttempts,
        ShuffleOnRetry = config.ShuffleOnRetry,
        ScaleLearningRateOnRetry = config.ScaleLearningRateOnRetry,
        LearningRateRetryScale = config.LearningRateRetryScale,
        PerformanceRequirementsJson = JsonSerializer.Serialize(config.PerformanceRequirements),
        TradingEnvironmentJson = JsonSerializer.Serialize(config.TradingEnvironment),
        CreatedAtUtc = config.CreatedAtUtc,
        UpdatedAtUtc = config.UpdatedAtUtc
    };

    public void UpdateFrom(TrainingConfiguration config)
    {
        Name = config.Name;
        Description = config.Description;
        MaxLags = config.MaxLags;
        ForecastHorizon = config.ForecastHorizon;
        HiddenLayerSizesJson = JsonSerializer.Serialize(config.HiddenLayerSizes);
        DropoutRate = config.DropoutRate;
        LearningRate = config.LearningRate;
        BatchSize = config.BatchSize;
        MaxEpochs = config.MaxEpochs;
        EarlyStoppingPatience = config.EarlyStoppingPatience;
        UseEarlyStopping = config.UseEarlyStopping;
        ValidationSplit = config.ValidationSplit;
        TestSplit = config.TestSplit;
        RandomSeed = config.RandomSeed;
        SaveCheckpoints = config.SaveCheckpoints;
        CheckpointIntervalEpochs = config.CheckpointIntervalEpochs;
        RetryUntilSuccess = config.RetryUntilSuccess;
        MaxRetryAttempts = config.MaxRetryAttempts;
        ShuffleOnRetry = config.ShuffleOnRetry;
        ScaleLearningRateOnRetry = config.ScaleLearningRateOnRetry;
        LearningRateRetryScale = config.LearningRateRetryScale;
        PerformanceRequirementsJson = JsonSerializer.Serialize(config.PerformanceRequirements);
        TradingEnvironmentJson = JsonSerializer.Serialize(config.TradingEnvironment);
        UpdatedAtUtc = config.UpdatedAtUtc;
    }
}
