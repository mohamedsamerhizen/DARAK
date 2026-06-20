using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ResidentRiskFlagAction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ResidentRiskFlagId { get; set; }

    public Guid ActorUserId { get; set; }

    public ResidentRiskFlagActionType ActionType { get; set; }

    public ResidentRiskFlagStatus? PreviousStatus { get; set; }

    public ResidentRiskFlagStatus? NewStatus { get; set; }

    public ResidentRiskFlagSeverity? PreviousSeverity { get; set; }

    public ResidentRiskFlagSeverity? NewSeverity { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ResidentRiskFlag ResidentRiskFlag { get; set; } = null!;

    public ApplicationUser ActorUser { get; set; } = null!;
}
