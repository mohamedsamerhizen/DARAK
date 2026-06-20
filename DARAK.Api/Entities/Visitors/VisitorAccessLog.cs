using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class VisitorAccessLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VisitorPassId { get; set; }

    public Guid? GuardUserId { get; set; }

    public VisitorAccessAction Action { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public VisitorPass VisitorPass { get; set; } = null!;

    public ApplicationUser? GuardUser { get; set; }
}
