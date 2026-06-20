using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Reports;

public sealed class ManagementReportQuery
{
    public Guid? CompoundId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
}

public sealed class SavedReportSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public ManagementReportType? ReportType { get; init; }
    public bool IncludeInactive { get; init; }
    [MaxLength(150)] public string? SearchTerm { get; init; }
}

public sealed class CreateSavedReportRequest
{
    public Guid? CompoundId { get; init; }
    public ManagementReportType ReportType { get; init; }
    public SavedReportVisibility Visibility { get; init; } = SavedReportVisibility.Private;
    [Required, MaxLength(150)] public string Name { get; init; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; init; }
    [Required, MaxLength(4000)] public string FilterJson { get; init; } = "{}";
}

public sealed class CreateReportExportJobRequest
{
    public Guid? CompoundId { get; init; }
    public ManagementReportType ReportType { get; init; }
    public ReportExportFormat Format { get; init; } = ReportExportFormat.Csv;
    [Required, MaxLength(4000)] public string FilterJson { get; init; } = "{}";
}

public sealed class CompleteReportExportJobRequest
{
    [Required, MaxLength(300)] public string FileName { get; init; } = string.Empty;
    [Required, MaxLength(1000)] public string DownloadPath { get; init; } = string.Empty;
}

public sealed record FinancialManagementReportResponse(
    Guid? CompoundId,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal TotalBilledAmount,
    decimal TotalCollectedAmount,
    decimal OutstandingAmount,
    decimal ManualDebitAdjustments,
    decimal ManualCreditAdjustments,
    int OpenBillCount,
    int PaymentCount,
    DateTime GeneratedAtUtc);

public sealed record OccupancyManagementReportResponse(
    Guid? CompoundId,
    int TotalUnits,
    int OccupiedUnits,
    int AvailableUnits,
    int ActiveResidents,
    int ActiveOccupancies,
    double OccupancyRatePercent,
    DateTime GeneratedAtUtc);

public sealed record MaintenanceManagementReportResponse(
    Guid? CompoundId,
    DateTime FromUtc,
    DateTime ToUtc,
    int OpenMaintenanceRequests,
    int EmergencyMaintenanceRequests,
    int OpenWorkOrders,
    int OverdueWorkOrders,
    int CompletedWorkOrders,
    decimal EstimatedWorkOrderCost,
    decimal ActualWorkOrderCost,
    DateTime GeneratedAtUtc);

public sealed record SupportManagementReportResponse(
    Guid? CompoundId,
    int OpenCases,
    int EscalatedCases,
    int OverdueCases,
    int ResolvedCases,
    int ReopenedCases,
    double ResolutionRatePercent,
    DateTime GeneratedAtUtc);

public sealed record RiskAuditManagementReportResponse(
    Guid? CompoundId,
    DateTime FromUtc,
    DateTime ToUtc,
    int ActiveRiskFlags,
    int CriticalRiskFlags,
    int OverdueRiskReviews,
    int AuditEvents,
    int HighSeverityAuditEvents,
    int CriticalAuditEvents,
    DateTime GeneratedAtUtc);

public sealed record SavedReportResponse(
    Guid Id,
    Guid? CompoundId,
    Guid CreatedByUserId,
    ManagementReportType ReportType,
    SavedReportVisibility Visibility,
    string Name,
    string? Description,
    string FilterJson,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record ReportExportJobResponse(
    Guid Id,
    Guid? CompoundId,
    Guid RequestedByUserId,
    ManagementReportType ReportType,
    ReportExportFormat Format,
    ReportExportJobStatus Status,
    string FilterJson,
    string? FileName,
    string? DownloadPath,
    string? FailureReason,
    DateTime RequestedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc);

