using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ArbitrationCase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid? ResidentProfileId { get; set; }

    public ArbitrationCaseSourceType SourceType { get; set; }

    public Guid SourceId { get; set; }

    public ArbitrationCaseStatus Status { get; set; } = ArbitrationCaseStatus.Open;

    public ArbitrationCasePriority Priority { get; set; } = ArbitrationCasePriority.Normal;

    public string Title { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string? FinalDecision { get; set; }

    public string? FinalDecisionSummary { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? DecidedByUserId { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? DecisionIssuedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public Compound Compound { get; set; } = null!;

    public ResidentProfile? ResidentProfile { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public ApplicationUser? DecidedByUser { get; set; }

    public ApplicationUser? CancelledByUser { get; set; }

    public ICollection<ArbitrationCaseEvent> Events { get; set; } = new List<ArbitrationCaseEvent>();
}
