using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Violations;

public sealed record ViolationResponse(
    Guid Id,
    Guid CompoundId,
    string CompoundName,
    Guid? ResidentProfileId,
    string? ResidentName,
    Guid? PropertyUnitId,
    string? UnitNumber,
    Guid? ComplaintId,
    ViolationType ViolationType,
    string Title,
    string Description,
    Guid? CreatedByUserId,
    string? CreatedByUserName,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record ViolationFineResponse(
    Guid Id,
    Guid ViolationId,
    Guid CompoundId,
    string CompoundName,
    Guid? ResidentProfileId,
    string? ResidentName,
    decimal Amount,
    decimal PaidAmount,
    decimal RemainingAmount,
    ViolationFineStatus Status,
    string Reason,
    DateOnly DueDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? CancelledAt,
    string? CancellationReason);

public sealed class ViolationSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public Guid? ComplaintId { get; init; }

    public ViolationType? ViolationType { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class ViolationFineSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? ViolationId { get; init; }

    public ViolationFineStatus? Status { get; init; }

    public DateOnly? DueBefore { get; init; }

    public DateOnly? DueAfter { get; init; }
}

public sealed class CreateViolationRequest
{
    public Guid CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public ViolationType ViolationType { get; init; }

    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; init; } = string.Empty;
}

public sealed class CreateViolationFineRequest
{
    public Guid ViolationId { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; init; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;

    public DateOnly DueDate { get; init; }
}

public sealed class CancelViolationFineRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}
