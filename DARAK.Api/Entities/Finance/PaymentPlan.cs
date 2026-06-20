using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class PaymentPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public Guid CollectionCaseId { get; set; }

    public PaymentPlanStatus Status { get; set; } = PaymentPlanStatus.Active;

    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = "IQD";

    public int InstallmentCount { get; set; }

    public DateOnly StartDate { get; set; }

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public CollectionCase CollectionCase { get; set; } = null!;

    public ApplicationUser? CreatedByUser { get; set; }

    public ICollection<PaymentPlanInstallment> Installments { get; set; } = new List<PaymentPlanInstallment>();
}
