namespace DARAK.Api.DTOs.AdminPortal;

public sealed record AdminUnitsByCompoundResponse(
    Guid CompoundId,
    string CompoundName,
    int TotalUnits,
    int Available,
    int Occupied,
    int Rented,
    int SoldCash,
    int SoldInstallment,
    int UnderMaintenance,
    int Blocked);
