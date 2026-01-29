using Microsoft.EntityFrameworkCore;
using ModelFarm.Contracts.MarketData;
using ModelFarm.Contracts.Training;

namespace ModelFarm.Infrastructure.Persistence.Entities;

[Index(nameof(Name), IsUnique = true)]
public sealed class DatasetEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    
    public required DatasetType Type { get; set; }
    public required Exchange Exchange { get; set; }
    public required string Symbol { get; set; }
    public required KlineInterval Interval { get; set; }
    
    public required DateTime StartTimeUtc { get; set; }
    public required DateTime EndTimeUtc { get; set; }
    
    public required DatasetStatus Status { get; set; }
    public int? RecordCount { get; set; }
    
    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    
    public Guid? IngestionOperationId { get; set; }

    public DatasetDefinition ToDefinition() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        Type = Type,
        Exchange = Exchange,
        Symbol = Symbol,
        Interval = Interval,
        StartTimeUtc = StartTimeUtc,
        EndTimeUtc = EndTimeUtc,
        Status = Status,
        RecordCount = RecordCount,
        CreatedAtUtc = CreatedAtUtc,
        UpdatedAtUtc = UpdatedAtUtc,
        IngestionOperationId = IngestionOperationId
    };

    public static DatasetEntity FromDefinition(DatasetDefinition def) => new()
    {
        Id = def.Id,
        Name = def.Name,
        Description = def.Description,
        Type = def.Type,
        Exchange = def.Exchange,
        Symbol = def.Symbol,
        Interval = def.Interval,
        StartTimeUtc = def.StartTimeUtc,
        EndTimeUtc = def.EndTimeUtc,
        Status = def.Status,
        RecordCount = def.RecordCount,
        CreatedAtUtc = def.CreatedAtUtc,
        UpdatedAtUtc = def.UpdatedAtUtc,
        IngestionOperationId = def.IngestionOperationId
    };
}
