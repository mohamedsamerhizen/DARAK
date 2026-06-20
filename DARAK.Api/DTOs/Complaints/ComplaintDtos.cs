using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Complaints;

public sealed record ComplaintResponse(
    Guid Id,
    Guid ResidentProfileId,
    string ResidentName,
    Guid CompoundId,
    string CompoundName,
    Guid? PropertyUnitId,
    string? UnitNumber,
    string Title,
    string Description,
    ComplaintStatus Status,
    string? AdminResponse,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ResolvedAt,
    DateTime? RejectedAt);

public sealed class ComplaintSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public ComplaintStatus? Status { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateComplaintRequest
{
    public Guid? PropertyUnitId { get; init; }

    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; init; } = string.Empty;
}

public sealed class ComplaintAdminResponseRequest
{
    [Required]
    [MaxLength(2000)]
    public string AdminResponse { get; init; } = string.Empty;
}

public sealed class ConvertComplaintToViolationRequest
{
    public ViolationType ViolationType { get; init; }

    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; init; } = string.Empty;
}
