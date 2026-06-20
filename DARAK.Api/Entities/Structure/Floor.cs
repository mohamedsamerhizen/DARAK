namespace DARAK.Api.Entities;

public sealed class Floor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid BuildingId { get; set; }

    public int FloorNumber { get; set; }

    public string? Name { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Compound Compound { get; set; } = null!;

    public Building Building { get; set; } = null!;

    public ICollection<PropertyUnit> PropertyUnits { get; set; } = new List<PropertyUnit>();
}
