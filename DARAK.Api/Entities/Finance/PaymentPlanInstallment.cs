using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class PaymentPlanInstallment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentPlanId { get; set; }

    public int InstallmentNumber { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal Amount { get; set; }

    public decimal PaidAmount { get; set; }

    public PaymentPlanInstallmentStatus Status { get; set; } = PaymentPlanInstallmentStatus.Pending;

    public DateTime? PaidAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public PaymentPlan PaymentPlan { get; set; } = null!;
}
