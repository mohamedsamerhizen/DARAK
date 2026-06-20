using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Audit;

public sealed class AuditSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? ActorUserId { get; init; }

    public AuditActionType? ActionType { get; init; }

    public AuditEntityType? EntityType { get; init; }

    public Guid? EntityId { get; init; }

    public AuditSeverity? Severity { get; init; }

    [MaxLength(100)]
    public string? SourceModule { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class AuditDashboardQuery
{
    public Guid? CompoundId { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }
}

public sealed class AuditEntityTrailQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }
}

public sealed record AuditLogResponse(
    Guid Id,
    Guid? CompoundId,
    Guid? ResidentProfileId,
    Guid? ActorUserId,
    string ActorRole,
    AuditActionType ActionType,
    AuditEntityType EntityType,
    Guid? EntityId,
    AuditSeverity Severity,
    string SourceModule,
    string Description,
    string? Reason,
    string? CorrelationId,
    bool IsSystemGenerated,
    DateTime CreatedAtUtc);

public sealed record AuditLogDetailsResponse(
    Guid Id,
    Guid? CompoundId,
    Guid? ResidentProfileId,
    Guid? ActorUserId,
    string ActorRole,
    AuditActionType ActionType,
    AuditEntityType EntityType,
    Guid? EntityId,
    AuditSeverity Severity,
    string SourceModule,
    string Description,
    string? Reason,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId,
    string? BeforeValuesJson,
    string? AfterValuesJson,
    string? MetadataJson,
    bool IsSystemGenerated,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<AuditLogChangeResponse> Changes);

public sealed record AuditLogChangeResponse(
    Guid Id,
    string PropertyName,
    string? OldValue,
    string? NewValue,
    bool IsSensitive,
    DateTime CreatedAtUtc);

public sealed record AuditDashboardResponse(
    Guid? CompoundId,
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalCount,
    int CriticalCount,
    int HighCount,
    int MediumCount,
    int LowCount,
    DateTime? LatestCriticalAtUtc,
    IReadOnlyCollection<AuditCountByActionResponse> ByAction,
    IReadOnlyCollection<AuditCountByEntityResponse> ByEntity,
    IReadOnlyCollection<AuditCountBySourceModuleResponse> BySourceModule);

public sealed record AuditCountByActionResponse(
    AuditActionType ActionType,
    int Count);

public sealed record AuditCountByEntityResponse(
    AuditEntityType EntityType,
    int Count);

public sealed record AuditCountBySourceModuleResponse(
    string SourceModule,
    int Count);

public sealed record AuditLogRecord(
    Guid? CompoundId,
    Guid? ResidentProfileId,
    Guid? ActorUserId,
    string? ActorRole,
    AuditActionType ActionType,
    AuditEntityType EntityType,
    Guid? EntityId,
    AuditSeverity Severity,
    string SourceModule,
    string Description,
    string? Reason = null,
    string? BeforeValuesJson = null,
    string? AfterValuesJson = null,
    string? MetadataJson = null,
    string? CorrelationId = null,
    bool IsSystemGenerated = false,
    IReadOnlyCollection<AuditLogChangeRecord>? Changes = null);

public sealed record AuditLogChangeRecord(
    string PropertyName,
    string? OldValue,
    string? NewValue,
    bool IsSensitive = false);
