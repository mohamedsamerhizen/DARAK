using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class InstallmentRescheduleRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid InstallmentScheduleItemId { get; set; }

    public Guid PropertySaleContractId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public InstallmentRescheduleStatus Status { get; set; } = InstallmentRescheduleStatus.PendingApproval;

    public DateOnly OriginalDueDate { get; set; }

    public DateOnly RequestedDueDate { get; set; }

    public decimal OriginalAmount { get; set; }

    public decimal? RequestedAmount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? DecisionReason { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAtUtc { get; set; }

    public DateTime? AppliedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public InstallmentScheduleItem InstallmentScheduleItem { get; set; } = null!;

    public PropertySaleContract PropertySaleContract { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;
}
