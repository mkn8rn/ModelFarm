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
    public double ValidationSplit { get; set; } = 0.2;
    public double TestSplit { get; set; } = 0.1;
    public int RandomSeed { get; set; } = 42;

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
        ValidationSplit = ValidationSplit,
        TestSplit = TestSplit,
        RandomSeed = RandomSeed,
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
        ValidationSplit = config.ValidationSplit,
        TestSplit = config.TestSplit,
        RandomSeed = config.RandomSeed,
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
        ValidationSplit = config.ValidationSplit;
        TestSplit = config.TestSplit;
        RandomSeed = config.RandomSeed;
        PerformanceRequirementsJson = JsonSerializer.Serialize(config.PerformanceRequirements);
        TradingEnvironmentJson = JsonSerializer.Serialize(config.TradingEnvironment);
        UpdatedAtUtc = config.UpdatedAtUtc;
    }
}
