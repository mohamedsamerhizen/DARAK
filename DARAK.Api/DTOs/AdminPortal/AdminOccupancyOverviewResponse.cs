namespace DARAK.Api.DTOs.AdminPortal;

public sealed record AdminOccupancyOverviewResponse(
    int ActiveOccupancies,
    int Tenants,
    int OwnerCash,
    int OwnerInstallment,
    int EndedOccupancies,
    int CancelledOccupancies);
