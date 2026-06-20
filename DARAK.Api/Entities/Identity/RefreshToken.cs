using System.ComponentModel.DataAnnotations.Schema;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? CreatedByIp { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedByIp { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public string? RevokedReason { get; set; }

    [NotMapped]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

    [NotMapped]
    public bool IsActive => RevokedAtUtc is null && !IsExpired;
}
