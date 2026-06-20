using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Communication;

public sealed record AnnouncementResponse(
    Guid Id,
    string Title,
    string Body,
    AnnouncementCategory Category,
    AnnouncementPriority Priority,
    AnnouncementAudience Audience,
    AnnouncementStatus Status,
    Guid CompoundId,
    DateTime? PublishedAt,
    DateTime? ExpiresAt,
    Guid? CreatedByUserId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsPinned,
    bool IsActive,
    bool IsRead,
    int ReadCount);

public sealed record AnnouncementReadReceiptResponse(
    Guid Id,
    Guid AnnouncementId,
    Guid UserId,
    DateTime ReadAt);

public sealed class AnnouncementSearchQuery : PaginationQuery
{
    public AnnouncementStatus? Status { get; init; }

    public AnnouncementCategory? Category { get; init; }

    public AnnouncementPriority? Priority { get; init; }

    public AnnouncementAudience? Audience { get; init; }

    public Guid? CompoundId { get; init; }

    public bool? IsPinned { get; init; }

    public bool? IsActive { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateAnnouncementRequest
{
    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Body { get; init; } = string.Empty;

    public AnnouncementCategory Category { get; init; } = AnnouncementCategory.General;

    public AnnouncementPriority Priority { get; init; } = AnnouncementPriority.Normal;

    public AnnouncementAudience Audience { get; init; } = AnnouncementAudience.AllResidents;

    public Guid? CompoundId { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public bool IsPinned { get; init; }
}

public sealed class UpdateAnnouncementRequest
{
    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Body { get; init; } = string.Empty;

    public AnnouncementCategory Category { get; init; } = AnnouncementCategory.General;

    public AnnouncementPriority Priority { get; init; } = AnnouncementPriority.Normal;

    public AnnouncementAudience Audience { get; init; } = AnnouncementAudience.AllResidents;

    public Guid? CompoundId { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public bool IsPinned { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed class PublishAnnouncementRequest
{
    public DateTime? PublishedAt { get; init; }

    public DateTime? ExpiresAt { get; init; }
}
