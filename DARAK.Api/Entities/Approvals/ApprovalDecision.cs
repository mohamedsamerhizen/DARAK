using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ApprovalDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ApprovalRequestId { get; set; }

    public Guid DecidedByUserId { get; set; }

    public ApprovalDecisionType DecisionType { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ApprovalRequest ApprovalRequest { get; set; } = null!;

    public ApplicationUser DecidedByUser { get; set; } = null!;
}
