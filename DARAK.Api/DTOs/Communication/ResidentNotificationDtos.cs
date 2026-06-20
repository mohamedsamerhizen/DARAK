using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Communication;

public sealed record ResidentNotificationResponse(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    ResidentNotificationType Type,
    ResidentNotificationSeverity Severity,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt);

public sealed class ResidentNotificationSearchQuery : PaginationQuery
{
    public bool? IsRead { get; init; }

    public ResidentNotificationType? Type { get; init; }

    public ResidentNotificationSeverity? Severity { get; init; }
}

public sealed class CreateResidentNotificationRequest
{
    public Guid UserId { get; init; }

    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Message { get; init; } = string.Empty;

    public ResidentNotificationType Type { get; init; } = ResidentNotificationType.General;

    public ResidentNotificationSeverity Severity { get; init; } = ResidentNotificationSeverity.Info;

    [MaxLength(100)]
    public string? RelatedEntityType { get; init; }

    public Guid? RelatedEntityId { get; init; }
}
