namespace DARAK.Api.Entities;

public sealed class Building
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public int NumberOfFloors { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Compound Compound { get; set; } = null!;

    public ICollection<Floor> Floors { get; set; } = new List<Floor>();

    public ICollection<PropertyUnit> PropertyUnits { get; set; } = new List<PropertyUnit>();
}
