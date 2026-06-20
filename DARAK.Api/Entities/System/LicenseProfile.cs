using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class LicenseProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string LicensedTo { get; set; } = string.Empty;

    public string LicenseKeyFingerprint { get; set; } = string.Empty;

    public LicensePlan Plan { get; set; } = LicensePlan.Professional;

    public LicenseStatus Status { get; set; } = LicenseStatus.Trial;

    public int MaxCompounds { get; set; }

    public int MaxUnits { get; set; }

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAtUtc { get; set; }

    public string? Notes { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public ApplicationUser? UpdatedByUser { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
