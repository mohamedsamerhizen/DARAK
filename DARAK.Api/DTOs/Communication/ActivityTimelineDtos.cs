using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;
using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Communication;

public sealed record RecordActivityEventRequest(
    Guid CompoundId,
    Guid? ResidentProfileId,
    Guid? PropertyUnitId,
    Guid? ActorUserId,
    ActivityEventType EventType,
    string Title,
    string Description,
    ActivityEntityType EntityType,
    Guid? EntityId,
    string? MetadataJson = null);

public sealed record ActivityEventResponse(
    Guid Id,
    Guid CompoundId,
    Guid? ResidentProfileId,
    Guid? PropertyUnitId,
    Guid? ActorUserId,
    ActivityEventType EventType,
    string Title,
    string Description,
    ActivityEntityType EntityType,
    Guid? EntityId,
    DateTime CreatedAtUtc,
    string? MetadataJson);


public sealed class ActivityTimelineQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public ActivityEventType? EventType { get; init; }

    public ActivityEntityType? EntityType { get; init; }

    public Guid? EntityId { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}
