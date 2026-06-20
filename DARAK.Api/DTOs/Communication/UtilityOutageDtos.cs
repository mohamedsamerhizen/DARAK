using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Communication;

public sealed class UtilityOutageQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? BuildingId { get; init; }
    public Guid? FloorId { get; init; }
    public Guid? PropertyUnitId { get; init; }
    public UtilityOutageServiceType? ServiceType { get; init; }
    public UtilityOutageStatus? Status { get; init; }
    public UtilityOutageSeverity? Severity { get; init; }
    public bool ActiveOnly { get; init; }
}

public sealed class CreateUtilityOutageRequest
{
    public Guid CompoundId { get; init; }

    public Guid? BuildingId { get; init; }

    public Guid? FloorId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public UtilityOutageServiceType ServiceType { get; init; }

    public UtilityOutageAffectedScope AffectedScope { get; init; } = UtilityOutageAffectedScope.Compound;

    public UtilityOutageStatus Status { get; init; } = UtilityOutageStatus.Active;

    public UtilityOutageSeverity Severity { get; init; } = UtilityOutageSeverity.Medium;

    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Description { get; init; } = string.Empty;

    public DateTime? EstimatedStartAtUtc { get; init; }

    public DateTime? EstimatedEndAtUtc { get; init; }

    public bool PublishAnnouncement { get; init; } = true;

    public bool NotifyResidents { get; init; } = true;
}

public sealed class PublishUtilityOutageUpdateRequest
{
    public UtilityOutageUpdateType UpdateType { get; init; } = UtilityOutageUpdateType.Information;

    [Required]
    [MaxLength(2000)]
    public string Message { get; init; } = string.Empty;

    public DateTime? NewEstimatedEndAtUtc { get; init; }

    public bool NotifyResidents { get; init; } = true;
}

public sealed class ResolveUtilityOutageRequest
{
    [MaxLength(2000)]
    public string? ResolutionNotes { get; init; }

    public bool NotifyResidents { get; init; } = true;
}

public sealed class CancelUtilityOutageRequest
{
    [MaxLength(2000)]
    public string? Reason { get; init; }

    public bool NotifyResidents { get; init; } = true;
}

public sealed record UtilityOutageResponse(
    Guid Id,
    Guid CompoundId,
    Guid? BuildingId,
    Guid? FloorId,
    Guid? PropertyUnitId,
    Guid? AnnouncementId,
    UtilityOutageServiceType ServiceType,
    UtilityOutageAffectedScope AffectedScope,
    UtilityOutageStatus Status,
    UtilityOutageSeverity Severity,
    string Title,
    string Description,
    DateTime EstimatedStartAtUtc,
    DateTime? EstimatedEndAtUtc,
    DateTime? PublishedAtUtc,
    DateTime? ResolvedAtUtc,
    string? ResolutionNotes,
    bool NotifyResidents,
    int RecipientCount,
    int OutboxItemCount,
    Guid? CreatedByUserId,
    Guid? ResolvedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int UpdateCount);

public sealed record UtilityOutageUpdateResponse(
    Guid Id,
    Guid UtilityOutageId,
    UtilityOutageUpdateType UpdateType,
    string Message,
    DateTime? NewEstimatedEndAtUtc,
    Guid? CreatedByUserId,
    DateTime CreatedAtUtc);

public sealed record UtilityOutageDetailsResponse(
    UtilityOutageResponse Outage,
    IReadOnlyCollection<UtilityOutageUpdateResponse> Updates);

public sealed record ResidentCommunicationOperationsSummaryResponse(
    int ActiveAnnouncementCount,
    int ActiveOutageCount,
    int CriticalOutageCount,
    int PlannedOutageCount,
    int ResolvedOutageCount,
    int UnreadAnnouncementReceiptCount,
    int PendingOutboxItemCount);
