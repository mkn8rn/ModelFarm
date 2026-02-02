using Microsoft.EntityFrameworkCore;
using ModelFarm.Contracts.Resources;

namespace ModelFarm.Infrastructure.Persistence.Entities;

/// <summary>
/// Database entity for resource containers.
/// </summary>
[Index(nameof(Name), nameof(Type), IsUnique = true)]
[Index(nameof(Type), nameof(IsDefault))]
public sealed class ResourceContainerEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required ResourceType Type { get; set; }
    public required long MaxCapacity { get; set; }
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public required DateTime CreatedAtUtc { get; set; }


    public ResourceContainer ToContract() => new()
    {
        Id = Id,
        Name = Name,
        Type = Type,
        MaxCapacity = MaxCapacity,
        Description = Description,
        IsDefault = IsDefault,
        CreatedAtUtc = CreatedAtUtc
    };

    public static ResourceContainerEntity FromContract(ResourceContainer container) => new()
    {
        Id = container.Id,
        Name = container.Name,
        Type = container.Type,
        MaxCapacity = container.MaxCapacity,
        Description = container.Description,
        IsDefault = container.IsDefault,
        CreatedAtUtc = container.CreatedAtUtc
    };
}
