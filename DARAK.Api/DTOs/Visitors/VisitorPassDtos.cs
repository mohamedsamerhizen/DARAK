using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Visitors;

public sealed record VisitorPassResponse(
    Guid Id,
    Guid ResidentProfileId,
    string ResidentName,
    Guid CompoundId,
    string CompoundName,
    Guid PropertyUnitId,
    string UnitNumber,
    string VisitorName,
    string VisitorPhoneNumber,
    string VisitReason,
    string AccessCode,
    VisitorPassStatus Status,
    DateTime ValidFrom,
    DateTime ValidUntil,
    DateTime? CheckedInAt,
    DateTime? CheckedOutAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? CancelledAt,
    string? DenialReason);

public sealed record VisitorAccessLogResponse(
    Guid Id,
    Guid VisitorPassId,
    Guid? GuardUserId,
    string? GuardName,
    VisitorAccessAction Action,
    string? Notes,
    DateTime CreatedAt);

public sealed class VisitorPassSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public VisitorPassStatus? Status { get; init; }

    public DateTime? ValidFrom { get; init; }

    public DateTime? ValidUntil { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class VisitorAccessLogSearchQuery : PaginationQuery
{
}

public sealed class CreateVisitorPassRequest
{
    public Guid PropertyUnitId { get; init; }

    [Required]
    [MaxLength(150)]
    public string VisitorName { get; init; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string VisitorPhoneNumber { get; init; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string VisitReason { get; init; } = string.Empty;

    public DateTime ValidFrom { get; init; }

    public DateTime ValidUntil { get; init; }
}

public sealed class VisitorPassAccessRequest
{
    [MaxLength(80)]
    public string? AccessCode { get; init; }

    [MaxLength(500)]
    public string? Notes { get; init; }
}

public sealed class VerifyVisitorPassAccessCodeRequest
{
    [Required]
    [MaxLength(80)]
    public string AccessCode { get; init; } = string.Empty;
}

public sealed class DenyVisitorPassRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class CancelVisitorPassRequest
{
    [MaxLength(500)]
    public string? Reason { get; init; }
}
