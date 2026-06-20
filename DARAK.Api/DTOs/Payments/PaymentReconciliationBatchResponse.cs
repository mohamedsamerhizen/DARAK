using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Payments;

public sealed record PaymentReconciliationBatchResponse(
    Guid Id,
    Guid CompoundId,
    string CompoundName,
    string Provider,
    string StatementReference,
    DateOnly StatementDate,
    PaymentReconciliationBatchStatus Status,
    int TotalItems,
    int MatchedItems,
    int IssueItems,
    int ReviewRequiredItems,
    int ReviewedIssueItems,
    int UnreviewedIssueItems,
    decimal TotalDifferenceAmount,
    string? Notes,
    DateTime CreatedAtUtc,
    Guid? CreatedByUserId,
    DateTime? ClosedAtUtc,
    Guid? ClosedByUserId,
    IReadOnlyCollection<PaymentReconciliationItemResponse> Items);
