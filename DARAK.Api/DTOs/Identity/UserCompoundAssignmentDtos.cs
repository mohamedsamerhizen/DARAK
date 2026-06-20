using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Identity;

public sealed record UserCompoundAssignmentResponse(
    Guid Id,
    Guid UserId,
    string? UserEmail,
    string UserFullName,
    Guid CompoundId,
    string CompoundName,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    Guid? CreatedByUserId);

public sealed class UserCompoundAssignmentSearchQuery : PaginationQuery
{
    public Guid? UserId { get; init; }

    public Guid? CompoundId { get; init; }

    public UserRole? Role { get; init; }

    public bool? IsActive { get; init; }
}

public sealed class CreateUserCompoundAssignmentRequest
{
    public Guid UserId { get; init; }

    public Guid CompoundId { get; init; }

    [Required]
    public UserRole Role { get; init; }
}

public sealed class UpdateUserCompoundAssignmentRequest
{
    public bool IsActive { get; init; }
}
