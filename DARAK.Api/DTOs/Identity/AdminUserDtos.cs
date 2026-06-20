using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Identity;

public sealed record AdminUserResponse(
    Guid Id,
    string Email,
    string FullName,
    bool EmailConfirmed,
    bool IsLockedOut,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<string> Roles);

public sealed class AdminUserSearchQuery : PaginationQuery
{
    [MaxLength(120)]
    public string? SearchTerm { get; init; }

    public UserRole? Role { get; init; }
}

public sealed class AssignUserRoleRequest
{
    [Required]
    public UserRole Role { get; init; }
}
