using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Meters;

public sealed class SmartMeterDeviceQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? MeterId { get; init; }
    public SmartMeterDeviceStatus? Status { get; init; }
    public SmartMeterDeviceHealthStatus? HealthStatus { get; init; }
    public bool OfflineOnly { get; init; }
}

public sealed class SmartMeterIngestionQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? DeviceId { get; init; }
    public Guid? MeterId { get; init; }
    public SmartMeterReadingIngestionStatus? Status { get; init; }
    public SmartMeterReadingAnomalyType? AnomalyType { get; init; }
    public bool BillingHoldOnly { get; init; }
}

public sealed class RegisterSmartMeterDeviceRequest
{
    public Guid CompoundId { get; init; }

    public Guid MeterId { get; init; }

    [Required]
    [MaxLength(100)]
    public string DeviceIdentifier { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ProviderName { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? FirmwareVersion { get; init; }

    public int ExpectedReadIntervalMinutes { get; init; } = 1440;

    public int OfflineAfterMinutes { get; init; } = 2880;

    public decimal SuspiciousConsumptionThreshold { get; init; } = 1000;
}

public sealed class IngestSmartMeterReadingRequest
{
    public int Year { get; init; }

    public int Month { get; init; }

    public decimal CurrentReading { get; init; }

    public DateTime? ReadingTimestampUtc { get; init; }

    [MaxLength(128)]
    public string? ProviderReference { get; init; }

    [MaxLength(4000)]
    public string? RawPayload { get; init; }
}

public sealed class UpdateSmartMeterDeviceStatusRequest
{
    public SmartMeterDeviceStatus Status { get; init; }

    [MaxLength(500)]
    public string? Notes { get; init; }
}

public sealed record SmartMeterDeviceResponse(
    Guid Id,
    Guid CompoundId,
    Guid MeterId,
    string MeterNumber,
    Guid PropertyUnitId,
    MeterType MeterType,
    string DeviceIdentifier,
    string ProviderName,
    string? FirmwareVersion,
    SmartMeterDeviceStatus Status,
    SmartMeterDeviceHealthStatus HealthStatus,
    int ExpectedReadIntervalMinutes,
    int OfflineAfterMinutes,
    decimal SuspiciousConsumptionThreshold,
    DateTime? LastSeenAtUtc,
    DateTime? LastReadingAtUtc,
    decimal? LastReadingValue,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record SmartMeterReadingIngestionResponse(
    Guid Id,
    Guid CompoundId,
    Guid SmartMeterDeviceId,
    Guid MeterId,
    Guid PropertyUnitId,
    Guid? MeterReadingId,
    int Year,
    int Month,
    decimal PreviousReading,
    decimal CurrentReading,
    decimal Consumption,
    SmartMeterReadingIngestionStatus Status,
    SmartMeterReadingAnomalyType AnomalyType,
    bool BillingHoldRecommended,
    string? Message,
    string? ProviderReference,
    DateTime ReadingTimestampUtc,
    DateTime CreatedAtUtc);

public sealed record SmartMeterOperationsSummaryResponse(
    int DeviceCount,
    int OnlineDeviceCount,
    int OfflineDeviceCount,
    int WarningDeviceCount,
    int SuspendedDeviceCount,
    int AcceptedIngestionCount,
    int SuspiciousIngestionCount,
    int RejectedIngestionCount,
    int BillingHoldRecommendedCount);
