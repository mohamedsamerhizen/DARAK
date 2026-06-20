using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class CollectionCase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public FinancialCollectionSourceType SourceType { get; set; } = FinancialCollectionSourceType.ManualBalance;

    public Guid? SourceId { get; set; }

    public CollectionStage Stage { get; set; } = CollectionStage.Reminder;

    public CollectionCaseStatus Status { get; set; } = CollectionCaseStatus.Open;

    public decimal AmountDue { get; set; }

    public string Currency { get; set; } = "IQD";

    public DateOnly? DueDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? ClosedByUserId { get; set; }

    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastActionAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public ApplicationUser? AssignedToUser { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public ApplicationUser? ClosedByUser { get; set; }

    public ICollection<LegalNotice> LegalNotices { get; set; } = new List<LegalNotice>();

    public ICollection<PaymentPlan> PaymentPlans { get; set; } = new List<PaymentPlan>();
}
