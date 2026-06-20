using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.PropertySales;

public sealed record InstallmentScheduleItemResponse(
    Guid Id,
    Guid PropertySaleContractId,
    Guid CompoundId,
    string CompoundName,
    Guid PropertyUnitId,
    string UnitNumber,
    Guid ResidentProfileId,
    string ResidentFullName,
    int InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,
    decimal PaidAmount,
    decimal RemainingAmount,
    InstallmentStatus InstallmentStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PaidAt,
    DateTime? CancelledAt,
    string? CancellationReason);
