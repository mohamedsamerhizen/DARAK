namespace DARAK.Api.DTOs.System;

public sealed record Resident360ProfileResponse(
    Guid ResidentId,
    Guid CompoundId,
    string FullName,
    bool IsActive,
    Darak360CurrentUnitResponse? CurrentUnit,
    Darak360FinancialSnapshotResponse FinancialSnapshot,
    Darak360OperationsSnapshotResponse OperationsSnapshot,
    Darak360LegalRiskSnapshotResponse LegalRiskSnapshot,
    Darak360CommunicationSnapshotResponse CommunicationSnapshot,
    IReadOnlyCollection<Darak360SignalResponse> Signals,
    IReadOnlyCollection<string> RecommendedActions,
    DateTime GeneratedAtUtc);

public sealed record Unit360ProfileResponse(
    Guid UnitId,
    Guid CompoundId,
    string UnitNumber,
    string UnitStatus,
    string PropertyType,
    Darak360CurrentResidentResponse? CurrentResident,
    Darak360FinancialSnapshotResponse FinancialSnapshot,
    Darak360UnitOperationsSnapshotResponse OperationsSnapshot,
    Darak360LifecycleSnapshotResponse LifecycleSnapshot,
    IReadOnlyCollection<Darak360SignalResponse> Signals,
    IReadOnlyCollection<string> RecommendedActions,
    DateTime GeneratedAtUtc);

public sealed record Compound360OverviewResponse(
    Guid CompoundId,
    string CompoundName,
    string CompoundCode,
    Darak360InventorySnapshotResponse InventorySnapshot,
    Darak360FinancialSnapshotResponse FinancialSnapshot,
    Darak360OperationsSnapshotResponse OperationsSnapshot,
    Darak360LegalRiskSnapshotResponse LegalRiskSnapshot,
    Darak360CommunicationSnapshotResponse CommunicationSnapshot,
    IReadOnlyCollection<Darak360SignalResponse> Signals,
    IReadOnlyCollection<string> RecommendedActions,
    DateTime GeneratedAtUtc);

public sealed record Darak360CurrentUnitResponse(
    Guid UnitId,
    string UnitNumber,
    string UnitStatus,
    string PropertyType,
    string OccupancyType,
    DateOnly StartDate,
    string? ContractNumber);

public sealed record Darak360CurrentResidentResponse(
    Guid ResidentId,
    string FullName,
    string? PhoneNumber,
    string OccupancyType,
    DateOnly StartDate,
    string? ContractNumber);

public sealed record Darak360InventorySnapshotResponse(
    int Buildings,
    int Floors,
    int Units,
    int AvailableUnits,
    int OccupiedUnits,
    int Residents,
    int ActiveOccupancies);

public sealed record Darak360FinancialSnapshotResponse(
    int UtilityBills,
    int RentInvoices,
    int Payments,
    decimal TotalBilled,
    decimal TotalPaid,
    decimal OutstandingAmount,
    int OpenDisputes,
    int CollectionCases);

public sealed record Darak360OperationsSnapshotResponse(
    int MaintenanceRequests,
    int WorkOrders,
    int SupportCases,
    int VisitorPasses,
    int ActiveRiskFlags);

public sealed record Darak360UnitOperationsSnapshotResponse(
    int Meters,
    int MaintenanceRequests,
    int WorkOrders,
    int VisitorPasses,
    int SupportCases,
    int ActiveRiskFlags);

public sealed record Darak360LifecycleSnapshotResponse(
    int OccupancyHistoryRecords,
    int ActiveLifecycleProcesses,
    int ReadinessRecords,
    string? LatestReadinessStatus,
    int OpenDamageLiabilities,
    decimal EstimatedDamageAmount);

public sealed record Darak360LegalRiskSnapshotResponse(
    int Violations,
    int ViolationFines,
    decimal OpenFineAmount,
    int OpenDisputes,
    int OpenCollectionCases,
    int LegalNotices,
    int ActiveRiskFlags);

public sealed record Darak360CommunicationSnapshotResponse(
    int Conversations,
    int OpenConversations,
    int Notifications,
    int UnreadNotifications,
    int Announcements,
    int ActiveOutages);

public sealed record Darak360SignalResponse(
    string SignalKey,
    string Severity,
    string Title,
    string Description);
