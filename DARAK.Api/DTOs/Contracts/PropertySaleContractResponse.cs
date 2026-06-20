using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.PropertySales;

public sealed record PropertySaleContractResponse(
    Guid Id,
    Guid CompoundId,
    string CompoundName,
    Guid PropertyUnitId,
    string UnitNumber,
    Guid ResidentProfileId,
    string ResidentFullName,
    SaleType SaleType,
    SaleContractStatus ContractStatus,
    string ContractNumber,
    DateOnly ContractDate,
    decimal PropertyPrice,
    decimal DownPaymentAmount,
    int InstallmentCount,
    decimal TotalScheduledInstallments,
    decimal TotalPaidInstallments,
    decimal RemainingInstallmentBalance,
    DateOnly? FirstInstallmentDueDate,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? CancelledAt,
    string? CancellationReason,
    IReadOnlyCollection<InstallmentScheduleItemResponse> Installments);
