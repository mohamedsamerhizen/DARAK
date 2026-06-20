namespace DARAK.Api.Entities;

public sealed class ParkingSpot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public string SpotNumber { get; set; } = string.Empty;

    public bool IsCovered { get; set; }

    public bool IsReserved { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Compound Compound { get; set; } = null!;
}
