using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class OccupancyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ResidentProfileId { get; set; }

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public OccupancyType OccupancyType { get; set; }

    public OccupancyStatus OccupancyStatus { get; set; } = OccupancyStatus.Active;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? ContractNumber { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;
}
