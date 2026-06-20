using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.System;

public sealed record SystemVersionResponse(
    string ProductName,
    string ApiVersion,
    string EnvironmentName,
    DateTime ServerTimeUtc,
    string InformationalVersion);

public sealed class SystemSettingSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }

    public bool IncludeSensitiveValues { get; init; }
}

public sealed class UpsertSystemSettingRequest
{
    public Guid? CompoundId { get; init; }

    [Required]
    [MaxLength(150)]
    public string Key { get; init; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Value { get; init; } = string.Empty;

    public SystemSettingValueType ValueType { get; init; } = SystemSettingValueType.String;

    [MaxLength(1000)]
    public string? Description { get; init; }

    public bool IsSensitive { get; init; }

    public bool IsReadOnly { get; init; }
}

public sealed record SystemSettingResponse(
    Guid Id,
    Guid? CompoundId,
    string Key,
    string Value,
    SystemSettingValueType ValueType,
    SystemSettingScope Scope,
    string? Description,
    bool IsSensitive,
    bool IsReadOnly,
    Guid? UpdatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class UpdateLicenseProfileRequest
{
    [Required]
    [MaxLength(200)]
    public string LicensedTo { get; init; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string LicenseKeyFingerprint { get; init; } = string.Empty;

    public LicensePlan Plan { get; init; } = LicensePlan.Professional;

    public LicenseStatus Status { get; init; } = LicenseStatus.Active;

    [Range(1, 10000)]
    public int MaxCompounds { get; init; } = 1;

    [Range(1, 1000000)]
    public int MaxUnits { get; init; } = 100;

    public DateTime? ExpiresAtUtc { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record LicenseProfileResponse(
    Guid Id,
    string LicensedTo,
    string LicenseKeyFingerprint,
    LicensePlan Plan,
    LicenseStatus Status,
    int MaxCompounds,
    int MaxUnits,
    DateTime IssuedAtUtc,
    DateTime? ExpiresAtUtc,
    string? Notes,
    Guid? UpdatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class SetMaintenanceModeRequest
{
    public bool IsEnabled { get; init; }

    [MaxLength(1000)]
    public string? Message { get; init; }
}

public sealed record MaintenanceModeResponse(
    bool IsEnabled,
    string? Message,
    DateTime? UpdatedAtUtc);

public sealed record DeploymentChecklistResponse(
    IReadOnlyCollection<DeploymentChecklistItemResponse> Items,
    int CompletedCount,
    int TotalCount,
    bool IsCommercialReady);

public sealed record DeploymentChecklistItemResponse(
    string Key,
    string Title,
    bool IsCompleted,
    string Recommendation);

public sealed record SystemHealthDashboardResponse(
    SystemHealthStatus Status,
    int PendingNotifications,
    int FailedNotifications,
    int OpenIntegrationFailures,
    int FailedBackgroundJobs24h,
    DateTime CapturedAtUtc,
    string Summary);

public sealed class BackgroundJobRunSearchQuery : PaginationQuery
{
    public string? JobName { get; init; }

    public BackgroundJobRunStatus? Status { get; init; }
}

public sealed class StartBackgroundJobRunRequest
{
    [Required]
    [MaxLength(150)]
    public string JobName { get; init; } = string.Empty;

    [MaxLength(150)]
    public string? WorkerName { get; init; }

    [MaxLength(4000)]
    public string? MetadataJson { get; init; }
}

public sealed class CompleteBackgroundJobRunRequest
{
    public BackgroundJobRunStatus Status { get; init; } = BackgroundJobRunStatus.Succeeded;

    [Range(0, int.MaxValue)]
    public int ProcessedCount { get; init; }

    [Range(0, int.MaxValue)]
    public int FailedCount { get; init; }

    [MaxLength(1000)]
    public string? ErrorMessage { get; init; }
}

public sealed record BackgroundJobRunResponse(
    Guid Id,
    string JobName,
    string? WorkerName,
    BackgroundJobRunStatus Status,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int? DurationMs,
    int ProcessedCount,
    int FailedCount,
    string? ErrorMessage,
    string? MetadataJson);

public sealed class IntegrationFailureSearchQuery : PaginationQuery
{
    public string? IntegrationName { get; init; }

    public IntegrationFailureStatus? Status { get; init; }
}

public sealed class RecordIntegrationFailureRequest
{
    [Required]
    [MaxLength(150)]
    public string IntegrationName { get; init; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string OperationName { get; init; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string ErrorMessage { get; init; } = string.Empty;

    [MaxLength(4000)]
    public string? MetadataJson { get; init; }
}

public sealed class ResolveIntegrationFailureRequest
{
    [MaxLength(1000)]
    public string? ResolutionNote { get; init; }
}

public sealed record IntegrationFailureEventResponse(
    Guid Id,
    string IntegrationName,
    string OperationName,
    IntegrationFailureStatus Status,
    string ErrorMessage,
    int OccurrenceCount,
    DateTime FirstOccurredAtUtc,
    DateTime LastOccurredAtUtc,
    DateTime? ResolvedAtUtc,
    Guid? ResolvedByUserId,
    string? ResolutionNote,
    string? MetadataJson);
