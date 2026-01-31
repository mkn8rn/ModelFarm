using ModelFarm.Contracts.Testing;
using System.Text.Json;

namespace ModelFarm.Infrastructure.Persistence.Entities;

/// <summary>
/// Entity for persisted model test results.
/// </summary>
public sealed class ModelTestEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    // References
    public required Guid ModelJobId { get; set; }
    public required Guid DatasetId { get; set; }

    // Display info
    public required string ModelName { get; set; }
    public required string ModelType { get; set; }
    public required string DatasetName { get; set; }
    public required string Symbol { get; set; }

    // Status
    public required ModelTestStatus Status { get; set; }
    public string? ErrorMessage { get; set; }

    // Timestamps
    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    // Results (stored as JSON)
    public string? ResultJson { get; set; }

    public ModelTest ToModelTest() => new()
    {
        Id = Id,
        Name = Name,
        ModelJobId = ModelJobId,
        DatasetId = DatasetId,
        ModelName = ModelName,
        ModelType = ModelType,
        DatasetName = DatasetName,
        Symbol = Symbol,
        Status = Status,
        ErrorMessage = ErrorMessage,
        CreatedAtUtc = CreatedAtUtc,
        CompletedAtUtc = CompletedAtUtc,
        Result = ResultJson != null ? JsonSerializer.Deserialize<ModelTestResult>(ResultJson) : null
    };

    public static ModelTestEntity FromModelTest(ModelTest test) => new()
    {
        Id = test.Id,
        Name = test.Name,
        ModelJobId = test.ModelJobId,
        DatasetId = test.DatasetId,
        ModelName = test.ModelName,
        ModelType = test.ModelType,
        DatasetName = test.DatasetName,
        Symbol = test.Symbol,
        Status = test.Status,
        ErrorMessage = test.ErrorMessage,
        CreatedAtUtc = test.CreatedAtUtc,
        CompletedAtUtc = test.CompletedAtUtc,
        ResultJson = test.Result != null ? JsonSerializer.Serialize(test.Result) : null
    };
}
