namespace DARAK.Api.DTOs.System;

public sealed record SaasPortfolioOverviewResponse(
    DateTime GeneratedAtUtc,
    SaasLicenseSnapshotResponse License,
    SaasCapacitySnapshotResponse Capacity,
    int TotalAccessibleCompounds,
    int CriticalTenants,
    int HighPriorityTenants,
    int MediumPriorityTenants,
    int LowPriorityTenants,
    IReadOnlyCollection<SaasTenantPriorityItemResponse> PrioritizedTenants,
    IReadOnlyCollection<string> CommercialActions);

public sealed record SaasLicenseSnapshotResponse(
    Guid? LicenseProfileId,
    string LicensedTo,
    string Plan,
    string Status,
    int MaxCompounds,
    int MaxUnits,
    DateTime? ExpiresAtUtc,
    bool IsExpired,
    bool IsCapacityExceeded,
    int DaysUntilExpiry,
    string CommercialState);

public sealed record SaasCapacitySnapshotResponse(
    int Compounds,
    int Units,
    int Residents,
    int MaxCompounds,
    int MaxUnits,
    decimal CompoundUtilizationPercent,
    decimal UnitUtilizationPercent,
    string UtilizationBand);

public sealed record SaasTenantPriorityItemResponse(
    Guid CompoundId,
    string CompoundName,
    string CompoundCode,
    string PriorityBand,
    int PriorityScore,
    int Units,
    int Residents,
    decimal OutstandingAmount,
    int OpenOperationalItems,
    int OpenSupportCases,
    int OpenLegalItems,
    int FailedNotifications,
    int ActiveRiskFlags,
    IReadOnlyCollection<string> Reasons,
    IReadOnlyCollection<string> RecommendedActions);

public sealed record SaasTenantReadinessResponse(
    Guid CompoundId,
    string CompoundName,
    DateTime GeneratedAtUtc,
    string PriorityBand,
    int PriorityScore,
    string ReadinessBand,
    bool IsCommerciallyReady,
    SaasTenantOperationalSnapshotResponse OperationalSnapshot,
    SaasTenantFinancialSnapshotResponse FinancialSnapshot,
    SaasTenantReliabilitySnapshotResponse ReliabilitySnapshot,
    IReadOnlyCollection<string> Blockers,
    IReadOnlyCollection<string> RecommendedActions);

public sealed record SaasTenantOperationalSnapshotResponse(
    int Units,
    int OccupiedUnits,
    int Residents,
    int OpenMaintenanceRequests,
    int OpenWorkOrders,
    int OpenSupportCases);

public sealed record SaasTenantFinancialSnapshotResponse(
    decimal OutstandingAmount,
    int OpenBills,
    int OpenCollectionCases,
    int OpenLegalNotices,
    int ActiveRiskFlags);

public sealed record SaasTenantReliabilitySnapshotResponse(
    int PendingNotifications,
    int FailedNotifications,
    int OpenIntegrationFailures,
    int FailedBackgroundJobs24h,
    string ReliabilityBand);

public sealed record DarakPrioritizationBrainResponse(
    DateTime GeneratedAtUtc,
    string? AreaFilter,
    int TotalActions,
    IReadOnlyCollection<DarakPriorityActionResponse> Actions,
    IReadOnlyCollection<string> ExecutiveSummary);

public sealed record DarakPriorityActionResponse(
    string ActionKey,
    Guid CompoundId,
    string CompoundName,
    string Area,
    string PriorityBand,
    int PriorityScore,
    string Title,
    string Reason,
    string RecommendedOwner,
    string RecommendedNextStep,
    DateTime? DueAtUtc);
