using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class OwnershipTransferRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid CurrentOwnerResidentProfileId { get; set; }

    public Guid NewOwnerResidentProfileId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public OwnershipTransferStatus Status { get; set; } = OwnershipTransferStatus.PendingApproval;

    public DateOnly RequestedTransferDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? DecisionReason { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ResidentProfile CurrentOwnerResidentProfile { get; set; } = null!;

    public ResidentProfile NewOwnerResidentProfile { get; set; } = null!;
}
