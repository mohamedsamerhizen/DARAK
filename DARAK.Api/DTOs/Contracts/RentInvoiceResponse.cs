using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Rents;

public sealed record RentInvoiceResponse(
    Guid Id,
    Guid RentContractId,
    Guid CompoundId,
    string CompoundName,
    Guid PropertyUnitId,
    string UnitNumber,
    Guid ResidentProfileId,
    string ResidentFullName,
    string InvoiceNumber,
    int Year,
    int Month,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal RentAmount,
    decimal PreviousBalanceAmount,
    decimal LateFeeAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    RentInvoiceStatus RentInvoiceStatus,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? CancelledAt,
    string? CancellationReason);
