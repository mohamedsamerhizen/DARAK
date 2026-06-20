using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Rents;

public sealed record RentContractResponse(
    Guid Id,
    Guid CompoundId,
    string CompoundName,
    Guid PropertyUnitId,
    string UnitNumber,
    Guid ResidentProfileId,
    string ResidentFullName,
    string ContractNumber,
    RentContractStatus ContractStatus,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal MonthlyRentAmount,
    decimal DepositAmount,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? TerminatedAt,
    string? TerminationReason,
    DateTime? CancelledAt,
    string? CancellationReason);
