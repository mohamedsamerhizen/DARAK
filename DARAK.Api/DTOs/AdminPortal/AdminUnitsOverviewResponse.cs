namespace DARAK.Api.DTOs.AdminPortal;

public sealed record AdminUnitsOverviewResponse(
    int TotalUnits,
    int Available,
    int Occupied,
    int Rented,
    int SoldCash,
    int SoldInstallment,
    int UnderMaintenance,
    int Blocked,
    List<AdminUnitsByCompoundResponse> ByCompound);
