using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class SmartMeterReadingIngestion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid SmartMeterDeviceId { get; set; }

    public Guid MeterId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid? MeterReadingId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public decimal PreviousReading { get; set; }

    public decimal CurrentReading { get; set; }

    public decimal Consumption { get; set; }

    public SmartMeterReadingIngestionStatus Status { get; set; }

    public SmartMeterReadingAnomalyType AnomalyType { get; set; } = SmartMeterReadingAnomalyType.None;

    public bool BillingHoldRecommended { get; set; }

    public string? Message { get; set; }

    public string? ProviderReference { get; set; }

    public string? RawPayload { get; set; }

    public DateTime ReadingTimestampUtc { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Compound Compound { get; set; } = null!;

    public SmartMeterDevice SmartMeterDevice { get; set; } = null!;

    public Meter Meter { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public MeterReading? MeterReading { get; set; }
}
