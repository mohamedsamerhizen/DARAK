using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class DocumentAccessLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentFileId { get; set; }

    public Guid UserId { get; set; }

    public DocumentAccessAction Action { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DocumentFile DocumentFile { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
