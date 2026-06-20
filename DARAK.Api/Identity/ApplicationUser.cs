using DARAK.Api.Entities;
using Microsoft.AspNetCore.Identity;

namespace DARAK.Api.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public ICollection<UserCompoundAssignment> CompoundAssignments { get; set; } = new List<UserCompoundAssignment>();
}
