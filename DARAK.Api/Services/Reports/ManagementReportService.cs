using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Reports;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DARAK.Api.Services;

public sealed class ManagementReportService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService,
    IAuditLogService auditLogService)
    : IManagementReportService
{
    public async Task<ServiceResult<FinancialManagementReportResponse>> GetFinancialReportAsync(
        ManagementReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await ValidateScopeAsync(query.CompoundId, cancellationToken);
        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<FinancialManagementReportResponse>.NotFound("Financial report was not found.");
        }

        var range = NormalizeDateRange(query.FromUtc, query.ToUtc);
        if (range.Error is not null)
        {
            return ServiceResult<FinancialManagementReportResponse>.BadRequest(range.Error);
        }

        var scope = scopeResult.Value!;
        var utilityBills = FilterByDate(ApplyCompoundFilter(dbContext.UtilityBills.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId), query.CompoundId), item => item.IssueDate, range.FromUtc, range.ToUtc);
        var rentInvoices = FilterByDate(ApplyCompoundFilter(dbContext.RentInvoices.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId), query.CompoundId), item => item.IssueDate, range.FromUtc, range.ToUtc);
        var payments = ApplyCompoundFilter(dbContext.Payments.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId), query.CompoundId)
            .Where(item => item.CreatedAt >= range.FromUtc && item.CreatedAt < range.ToUtc);
        var ledger = ApplyCompoundFilter(dbContext.ResidentLedgerEntries.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId), query.CompoundId)
            .Where(item => item.CreatedAtUtc >= range.FromUtc && item.CreatedAtUtc < range.ToUtc);

        var utilityBilled = await utilityBills.Where(item => item.BillStatus != BillStatus.Cancelled).SumAsync(item => item.TotalAmount, cancellationToken);
        var rentBilled = await rentInvoices.Where(item => item.RentInvoiceStatus != RentInvoiceStatus.Cancelled).SumAsync(item => item.TotalAmount, cancellationToken);
        var collected = await payments.Where(item => item.PaymentStatus == PaymentStatus.Succeeded).SumAsync(item => item.Amount, cancellationToken);
        var manualDebit = await ledger.Where(item => item.Direction == FinancialLedgerEntryDirection.Debit).SumAsync(item => item.Amount, cancellationToken);
        var manualCredit = await ledger.Where(item => item.Direction == FinancialLedgerEntryDirection.Credit).SumAsync(item => item.Amount, cancellationToken);
        var openBills = await utilityBills.CountAsync(item => item.BillStatus == BillStatus.Unpaid || item.BillStatus == BillStatus.PartiallyPaid || item.BillStatus == BillStatus.Overdue, cancellationToken)
            + await rentInvoices.CountAsync(item => item.RentInvoiceStatus == RentInvoiceStatus.Unpaid || item.RentInvoiceStatus == RentInvoiceStatus.PartiallyPaid || item.RentInvoiceStatus == RentInvoiceStatus.Overdue, cancellationToken);
        var paymentCount = await payments.CountAsync(item => item.PaymentStatus == PaymentStatus.Succeeded, cancellationToken);

        var totalBilled = utilityBilled + rentBilled + manualDebit;
        var totalCollected = collected + manualCredit;
        return ServiceResult<FinancialManagementReportResponse>.Success(new FinancialManagementReportResponse(
            query.CompoundId,
            range.FromUtc,
            range.ToUtc,
            totalBilled,
            totalCollected,
            Math.Max(0m, totalBilled - totalCollected),
            manualDebit,
            manualCredit,
            openBills,
            paymentCount,
            DateTime.UtcNow));
    }

    public async Task<ServiceResult<OccupancyManagementReportResponse>> GetOccupancyReportAsync(
        ManagementReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await ValidateScopeAsync(query.CompoundId, cancellationToken);
        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<OccupancyManagementReportResponse>.NotFound("Occupancy report was not found.");
        }

        var scope = scopeResult.Value!;
        var units = ApplyCompoundFilter(dbContext.PropertyUnits.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId), query.CompoundId);
        var residents = ApplyCompoundFilter(dbContext.ResidentProfiles.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId), query.CompoundId);
        var occupancies = ApplyCompoundFilter(dbContext.OccupancyRecords.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId), query.CompoundId);

        var totalUnits = await units.CountAsync(cancellationToken);
        var availableUnits = await units.CountAsync(item => item.UnitStatus == UnitStatus.Available, cancellationToken);
        var occupiedUnits = await units.CountAsync(item => item.UnitStatus == UnitStatus.Occupied, cancellationToken);
        var activeResidents = await residents.CountAsync(item => item.IsActive, cancellationToken);
        var activeOccupancies = await occupancies.CountAsync(item => item.OccupancyStatus == OccupancyStatus.Active, cancellationToken);
        var occupancyRate = totalUnits == 0 ? 0 : Math.Round(occupiedUnits * 100.0 / totalUnits, 2);

        return ServiceResult<OccupancyManagementReportResponse>.Success(new OccupancyManagementReportResponse(
            query.CompoundId,
            totalUnits,
            occupiedUnits,
            availableUnits,
            activeResidents,
            activeOccupancies,
            occupancyRate,
            DateTime.UtcNow));
    }

    public async Task<ServiceResult<MaintenanceManagementReportResponse>> GetMaintenanceReportAsync(
        ManagementReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await ValidateScopeAsync(query.CompoundId, cancellationToken);
        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<MaintenanceManagementReportResponse>.NotFound("Maintenance report was not found.");
        }

        var range = NormalizeDateRange(query.FromUtc, query.ToUtc);
        if (range.Error is not null)
        {
            return ServiceResult<MaintenanceManagementReportResponse>.BadRequest(range.Error);
        }

        var scope = scopeResult.Value!;
        var now = DateTime.UtcNow;
        var maintenance = ApplyCompoundFilter(dbContext.MaintenanceRequests.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId), query.CompoundId)
            .Where(item => item.CreatedAt >= range.FromUtc && item.CreatedAt < range.ToUtc);
        var workOrders = ApplyCompoundFilter(dbContext.WorkOrders.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId), query.CompoundId)
            .Where(item => item.CreatedAtUtc >= range.FromUtc && item.CreatedAtUtc < range.ToUtc);

        return ServiceResult<MaintenanceManagementReportResponse>.Success(new MaintenanceManagementReportResponse(
            query.CompoundId,
            range.FromUtc,
            range.ToUtc,
            await maintenance.CountAsync(item => item.Status == MaintenanceStatus.Open || item.Status == MaintenanceStatus.Assigned || item.Status == MaintenanceStatus.InProgress, cancellationToken),
            await maintenance.CountAsync(item => item.Status != MaintenanceStatus.Closed && item.Priority == MaintenancePriority.Emergency, cancellationToken),
            await workOrders.CountAsync(item => item.Status == WorkOrderStatus.New || item.Status == WorkOrderStatus.Assigned || item.Status == WorkOrderStatus.Scheduled || item.Status == WorkOrderStatus.InProgress, cancellationToken),
            await workOrders.CountAsync(item => (item.Status == WorkOrderStatus.New || item.Status == WorkOrderStatus.Assigned || item.Status == WorkOrderStatus.Scheduled || item.Status == WorkOrderStatus.InProgress) && item.DueAtUtc.HasValue && item.DueAtUtc.Value < now, cancellationToken),
            await workOrders.CountAsync(item => item.Status == WorkOrderStatus.Completed, cancellationToken),
            await workOrders.SumAsync(item => item.EstimatedCost ?? 0m, cancellationToken),
            await workOrders.SumAsync(item => item.ActualCost ?? 0m, cancellationToken),
            DateTime.UtcNow));
    }

    public async Task<ServiceResult<SupportManagementReportResponse>> GetSupportReportAsync(
        ManagementReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await ValidateScopeAsync(query.CompoundId, cancellationToken);
        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<SupportManagementReportResponse>.NotFound("Support report was not found.");
        }

        var scope = scopeResult.Value!;
        var now = DateTime.UtcNow;
        var cases = ApplyCompoundFilter(dbContext.SupportCases.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId), query.CompoundId);
        var total = await cases.CountAsync(cancellationToken);
        var resolved = await cases.CountAsync(item => item.Status == SupportCaseStatus.Resolved || item.Status == SupportCaseStatus.Closed, cancellationToken);

        return ServiceResult<SupportManagementReportResponse>.Success(new SupportManagementReportResponse(
            query.CompoundId,
            await cases.CountAsync(item => item.Status == SupportCaseStatus.Open || item.Status == SupportCaseStatus.Assigned || item.Status == SupportCaseStatus.InProgress, cancellationToken),
            await cases.CountAsync(item => item.Status == SupportCaseStatus.Escalated, cancellationToken),
            await cases.CountAsync(item => item.Status != SupportCaseStatus.Closed && item.Status != SupportCaseStatus.Cancelled && item.Status != SupportCaseStatus.Resolved && item.DueAtUtc < now, cancellationToken),
            resolved,
            await cases.CountAsync(item => item.ReopenCount > 0, cancellationToken),
            total == 0 ? 0 : Math.Round(resolved * 100.0 / total, 2),
            DateTime.UtcNow));
    }

    public async Task<ServiceResult<RiskAuditManagementReportResponse>> GetRiskAuditReportAsync(
        ManagementReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await ValidateScopeAsync(query.CompoundId, cancellationToken);
        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<RiskAuditManagementReportResponse>.NotFound("Risk and audit report was not found.");
        }

        var range = NormalizeDateRange(query.FromUtc, query.ToUtc);
        if (range.Error is not null)
        {
            return ServiceResult<RiskAuditManagementReportResponse>.BadRequest(range.Error);
        }

        var scope = scopeResult.Value!;
        var now = DateTime.UtcNow;
        var riskFlags = ApplyCompoundFilter(dbContext.ResidentRiskFlags.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId), query.CompoundId);
        var audits = ApplyNullableCompoundFilter(dbContext.AuditLogEntries.AsNoTracking(), scope, query.CompoundId)
            .Where(item => item.CreatedAtUtc >= range.FromUtc && item.CreatedAtUtc < range.ToUtc);

        return ServiceResult<RiskAuditManagementReportResponse>.Success(new RiskAuditManagementReportResponse(
            query.CompoundId,
            range.FromUtc,
            range.ToUtc,
            await riskFlags.CountAsync(item => item.Status == ResidentRiskFlagStatus.Active || item.Status == ResidentRiskFlagStatus.Monitoring, cancellationToken),
            await riskFlags.CountAsync(item => (item.Status == ResidentRiskFlagStatus.Active || item.Status == ResidentRiskFlagStatus.Monitoring) && item.Severity == ResidentRiskFlagSeverity.Critical, cancellationToken),
            await riskFlags.CountAsync(item => (item.Status == ResidentRiskFlagStatus.Active || item.Status == ResidentRiskFlagStatus.Monitoring) && item.NextReviewAtUtc.HasValue && item.NextReviewAtUtc.Value < now, cancellationToken),
            await audits.CountAsync(cancellationToken),
            await audits.CountAsync(item => item.Severity == AuditSeverity.High, cancellationToken),
            await audits.CountAsync(item => item.Severity == AuditSeverity.Critical, cancellationToken),
            DateTime.UtcNow));
    }

    public async Task<ServiceResult<PagedResult<SavedReportResponse>>> SearchSavedReportsAsync(
        SavedReportSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await ValidateScopeAsync(query.CompoundId, cancellationToken);
        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<PagedResult<SavedReportResponse>>.NotFound("Saved reports were not found.");
        }

        var reports = ApplyNullableCompoundFilter(dbContext.SavedReports.AsNoTracking(), scopeResult.Value!, query.CompoundId);
        if (query.ReportType.HasValue)
        {
            reports = reports.Where(item => item.ReportType == query.ReportType.Value);
        }
        if (!query.IncludeInactive)
        {
            reports = reports.Where(item => item.IsActive);
        }
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            reports = reports.Where(item => item.Name.Contains(term) || (item.Description != null && item.Description.Contains(term)));
        }

        var total = await reports.CountAsync(cancellationToken);
        var rows = await reports
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var items = rows.Select(ToSavedReportResponse).ToArray();

        return ServiceResult<PagedResult<SavedReportResponse>>.Success(new PagedResult<SavedReportResponse>(items, query.PageNumber, query.PageSize, total));
    }

    public async Task<ServiceResult<SavedReportResponse>> CreateSavedReportAsync(
        Guid? currentUserId,
        CreateSavedReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<SavedReportResponse>.Forbidden("Current user is required.");
        }
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ServiceResult<SavedReportResponse>.BadRequest("Report name is required.");
        }

        var scopeResult = await ValidateScopeAsync(request.CompoundId, cancellationToken);
        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<SavedReportResponse>.NotFound("Saved report target was not found.");
        }

        var normalizedFilter = NormalizeFilterJson(request.FilterJson);
        if (normalizedFilter.Error is not null)
        {
            return ServiceResult<SavedReportResponse>.BadRequest(normalizedFilter.Error);
        }

        var saved = new SavedReport
        {
            CompoundId = request.CompoundId,
            CreatedByUserId = currentUserId.Value,
            ReportType = request.ReportType,
            Visibility = request.Visibility,
            Name = Truncate(request.Name.Trim(), 150)!,
            Description = Truncate(request.Description, 1000),
            FilterJson = normalizedFilter.Value!,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.SavedReports.Add(saved);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            saved.CompoundId,
            null,
            currentUserId,
            null,
            AuditActionType.SavedReportCreated,
            AuditEntityType.SavedReport,
            saved.Id,
            AuditSeverity.Low,
            "Reports",
            $"Saved report '{saved.Name}' was created."),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<SavedReportResponse>.Success(ToSavedReportResponse(saved));
    }

    public async Task<ServiceResult<ReportExportJobResponse>> CreateExportJobAsync(
        Guid? currentUserId,
        CreateReportExportJobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ReportExportJobResponse>.Forbidden("Current user is required.");
        }

        var scopeResult = await ValidateScopeAsync(request.CompoundId, cancellationToken);
        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<ReportExportJobResponse>.NotFound("Report export target was not found.");
        }

        var normalizedFilter = NormalizeFilterJson(request.FilterJson);
        if (normalizedFilter.Error is not null)
        {
            return ServiceResult<ReportExportJobResponse>.BadRequest(normalizedFilter.Error);
        }

        var job = new ReportExportJob
        {
            CompoundId = request.CompoundId,
            RequestedByUserId = currentUserId.Value,
            ReportType = request.ReportType,
            Format = request.Format,
            Status = ReportExportJobStatus.Queued,
            FilterJson = normalizedFilter.Value!,
            RequestedAtUtc = DateTime.UtcNow
        };
        dbContext.ReportExportJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            job.CompoundId,
            null,
            currentUserId,
            null,
            AuditActionType.ReportExportQueued,
            AuditEntityType.ReportExportJob,
            job.Id,
            AuditSeverity.Low,
            "Reports",
            $"{job.ReportType} report export was queued."),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ReportExportJobResponse>.Success(ToExportJobResponse(job));
    }

    public async Task<ServiceResult<ReportExportJobResponse>> CompleteExportJobAsync(
        Guid? currentUserId,
        Guid id,
        CompleteReportExportJobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.DownloadPath))
        {
            return ServiceResult<ReportExportJobResponse>.BadRequest("File name and download path are required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var job = await ApplyNullableCompoundFilter(dbContext.ReportExportJobs, scope, null)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (job is null)
        {
            return ServiceResult<ReportExportJobResponse>.NotFound("Report export job was not found.");
        }
        if (job.Status is ReportExportJobStatus.Completed or ReportExportJobStatus.Cancelled)
        {
            return ServiceResult<ReportExportJobResponse>.Conflict("Report export job is already finalized.");
        }

        job.Status = ReportExportJobStatus.Completed;
        job.FileName = Truncate(request.FileName, 300);
        job.DownloadPath = Truncate(request.DownloadPath, 1000);
        job.CompletedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            job.CompoundId,
            null,
            currentUserId,
            null,
            AuditActionType.ReportExportCompleted,
            AuditEntityType.ReportExportJob,
            job.Id,
            AuditSeverity.Low,
            "Reports",
            $"{job.ReportType} report export was completed."),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ReportExportJobResponse>.Success(ToExportJobResponse(job));
    }

    private async Task<ServiceResult<CompoundAccessScope>> ValidateScopeAsync(Guid? compoundId, CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<CompoundAccessScope>.Forbidden("Current user is required.");
        }
        if (compoundId.HasValue && !scope.CanAccess(compoundId.Value))
        {
            return ServiceResult<CompoundAccessScope>.NotFound("Compound was not found.");
        }
        return ServiceResult<CompoundAccessScope>.Success(scope);
    }

    private static IQueryable<T> ApplyCompoundFilter<T>(IQueryable<T> query, Guid? compoundId) where T : class
    {
        return compoundId.HasValue ? query.Where(item => EF.Property<Guid>(item, "CompoundId") == compoundId.Value) : query;
    }

    private static IQueryable<T> ApplyNullableCompoundFilter<T>(IQueryable<T> query, CompoundAccessScope scope, Guid? compoundId) where T : class
    {
        if (!scope.IsAuthenticated)
        {
            return query.Where(_ => false);
        }
        if (compoundId.HasValue)
        {
            return query.Where(item => EF.Property<Guid?>(item, "CompoundId") == compoundId.Value);
        }
        if (scope.IsSuperAdmin)
        {
            return query;
        }
        if (scope.AllowedCompoundIds.Length == 0)
        {
            return query.Where(item => EF.Property<Guid?>(item, "CompoundId") == null);
        }
        return query.Where(item => EF.Property<Guid?>(item, "CompoundId") == null || scope.AllowedCompoundIds.Contains(EF.Property<Guid?>(item, "CompoundId")!.Value));
    }

    private static IQueryable<UtilityBill> FilterByDate(IQueryable<UtilityBill> query, System.Linq.Expressions.Expression<Func<UtilityBill, DateOnly>> selector, DateTime fromUtc, DateTime toUtc)
    {
        var from = DateOnly.FromDateTime(fromUtc);
        var to = DateOnly.FromDateTime(toUtc);
        return query.Where(item => item.IssueDate >= from && item.IssueDate < to);
    }

    private static IQueryable<RentInvoice> FilterByDate(IQueryable<RentInvoice> query, System.Linq.Expressions.Expression<Func<RentInvoice, DateOnly>> selector, DateTime fromUtc, DateTime toUtc)
    {
        var from = DateOnly.FromDateTime(fromUtc);
        var to = DateOnly.FromDateTime(toUtc);
        return query.Where(item => item.IssueDate >= from && item.IssueDate < to);
    }

    private static (DateTime FromUtc, DateTime ToUtc, string? Error) NormalizeDateRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var to = toUtc ?? DateTime.UtcNow.AddDays(1);
        var from = fromUtc ?? to.AddDays(-30);
        if (from >= to)
        {
            return (from, to, "From date must be before to date.");
        }
        return (from, to, null);
    }

    private static SavedReportResponse ToSavedReportResponse(SavedReport item)
    {
        return new SavedReportResponse(
            item.Id,
            item.CompoundId,
            item.CreatedByUserId,
            item.ReportType,
            item.Visibility,
            item.Name,
            item.Description,
            item.FilterJson,
            item.IsActive,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }

    private static ReportExportJobResponse ToExportJobResponse(ReportExportJob item)
    {
        return new ReportExportJobResponse(
            item.Id,
            item.CompoundId,
            item.RequestedByUserId,
            item.ReportType,
            item.Format,
            item.Status,
            item.FilterJson,
            item.FileName,
            item.DownloadPath,
            item.FailureReason,
            item.RequestedAtUtc,
            item.StartedAtUtc,
            item.CompletedAtUtc);
    }

    private static (string? Value, string? Error) NormalizeFilterJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ("{}", null);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 4000)
        {
            return (null, "Report filter JSON must not exceed 4000 characters.");
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var normalized = JsonSerializer.Serialize(document.RootElement);
            if (normalized.Length > 4000)
            {
                return (null, "Report filter JSON must not exceed 4000 characters.");
            }

            return (normalized, null);
        }
        catch (JsonException)
        {
            return (null, "Report filter JSON must be valid JSON.");
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}


