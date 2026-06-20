using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.UtilityBills;

public sealed record UtilityBillResponse(
    Guid Id,
    Guid CompoundId,
    string CompoundName,
    Guid PropertyUnitId,
    string UnitNumber,
    Guid? ResidentProfileId,
    string? ResidentFullName,
    Guid BillingCycleId,
    int BillingCycleYear,
    int BillingCycleMonth,
    string BillNumber,
    BillStatus BillStatus,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal SubtotalAmount,
    decimal PreviousBalanceAmount,
    decimal LateFeeAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? CancelledAt,
    string? CancellationReason,
    IReadOnlyCollection<UtilityBillLineResponse> Lines);
