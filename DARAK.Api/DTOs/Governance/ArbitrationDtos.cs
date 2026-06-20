using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Governance;

public sealed class ArbitrationCaseQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public ArbitrationCaseSourceType? SourceType { get; init; }

    public ArbitrationCaseStatus? Status { get; init; }

    public ArbitrationCasePriority? Priority { get; init; }
}

public sealed class CreateArbitrationCaseRequest
{
    public Guid CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public ArbitrationCaseSourceType SourceType { get; init; }

    public Guid SourceId { get; init; }

    public ArbitrationCasePriority Priority { get; init; } = ArbitrationCasePriority.Normal;

    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class AddArbitrationCaseEventRequest
{
    public ArbitrationCaseEventType EventType { get; init; } = ArbitrationCaseEventType.NoteAdded;

    [Required]
    [MaxLength(4000)]
    public string Message { get; init; } = string.Empty;
}

public sealed class IssueArbitrationFinalDecisionRequest
{
    [Required]
    [MaxLength(2000)]
    public string Decision { get; init; } = string.Empty;

    [MaxLength(4000)]
    public string? DecisionSummary { get; init; }
}

public sealed class CancelArbitrationCaseRequest
{
    [Required]
    [MaxLength(2000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record ArbitrationCaseSummaryResponse(
    int OpenCases,
    int UnderReviewCases,
    int CriticalCases,
    int FinalDecisionCases,
    int CancelledCases);

public sealed record ArbitrationCaseEventResponse(
    Guid Id,
    ArbitrationCaseEventType EventType,
    string Message,
    Guid? CreatedByUserId,
    DateTime CreatedAtUtc);

public sealed record ArbitrationCaseResponse(
    Guid Id,
    Guid CompoundId,
    string CompoundName,
    Guid? ResidentProfileId,
    string? ResidentName,
    ArbitrationCaseSourceType SourceType,
    Guid SourceId,
    ArbitrationCaseStatus Status,
    ArbitrationCasePriority Priority,
    string Title,
    string Reason,
    string? FinalDecision,
    string? FinalDecisionSummary,
    Guid? CreatedByUserId,
    Guid? DecidedByUserId,
    Guid? CancelledByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? DecisionIssuedAtUtc,
    DateTime? CancelledAtUtc,
    IReadOnlyCollection<ArbitrationCaseEventResponse> Events);
