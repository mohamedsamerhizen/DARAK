using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class SmartMeterDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid MeterId { get; set; }

    public string DeviceIdentifier { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string? FirmwareVersion { get; set; }

    public SmartMeterDeviceStatus Status { get; set; } = SmartMeterDeviceStatus.Active;

    public SmartMeterDeviceHealthStatus HealthStatus { get; set; } = SmartMeterDeviceHealthStatus.Unknown;

    public int ExpectedReadIntervalMinutes { get; set; } = 1440;

    public int OfflineAfterMinutes { get; set; } = 2880;

    public decimal SuspiciousConsumptionThreshold { get; set; } = 1000;

    public DateTime? LastSeenAtUtc { get; set; }

    public DateTime? LastReadingAtUtc { get; set; }

    public decimal? LastReadingValue { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public Meter Meter { get; set; } = null!;

    public ICollection<SmartMeterReadingIngestion> Ingestions { get; set; } = new List<SmartMeterReadingIngestion>();
}
