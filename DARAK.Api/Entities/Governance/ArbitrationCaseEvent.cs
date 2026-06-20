using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ArbitrationCaseEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ArbitrationCaseId { get; set; }

    public ArbitrationCaseEventType EventType { get; set; } = ArbitrationCaseEventType.NoteAdded;

    public string Message { get; set; } = string.Empty;

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ArbitrationCase ArbitrationCase { get; set; } = null!;

    public ApplicationUser? CreatedByUser { get; set; }
}
