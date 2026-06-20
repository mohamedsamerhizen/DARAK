using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public Guid? PropertyUnitId { get; set; }

    public ConversationStatus Status { get; set; } = ConversationStatus.PendingAdminReply;

    public ConversationPriority Priority { get; set; } = ConversationPriority.Normal;

    public ConversationTopic Topic { get; set; } = ConversationTopic.General;

    public ConversationIssueType IssueType { get; set; } = ConversationIssueType.GeneralInquiry;

    public ConversationLinkedEntityType LinkedEntityType { get; set; } = ConversationLinkedEntityType.None;

    public Guid? LinkedEntityId { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public Guid? AssignedByUserId { get; set; }

    public DateTime? AssignedAtUtc { get; set; }

    public string? LastAssignmentReason { get; set; }

    public ConversationEscalationLevel EscalationLevel { get; set; } = ConversationEscalationLevel.None;

    public DateTime? EscalatedAtUtc { get; set; }

    public Guid? EscalatedByUserId { get; set; }

    public string? EscalationReason { get; set; }

    public int ReopenCount { get; set; }

    public string? LastReopenReason { get; set; }

    public DateTime? ReopenedAtUtc { get; set; }

    public Guid? ReopenedByResidentId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public DateTime LastMessageAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastResidentMessageAtUtc { get; set; }

    public DateTime? LastAdminMessageAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public PropertyUnit? PropertyUnit { get; set; }

    public ApplicationUser? AssignedToUser { get; set; }

    public ApplicationUser? AssignedByUser { get; set; }

    public ApplicationUser? EscalatedByUser { get; set; }

    public ResidentProfile? ReopenedByResident { get; set; }

    public ICollection<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
}
