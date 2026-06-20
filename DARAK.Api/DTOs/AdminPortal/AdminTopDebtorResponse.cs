namespace DARAK.Api.DTOs.AdminPortal;

public sealed record AdminTopDebtorResponse(
    Guid ResidentProfileId,
    string ResidentName,
    Guid CompoundId,
    string CompoundName,
    decimal TotalDebt,
    decimal UtilityDebt,
    decimal RentDebt,
    decimal InstallmentDebt);
