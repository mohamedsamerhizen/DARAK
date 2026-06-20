using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class MeterReadingCorrection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid MeterReadingId { get; set; }

    public Guid MeterId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public MeterReadingCorrectionStatus Status { get; set; } = MeterReadingCorrectionStatus.PendingReview;

    public decimal OriginalPreviousReading { get; set; }

    public decimal OriginalCurrentReading { get; set; }

    public decimal OriginalConsumption { get; set; }

    public decimal OriginalAmount { get; set; }

    public decimal CorrectedPreviousReading { get; set; }

    public decimal CorrectedCurrentReading { get; set; }

    public decimal CorrectedConsumption { get; set; }

    public decimal CorrectedAmount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? DecisionReason { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAtUtc { get; set; }

    public DateTime? AppliedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public MeterReading MeterReading { get; set; } = null!;

    public Meter Meter { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;
}
