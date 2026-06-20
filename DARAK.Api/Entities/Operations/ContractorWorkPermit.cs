using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class ContractorWorkPermit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Compound Compound { get; set; } = null!;

    public Guid VendorId { get; set; }

    public ServiceVendor Vendor { get; set; } = null!;

    public Guid? RelatedWorkOrderId { get; set; }

    public WorkOrder? RelatedWorkOrder { get; set; }

    public string Purpose { get; set; } = string.Empty;

    public string WorkArea { get; set; } = string.Empty;

    public string? EquipmentList { get; set; }

    public ContractorWorkPermitRiskLevel RiskLevel { get; set; } = ContractorWorkPermitRiskLevel.Low;

    public ContractorWorkPermitStatus Status { get; set; } = ContractorWorkPermitStatus.PendingApproval;

    public DateTime AllowedFromUtc { get; set; }

    public DateTime AllowedUntilUtc { get; set; }

    public bool RequiresEscort { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public Guid? DeniedByUserId { get; set; }

    public DateTime? DeniedAtUtc { get; set; }

    public string? DenialReason { get; set; }

    public Guid? GuardCheckedInByUserId { get; set; }

    public DateTime? CheckedInAtUtc { get; set; }

    public Guid? GuardCheckedOutByUserId { get; set; }

    public DateTime? CheckedOutAtUtc { get; set; }

    public string? GuardNotes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
