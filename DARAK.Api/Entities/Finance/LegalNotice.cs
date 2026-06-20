using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class LegalNotice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public Guid? CollectionCaseId { get; set; }

    public LegalNoticeType NoticeType { get; set; }

    public LegalNoticeStatus Status { get; set; } = LegalNoticeStatus.Draft;

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string? DeliveryChannel { get; set; }

    public string? DeliveryReference { get; set; }

    public DateOnly? DeadlineDate { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? IssuedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? IssuedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public CollectionCase? CollectionCase { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public ApplicationUser? IssuedByUser { get; set; }
}
