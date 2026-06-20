namespace DARAK.Api.Entities;

public sealed class Compound
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string City { get; set; } = string.Empty;

    public string Area { get; set; } = string.Empty;

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Building> Buildings { get; set; } = new List<Building>();

    public ICollection<PropertyUnit> PropertyUnits { get; set; } = new List<PropertyUnit>();

    public ICollection<ParkingSpot> ParkingSpots { get; set; } = new List<ParkingSpot>();

}
