using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class Meter
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public MeterType MeterType { get; set; }

    public string MeterNumber { get; set; } = string.Empty;

    public decimal RatePerUnit { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ICollection<MeterReading> Readings { get; set; } = new List<MeterReading>();
}
