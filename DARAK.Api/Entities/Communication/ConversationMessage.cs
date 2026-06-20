using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationId { get; set; }

    public Guid? SenderUserId { get; set; }

    public ConversationMessageType MessageType { get; set; }

    public ConversationMessageVisibility Visibility { get; set; } = ConversationMessageVisibility.ResidentVisible;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Conversation Conversation { get; set; } = null!;

    public ApplicationUser? SenderUser { get; set; }
}
