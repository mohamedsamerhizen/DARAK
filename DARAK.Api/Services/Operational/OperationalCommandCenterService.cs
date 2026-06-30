using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operational;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class OperationalCommandCenterService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService,
    IAuditLogService auditLogService)
    : IOperationalCommandCenterService
{
    private static readonly MaintenanceStatus[] OpenMaintenanceStatuses =
    [
        MaintenanceStatus.Open,
        MaintenanceStatus.Assigned,
        MaintenanceStatus.InProgress
    ];

    private static readonly ComplaintStatus[] OpenComplaintStatuses =
    [
        ComplaintStatus.Open,
        ComplaintStatus.UnderReview
    ];

    private static readonly WorkOrderStatus[] OpenWorkOrderStatuses =
    [
        WorkOrderStatus.New,
        WorkOrderStatus.Assigned,
        WorkOrderStatus.Scheduled,
        WorkOrderStatus.InProgress
    ];

    private static readonly ApprovalStatus[] PendingApprovalStatuses =
    [
        ApprovalStatus.Pending
    ];

    private static readonly ResidentRiskFlagStatus[] ActiveRiskFlagStatuses =
    [
        ResidentRiskFlagStatus.Active,
        ResidentRiskFlagStatus.Monitoring
    ];


    public async Task<ServiceResult<AdminCommandCenterIntelligenceResponse>> GetIntelligenceAsync(
        AdminCommandCenterIntelligenceQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await GetValidatedScopeAsync(query.CompoundId, cancellationToken);
        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<AdminCommandCenterIntelligenceResponse>.NotFound("Command center intelligence was not found.");
        }

        var scope = scopeResult.Value!;
        var now = DateTime.UtcNow;
        var limit = Math.Clamp(query.CriticalItemLimit, 1, 50);

        var financialDisputes = ApplyOptionalCompoundFilter(
            dbContext.FinancialDisputes.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var violationAppeals = ApplyOptionalCompoundFilter(
            dbContext.ViolationAppeals.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var financialAdjustments = ApplyOptionalCompoundFilter(
            dbContext.FinancialAdjustments.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var collectionCases = ApplyOptionalCompoundFilter(
            dbContext.CollectionCases.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var legalNotices = ApplyOptionalCompoundFilter(
            dbContext.LegalNotices.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var workOrders = ApplyOptionalCompoundFilter(
            dbContext.WorkOrders.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var assets = ApplyOptionalCompoundFilter(
            dbContext.MaintenanceAssets.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var lifecycleProcesses = ApplyOptionalCompoundFilter(
            dbContext.ResidentLifecycleProcesses.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var custodyItems = ApplyOptionalCompoundFilter(
            dbContext.ResidentCustodyItems.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var movePermits = ApplyOptionalCompoundFilter(
            dbContext.MoveLogisticsPermits.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var procurementRequests = ApplyOptionalCompoundFilter(
            dbContext.ProcurementRequests.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var purchaseOrders = ApplyOptionalCompoundFilter(
            dbContext.PurchaseOrders.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var stockItems = ApplyOptionalCompoundFilter(
            dbContext.StockItems.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var contractorPermits = ApplyOptionalCompoundFilter(
            dbContext.ContractorWorkPermits.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var accessCredentials = ApplyOptionalCompoundFilter(
            dbContext.AccessCredentials.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var outages = ApplyOptionalCompoundFilter(
            dbContext.UtilityOutages.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var smartDevices = ApplyOptionalCompoundFilter(
            dbContext.SmartMeterDevices.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var ingestions = ApplyOptionalCompoundFilter(
            dbContext.SmartMeterReadingIngestions.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);

        var financialOpen = await financialDisputes.CountAsync(item =>
                item.Status == FinancialDisputeStatus.Open
                || item.Status == FinancialDisputeStatus.UnderReview
                || item.Status == FinancialDisputeStatus.NeedResidentResponse,
            cancellationToken);
        var appealOpen = await violationAppeals.CountAsync(item =>
                item.Status == ViolationAppealStatus.Submitted
                || item.Status == ViolationAppealStatus.UnderReview
                || item.Status == ViolationAppealStatus.NeedResidentResponse,
            cancellationToken);
        var pendingAdjustments = await financialAdjustments.CountAsync(item => item.Status == FinancialAdjustmentStatus.PendingApproval, cancellationToken);
        var openCollections = await collectionCases.CountAsync(item =>
                item.Status == CollectionCaseStatus.Open
                || item.Status == CollectionCaseStatus.Paused
                || item.Status == CollectionCaseStatus.LegalEscalated
                || item.Status == CollectionCaseStatus.PaymentPlanActive,
            cancellationToken);
        var legalNoticesDue = await legalNotices.CountAsync(item => item.Status == LegalNoticeStatus.Issued, cancellationToken);

        var workOrderOpen = await workOrders.CountAsync(item =>
                item.Status == WorkOrderStatus.New
                || item.Status == WorkOrderStatus.Assigned
                || item.Status == WorkOrderStatus.Scheduled
                || item.Status == WorkOrderStatus.InProgress,
            cancellationToken);
        var slaBreaches = await workOrders.CountAsync(item =>
                item.SlaStatus == MaintenanceSlaStatus.ResponseBreached
                || item.SlaStatus == MaintenanceSlaStatus.ResolutionBreached
                || item.SlaStatus == MaintenanceSlaStatus.Escalated,
            cancellationToken);
        var assetAttention = await assets.CountAsync(item =>
                item.Status == MaintenanceAssetStatus.OutOfService
                || item.Status == MaintenanceAssetStatus.UnderMaintenance,
            cancellationToken);

        var lifecycleAttention = await lifecycleProcesses.CountAsync(item =>
                item.Status != ResidentLifecycleStatus.Completed
                && item.Status != ResidentLifecycleStatus.Cancelled,
            cancellationToken);
        var custodyAttention = await custodyItems.CountAsync(item =>
                item.Status == CustodyItemStatus.Issued
                || item.Status == CustodyItemStatus.Lost
                || item.Status == CustodyItemStatus.Damaged,
            cancellationToken);
        var movePermitAttention = await movePermits.CountAsync(item =>
                item.Status == MoveLogisticsPermitStatus.PendingApproval
                || item.Status == MoveLogisticsPermitStatus.Approved,
            cancellationToken);

        var procurementAttention = await procurementRequests.CountAsync(item => item.Status == ProcurementRequestStatus.PendingApproval, cancellationToken);
        var purchaseOrderAttention = await purchaseOrders.CountAsync(item =>
                item.Status == PurchaseOrderStatus.Ordered
                || item.Status == PurchaseOrderStatus.PartiallyReceived,
            cancellationToken);
        var lowStockAttention = await stockItems.CountAsync(item => item.CurrentQuantity <= item.MinimumQuantity, cancellationToken);

        var contractorAttention = await contractorPermits.CountAsync(item =>
                item.Status == ContractorWorkPermitStatus.PendingApproval
                || item.Status == ContractorWorkPermitStatus.Approved
                || item.Status == ContractorWorkPermitStatus.CheckedIn,
            cancellationToken);
        var credentialAttention = await accessCredentials.CountAsync(item =>
                item.Status == AccessCredentialStatus.Active
                && item.ValidUntilUtc.HasValue
                && item.ValidUntilUtc.Value < now,
            cancellationToken);

        var outageAttention = await outages.CountAsync(item =>
                item.Status == UtilityOutageStatus.Planned
                || item.Status == UtilityOutageStatus.Active,
            cancellationToken);
        var criticalOutageCount = await outages.CountAsync(item =>
                (item.Status == UtilityOutageStatus.Planned || item.Status == UtilityOutageStatus.Active)
                && item.Severity == UtilityOutageSeverity.Critical,
            cancellationToken);

        var offlineDeviceCount = await smartDevices.CountAsync(item => item.HealthStatus == SmartMeterDeviceHealthStatus.Offline, cancellationToken);
        var suspiciousIngestionCount = await ingestions.CountAsync(item =>
                item.Status == SmartMeterReadingIngestionStatus.Suspicious
                || item.BillingHoldRecommended,
            cancellationToken);

        var domains = new List<CommandCenterDomainCardResponse>
        {
            BuildDomain("Finance", "Financial Governance", financialOpen + appealOpen + pendingAdjustments + openCollections + legalNoticesDue, openCollections + legalNoticesDue, financialOpen + appealOpen + pendingAdjustments, "Review disputes, collections, legal notices, and pending adjustments."),
            BuildDomain("Maintenance", "Maintenance Reliability", workOrderOpen + assetAttention, slaBreaches, assetAttention, "Resolve breached work orders and restore unavailable assets."),
            BuildDomain("Lifecycle", "Resident Lifecycle", lifecycleAttention + custodyAttention + movePermitAttention, custodyAttention, lifecycleAttention + movePermitAttention, "Clear move-in/out, custody, and logistics blockers."),
            BuildDomain("Procurement", "Procurement & Inventory", procurementAttention + purchaseOrderAttention + lowStockAttention, lowStockAttention, procurementAttention + purchaseOrderAttention, "Approve procurement requests and replenish low-stock items."),
            BuildDomain("Access", "Access & Contractors", contractorAttention + credentialAttention, contractorAttention, credentialAttention, "Approve contractor permits and clean expired active credentials."),
            BuildDomain("Communications", "Outages & Communications", outageAttention, criticalOutageCount, outageAttention - criticalOutageCount, "Publish updates for active outages and close resolved service interruptions."),
            BuildDomain("SmartMeters", "Smart Meter Reliability", offlineDeviceCount + suspiciousIngestionCount, offlineDeviceCount, suspiciousIngestionCount, "Review offline devices and billing-hold recommendations.")
        };

        var criticalItems = new List<CommandCenterCriticalItemResponse>();
        criticalItems.AddRange(await BuildFinanceCriticalItemsAsync(financialDisputes, collectionCases, legalNotices, now, limit, cancellationToken));
        criticalItems.AddRange(await BuildMaintenanceCriticalItemsAsync(workOrders, now, limit, cancellationToken));
        criticalItems.AddRange(await BuildLifecycleCriticalItemsAsync(lifecycleProcesses, now, limit, cancellationToken));
        criticalItems.AddRange(await BuildAccessCriticalItemsAsync(contractorPermits, now, limit, cancellationToken));
        criticalItems.AddRange(await BuildOutageCriticalItemsAsync(outages, now, limit, cancellationToken));
        criticalItems.AddRange(await BuildSmartMeterCriticalItemsAsync(smartDevices, ingestions, limit, cancellationToken));

        var orderedCriticalItems = criticalItems
            .OrderByDescending(item => ToSeverityWeight(item.Severity))
            .ThenBy(item => item.DueAtUtc ?? DateTime.MaxValue)
            .ThenBy(item => item.CreatedAtUtc)
            .Take(limit)
            .ToArray();

        var totalCritical = domains.Sum(item => item.CriticalItemCount);
        var totalAttention = domains.Sum(item => item.AttentionItemCount);
        var overallHealth = Math.Clamp(100 - Math.Min(85, totalCritical * 6 + totalAttention * 2), 0, 100);
        var response = new AdminCommandCenterIntelligenceResponse(
            query.CompoundId,
            overallHealth,
            ToHealthStatus(overallHealth),
            totalCritical,
            totalAttention,
            domains,
            orderedCriticalItems,
            now);

        return ServiceResult<AdminCommandCenterIntelligenceResponse>.Success(response);
    }

    public async Task<ServiceResult<ExecutiveDailySummaryResponse>> GetExecutiveDailySummaryAsync(
        ExecutiveIntelligenceQuery query,
        CancellationToken cancellationToken = default)
    {
        var bundleResult = await BuildExecutiveIntelligenceAsync(query, cancellationToken);
        if (!bundleResult.IsSuccess)
        {
            return ServiceResult<ExecutiveDailySummaryResponse>.NotFound("Executive daily summary was not found.");
        }

        var bundle = bundleResult.Value!;
        var response = new ExecutiveDailySummaryResponse(
            query.CompoundId,
            bundle.ExecutiveScore,
            bundle.ExecutiveStatus,
            bundle.DomainSignals.Sum(item => item.CriticalCount),
            bundle.DomainSignals.Sum(item => item.AttentionCount),
            bundle.DomainSignals,
            bundle.CriticalActions,
            BuildExecutiveDecisionBriefs(bundle.DomainSignals, bundle.CriticalActions),
            bundle.GeneratedAtUtc);

        return ServiceResult<ExecutiveDailySummaryResponse>.Success(response);
    }

    public async Task<ServiceResult<DomainSignalBoardResponse>> GetDomainSignalBoardAsync(
        ExecutiveIntelligenceQuery query,
        CancellationToken cancellationToken = default)
    {
        var bundleResult = await BuildExecutiveIntelligenceAsync(query, cancellationToken);
        if (!bundleResult.IsSuccess)
        {
            return ServiceResult<DomainSignalBoardResponse>.NotFound("Executive domain signal board was not found.");
        }

        var bundle = bundleResult.Value!;
        return ServiceResult<DomainSignalBoardResponse>.Success(new DomainSignalBoardResponse(
            query.CompoundId,
            bundle.ExecutiveScore,
            bundle.ExecutiveStatus,
            bundle.DomainSignals,
            bundle.GeneratedAtUtc));
    }

    public async Task<ServiceResult<CriticalActionQueueResponse>> GetCriticalActionQueueAsync(
        ExecutiveIntelligenceQuery query,
        CancellationToken cancellationToken = default)
    {
        var bundleResult = await BuildExecutiveIntelligenceAsync(query, cancellationToken);
        if (!bundleResult.IsSuccess)
        {
            return ServiceResult<CriticalActionQueueResponse>.NotFound("Executive critical action queue was not found.");
        }

        var bundle = bundleResult.Value!;
        return ServiceResult<CriticalActionQueueResponse>.Success(new CriticalActionQueueResponse(
            query.CompoundId,
            bundle.CriticalActions.Count,
            bundle.CriticalActions,
            bundle.GeneratedAtUtc));
    }

    public async Task<ServiceResult<OperationalCommandCenterResponse>> GetCommandCenterAsync(
        OperationalCommandCenterQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await GetValidatedScopeAsync(query.CompoundId, cancellationToken);
        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<OperationalCommandCenterResponse>.NotFound("Operational command center was not found.");
        }

        var scope = scopeResult.Value!;
        var now = DateTime.UtcNow;

        var maintenanceQuery = ApplyOptionalCompoundFilter(
            dbContext.MaintenanceRequests.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var complaintQuery = ApplyOptionalCompoundFilter(
            dbContext.Complaints.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var workOrderQuery = ApplyOptionalCompoundFilter(
            dbContext.WorkOrders.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var approvalQuery = ApplyOptionalCompoundFilter(
            dbContext.ApprovalRequests.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var adjustmentQuery = ApplyOptionalCompoundFilter(
            dbContext.FinancialAdjustments.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var riskFlagQuery = ApplyOptionalCompoundFilter(
            dbContext.ResidentRiskFlags.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var taskQuery = ApplyOptionalCompoundFilter(
            dbContext.OperationalTasks.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);

        var openMaintenanceCount = await maintenanceQuery.CountAsync(item => OpenMaintenanceStatuses.Contains(item.Status), cancellationToken);
        var emergencyMaintenanceCount = await maintenanceQuery.CountAsync(item =>
            OpenMaintenanceStatuses.Contains(item.Status) && item.Priority == MaintenancePriority.Emergency,
            cancellationToken);
        var openComplaintCount = await complaintQuery.CountAsync(item => OpenComplaintStatuses.Contains(item.Status), cancellationToken);
        var criticalComplaintCount = await complaintQuery.CountAsync(item =>
            OpenComplaintStatuses.Contains(item.Status)
            && (item.CreatedAt <= now.AddHours(-48) || item.Title.Contains("urgent") || item.Title.Contains("critical")),
            cancellationToken);
        var openWorkOrderCount = await workOrderQuery.CountAsync(item => OpenWorkOrderStatuses.Contains(item.Status), cancellationToken);
        var overdueWorkOrderCount = await workOrderQuery.CountAsync(item =>
            OpenWorkOrderStatuses.Contains(item.Status)
            && item.DueAtUtc.HasValue
            && item.DueAtUtc.Value < now,
            cancellationToken);
        var pendingApprovalCount = await approvalQuery.CountAsync(item => PendingApprovalStatuses.Contains(item.Status), cancellationToken);
        var pendingAdjustmentCount = await adjustmentQuery.CountAsync(item => item.Status == FinancialAdjustmentStatus.PendingApproval, cancellationToken);
        var activeRiskFlagCount = await riskFlagQuery.CountAsync(item => ActiveRiskFlagStatuses.Contains(item.Status), cancellationToken);
        var criticalRiskFlagCount = await riskFlagQuery.CountAsync(item =>
            ActiveRiskFlagStatuses.Contains(item.Status) && item.Severity == ResidentRiskFlagSeverity.Critical,
            cancellationToken);
        var overdueRiskReviewCount = await riskFlagQuery.CountAsync(item =>
            ActiveRiskFlagStatuses.Contains(item.Status)
            && item.NextReviewAtUtc.HasValue
            && item.NextReviewAtUtc.Value < now,
            cancellationToken);
        var openTaskCount = await taskQuery.CountAsync(item =>
            item.Status == OperationalTaskStatus.Open || item.Status == OperationalTaskStatus.InProgress,
            cancellationToken);
        var overdueTaskCount = await taskQuery.CountAsync(item =>
            (item.Status == OperationalTaskStatus.Open || item.Status == OperationalTaskStatus.InProgress)
            && item.DueAtUtc.HasValue
            && item.DueAtUtc.Value < now,
            cancellationToken);

        var breaches = await BuildSlaBreachesAsync(scope, query.CompoundId, now, cancellationToken);
        var priorityItems = breaches
            .OrderByDescending(item => item.BreachHours)
            .ThenByDescending(item => item.AgeHours)
            .Take(8)
            .Select(item => new OperationalPriorityItemResponse(
                item.SourceType,
                item.SourceId,
                item.CompoundId,
                item.Title,
                item.SeverityLabel,
                item.CreatedAtUtc,
                item.DueAtUtc,
                item.AgeHours,
                item.Recommendation))
            .ToArray();

        var healthScore = CalculateHealthScore(
            openMaintenanceCount,
            openComplaintCount,
            openWorkOrderCount,
            breaches.Count,
            criticalRiskFlagCount,
            overdueRiskReviewCount,
            pendingApprovalCount + pendingAdjustmentCount,
            overdueTaskCount);

        var response = new OperationalCommandCenterResponse(
            query.CompoundId,
            openMaintenanceCount,
            emergencyMaintenanceCount,
            openComplaintCount,
            criticalComplaintCount,
            openWorkOrderCount,
            overdueWorkOrderCount,
            pendingApprovalCount,
            pendingAdjustmentCount,
            activeRiskFlagCount,
            criticalRiskFlagCount,
            overdueRiskReviewCount,
            openTaskCount,
            overdueTaskCount,
            breaches.Count,
            healthScore,
            priorityItems,
            now);

        return ServiceResult<OperationalCommandCenterResponse>.Success(response);
    }

    public async Task<ServiceResult<PagedResult<SlaBreachResponse>>> GetSlaBreachesAsync(
        SlaBreachQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await GetValidatedScopeAsync(query.CompoundId, cancellationToken);
        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<PagedResult<SlaBreachResponse>>.NotFound("SLA breaches were not found.");
        }

        var rows = await BuildSlaBreachesAsync(scopeResult.Value!, query.CompoundId, DateTime.UtcNow, cancellationToken);
        var ordered = rows
            .OrderByDescending(item => item.BreachHours)
            .ThenByDescending(item => item.AgeHours)
            .ToArray();
        var items = ordered
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new SlaBreachResponse(
                item.SourceType,
                item.SourceId,
                item.CompoundId,
                item.Title,
                item.SeverityLabel,
                item.CreatedAtUtc,
                item.DueAtUtc,
                item.AgeHours,
                item.BreachHours,
                item.Recommendation))
            .ToArray();

        return ServiceResult<PagedResult<SlaBreachResponse>>.Success(
            new PagedResult<SlaBreachResponse>(items, query.PageNumber, query.PageSize, ordered.Length));
    }

    public async Task<ServiceResult<StaffPerformanceResponse>> GetStaffPerformanceAsync(
        StaffPerformanceQuery query,
        CancellationToken cancellationToken = default)
    {
        var dateRange = NormalizeDateRange(query.FromUtc, query.ToUtc);
        if (dateRange.Error is not null)
        {
            return ServiceResult<StaffPerformanceResponse>.BadRequest(dateRange.Error);
        }

        var scopeResult = await GetValidatedScopeAsync(query.CompoundId, cancellationToken);
        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<StaffPerformanceResponse>.NotFound("Staff performance was not found.");
        }

        var scope = scopeResult.Value!;
        var workOrders = await ApplyOptionalCompoundFilter(
                dbContext.WorkOrders.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
                query.CompoundId)
            .Where(item => item.AssignedStaffMemberId.HasValue)
            .Where(item => item.CreatedAtUtc >= dateRange.FromUtc && item.CreatedAtUtc <= dateRange.ToUtc)
            .Select(item => new
            {
                item.Id,
                item.CompoundId,
                item.AssignedStaffMemberId,
                item.Status,
                item.DueAtUtc,
                item.CompletedAtUtc,
                item.ActualCost
            })
            .ToListAsync(cancellationToken);

        var staffIds = workOrders
            .Select(item => item.AssignedStaffMemberId!.Value)
            .Distinct()
            .ToArray();
        var staffMembers = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(staff => staffIds.Contains(staff.Id))
            .Select(staff => new { staff.Id, staff.FullName })
            .ToListAsync(cancellationToken);
        var workOrderIds = workOrders.Select(item => item.Id).ToArray();
        var ratings = await dbContext.WorkOrderRatings
            .AsNoTracking()
            .Where(rating => workOrderIds.Contains(rating.WorkOrderId))
            .Select(rating => new { rating.WorkOrderId, rating.Rating })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var rows = workOrders
            .GroupBy(item => item.AssignedStaffMemberId!.Value)
            .Select(group =>
            {
                var staff = staffMembers.FirstOrDefault(item => item.Id == group.Key);
                var workOrderIds = group.Select(item => item.Id).ToHashSet();
                var staffRatings = ratings.Where(rating => workOrderIds.Contains(rating.WorkOrderId)).Select(rating => rating.Rating).ToArray();
                return new StaffWorkloadResponse(
                    group.Key,
                    staff?.FullName ?? "Unknown staff member",
                    group.First().CompoundId,
                    group.Count(item => OpenWorkOrderStatuses.Contains(item.Status)),
                    group.Count(item => item.Status == WorkOrderStatus.Completed),
                    group.Count(item => OpenWorkOrderStatuses.Contains(item.Status) && item.DueAtUtc.HasValue && item.DueAtUtc.Value < now),
                    staffRatings.Length == 0 ? null : Math.Round((decimal)staffRatings.Average(), 2),
                    group.Sum(item => item.ActualCost ?? 0m));
            })
            .OrderByDescending(item => item.OverdueWorkOrderCount)
            .ThenByDescending(item => item.AssignedWorkOrderCount)
            .ToArray();

        return ServiceResult<StaffPerformanceResponse>.Success(new StaffPerformanceResponse(
            query.CompoundId,
            dateRange.FromUtc,
            dateRange.ToUtc,
            rows,
            DateTime.UtcNow));
    }

    public async Task<ServiceResult<CompoundHealthResponse>> GetCompoundHealthAsync(
        CompoundHealthQuery query,
        CancellationToken cancellationToken = default)
    {
        var center = await GetCommandCenterAsync(new OperationalCommandCenterQuery { CompoundId = query.CompoundId }, cancellationToken);
        if (!center.IsSuccess)
        {
            return ServiceResult<CompoundHealthResponse>.NotFound("Compound health was not found.");
        }

        var outstandingItems = await CountOverdueFinancialItemsAsync(center.Value!.CompoundId, cancellationToken);
        var factors = BuildHealthFactors(center.Value, outstandingItems);
        var score = Math.Clamp(center.Value.CompoundHealthScore - Math.Min(20, outstandingItems * 2), 0, 100);

        return ServiceResult<CompoundHealthResponse>.Success(new CompoundHealthResponse(
            query.CompoundId,
            score,
            score >= 85 ? "Healthy" : score >= 65 ? "Watch" : score >= 45 ? "AtRisk" : "Critical",
            center.Value.OpenMaintenanceRequestCount,
            center.Value.OpenComplaintCount,
            center.Value.OpenWorkOrderCount,
            center.Value.SlaBreachCount,
            center.Value.CriticalRiskFlagCount,
            outstandingItems,
            center.Value.PendingApprovalRequestCount,
            factors,
            DateTime.UtcNow));
    }

    public async Task<ServiceResult<PagedResult<OperationalTaskResponse>>> SearchTasksAsync(
        OperationalTaskSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await GetValidatedScopeAsync(query.CompoundId, cancellationToken);
        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<PagedResult<OperationalTaskResponse>>.NotFound("Operational tasks were not found.");
        }

        var now = DateTime.UtcNow;
        var tasks = ApplyTaskFilters(
            ApplyOptionalCompoundFilter(
                dbContext.OperationalTasks.AsNoTracking().ApplyCompoundAccess(scopeResult.Value!, item => item.CompoundId),
                query.CompoundId),
            query,
            now);
        var totalCount = await tasks.CountAsync(cancellationToken);
        var items = await tasks
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.DueAtUtc ?? DateTime.MaxValue)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => ToTaskResponse(item, now))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<OperationalTaskResponse>>.Success(
            new PagedResult<OperationalTaskResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<OperationalTaskResponse>> GetTaskAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return ServiceResult<OperationalTaskResponse>.BadRequest("Operational task id is required.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<OperationalTaskResponse>.Forbidden("Current user cannot access operational tasks.");
        }

        var task = await dbContext.OperationalTasks
            .AsNoTracking()
            .ApplyCompoundAccess(scope, item => item.CompoundId)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (task is null)
        {
            return ServiceResult<OperationalTaskResponse>.NotFound("Operational task was not found.");
        }

        return ServiceResult<OperationalTaskResponse>.Success(ToTaskResponse(task, DateTime.UtcNow));
    }

    public async Task<ServiceResult<OperationalTaskResponse>> CreateTaskAsync(
        Guid? currentUserId,
        CreateOperationalTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<OperationalTaskResponse>.Forbidden("Current user is required.");
        }

        var validation = ValidateCreateTaskRequest(request);
        if (validation is not null)
        {
            return ServiceResult<OperationalTaskResponse>.BadRequest(validation);
        }

        if (!await compoundAccessService.CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<OperationalTaskResponse>.NotFound("Operational task target compound was not found.");
        }

        var assignmentValidation = await ValidateAssignedUserScopeAsync(
            request.CompoundId,
            request.AssignedToUserId,
            cancellationToken);
        if (assignmentValidation is not null)
        {
            return ServiceResult<OperationalTaskResponse>.BadRequest(assignmentValidation);
        }

        var now = DateTime.UtcNow;
        var task = new OperationalTask
        {
            CompoundId = request.CompoundId,
            TaskType = request.TaskType,
            Priority = request.Priority,
            Status = OperationalTaskStatus.Open,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            RelatedEntityType = request.RelatedEntityType,
            RelatedEntityId = request.RelatedEntityId,
            AssignedToUserId = request.AssignedToUserId,
            CreatedByUserId = currentUserId.Value,
            DueAtUtc = request.DueAtUtc,
            CreatedAtUtc = now
        };

        dbContext.OperationalTasks.Add(task);
        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            request.CompoundId,
            null,
            currentUserId.Value,
            null,
            AuditActionType.OperationalTaskCreated,
            AuditEntityType.OperationalTask,
            task.Id,
            task.Priority == OperationalTaskPriority.Critical ? AuditSeverity.High : AuditSeverity.Medium,
            "Operations",
            $"Operational task '{task.Title}' was created.",
            task.Description,
            MetadataJson: $"{{\"taskType\":\"{task.TaskType}\",\"priority\":\"{task.Priority}\"}}"),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<OperationalTaskResponse>.Success(ToTaskResponse(task, now));
    }

    public async Task<ServiceResult<OperationalTaskResponse>> CompleteTaskAsync(
        Guid? currentUserId,
        Guid id,
        CompleteOperationalTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<OperationalTaskResponse>.Forbidden("Current user is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CompletionNotes))
        {
            return ServiceResult<OperationalTaskResponse>.BadRequest("Completion notes are required.");
        }

        var task = await dbContext.OperationalTasks.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (task is null || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(task.CompoundId, cancellationToken))
        {
            return ServiceResult<OperationalTaskResponse>.NotFound("Operational task was not found.");
        }

        if (task.Status is OperationalTaskStatus.Completed or OperationalTaskStatus.Cancelled)
        {
            return ServiceResult<OperationalTaskResponse>.Conflict("Only open or in-progress operational tasks can be completed.");
        }

        var now = DateTime.UtcNow;
        task.Status = OperationalTaskStatus.Completed;
        task.CompletedByUserId = currentUserId.Value;
        task.CompletedAtUtc = now;
        task.UpdatedAtUtc = now;
        task.CompletionNotes = request.CompletionNotes.Trim();

        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            task.CompoundId,
            null,
            currentUserId.Value,
            null,
            AuditActionType.OperationalTaskCompleted,
            AuditEntityType.OperationalTask,
            task.Id,
            AuditSeverity.Medium,
            "Operations",
            $"Operational task '{task.Title}' was completed.",
            request.CompletionNotes,
            Changes:
            [
                new AuditLogChangeRecord("Status", OperationalTaskStatus.Open.ToString(), OperationalTaskStatus.Completed.ToString())
            ]),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<OperationalTaskResponse>.Success(ToTaskResponse(task, now));
    }

    public async Task<ServiceResult<OperationalTaskResponse>> CancelTaskAsync(
        Guid? currentUserId,
        Guid id,
        CancelOperationalTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<OperationalTaskResponse>.Forbidden("Current user is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult<OperationalTaskResponse>.BadRequest("Cancellation reason is required.");
        }

        var task = await dbContext.OperationalTasks.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (task is null || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(task.CompoundId, cancellationToken))
        {
            return ServiceResult<OperationalTaskResponse>.NotFound("Operational task was not found.");
        }

        if (task.Status is OperationalTaskStatus.Completed or OperationalTaskStatus.Cancelled)
        {
            return ServiceResult<OperationalTaskResponse>.Conflict("Only open or in-progress operational tasks can be cancelled.");
        }

        var now = DateTime.UtcNow;
        task.Status = OperationalTaskStatus.Cancelled;
        task.CancelledByUserId = currentUserId.Value;
        task.CancelledAtUtc = now;
        task.UpdatedAtUtc = now;
        task.CancellationReason = request.Reason.Trim();

        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            task.CompoundId,
            null,
            currentUserId.Value,
            null,
            AuditActionType.OperationalTaskCancelled,
            AuditEntityType.OperationalTask,
            task.Id,
            AuditSeverity.Medium,
            "Operations",
            $"Operational task '{task.Title}' was cancelled.",
            request.Reason,
            Changes:
            [
                new AuditLogChangeRecord("Status", OperationalTaskStatus.Open.ToString(), OperationalTaskStatus.Cancelled.ToString())
            ]),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<OperationalTaskResponse>.Success(ToTaskResponse(task, now));
    }

    private async Task<List<SlaBreachRow>> BuildSlaBreachesAsync(
        CompoundAccessScope scope,
        Guid? compoundId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var breaches = new List<SlaBreachRow>();

        var maintenanceRows = await ApplyOptionalCompoundFilter(
                dbContext.MaintenanceRequests.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
                compoundId)
            .Where(item => OpenMaintenanceStatuses.Contains(item.Status))
            .Select(item => new
            {
                item.Id,
                item.CompoundId,
                item.Title,
                item.Priority,
                item.CreatedAt,
                item.AssignedAt
            })
            .ToListAsync(cancellationToken);
        foreach (var item in maintenanceRows)
        {
            var thresholdHours = item.Priority switch
            {
                MaintenancePriority.Emergency => 4,
                MaintenancePriority.High => 24,
                MaintenancePriority.Medium => 72,
                _ => 120
            };
            var ageHours = ToAgeHours(item.CreatedAt, now);
            if (ageHours > thresholdHours)
            {
                breaches.Add(new SlaBreachRow(
                    "MaintenanceRequest",
                    item.Id,
                    item.CompoundId,
                    item.Title,
                    item.Priority.ToString(),
                    item.CreatedAt,
                    item.CreatedAt.AddHours(thresholdHours),
                    ageHours,
                    ageHours - thresholdHours,
                    item.AssignedAt.HasValue ? "Follow up with assigned maintenance staff." : "Assign maintenance staff immediately."));
            }
        }

        var complaintRows = await ApplyOptionalCompoundFilter(
                dbContext.Complaints.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
                compoundId)
            .Where(item => OpenComplaintStatuses.Contains(item.Status))
            .Select(item => new
            {
                item.Id,
                item.CompoundId,
                item.Title,
                item.CreatedAt
            })
            .ToListAsync(cancellationToken);
        foreach (var item in complaintRows)
        {
            const int thresholdHours = 48;
            var ageHours = ToAgeHours(item.CreatedAt, now);
            if (ageHours > thresholdHours)
            {
                breaches.Add(new SlaBreachRow(
                    "Complaint",
                    item.Id,
                    item.CompoundId,
                    item.Title,
                    "High",
                    item.CreatedAt,
                    item.CreatedAt.AddHours(thresholdHours),
                    ageHours,
                    ageHours - thresholdHours,
                    "Escalate complaint review and contact the resident."));
            }
        }

        var workOrderRows = await ApplyOptionalCompoundFilter(
                dbContext.WorkOrders.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
                compoundId)
            .Where(item => OpenWorkOrderStatuses.Contains(item.Status))
            .Select(item => new
            {
                item.Id,
                item.CompoundId,
                item.Title,
                item.Priority,
                item.CreatedAtUtc,
                item.DueAtUtc,
                item.AssignedStaffMemberId,
                item.AssignedVendorId
            })
            .ToListAsync(cancellationToken);
        foreach (var item in workOrderRows)
        {
            var dueAt = item.DueAtUtc ?? item.CreatedAtUtc.AddHours(item.Priority switch
            {
                WorkOrderPriority.Emergency => 6,
                WorkOrderPriority.Urgent => 12,
                WorkOrderPriority.High => 48,
                _ => 96
            });
            if (dueAt < now)
            {
                var ageHours = ToAgeHours(item.CreatedAtUtc, now);
                breaches.Add(new SlaBreachRow(
                    "WorkOrder",
                    item.Id,
                    item.CompoundId,
                    item.Title,
                    item.Priority.ToString(),
                    item.CreatedAtUtc,
                    dueAt,
                    ageHours,
                    ToAgeHours(dueAt, now),
                    item.AssignedStaffMemberId.HasValue || item.AssignedVendorId.HasValue
                        ? "Escalate the assigned resource and update due date."
                        : "Assign staff or vendor immediately."));
            }
        }

        var riskRows = await ApplyOptionalCompoundFilter(
                dbContext.ResidentRiskFlags.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
                compoundId)
            .Where(item => ActiveRiskFlagStatuses.Contains(item.Status) && item.NextReviewAtUtc.HasValue && item.NextReviewAtUtc.Value < now)
            .Select(item => new
            {
                item.Id,
                item.CompoundId,
                item.Title,
                item.Severity,
                item.CreatedAtUtc,
                item.NextReviewAtUtc
            })
            .ToListAsync(cancellationToken);
        foreach (var item in riskRows)
        {
            breaches.Add(new SlaBreachRow(
                "ResidentRiskFlag",
                item.Id,
                item.CompoundId,
                item.Title,
                item.Severity.ToString(),
                item.CreatedAtUtc,
                item.NextReviewAtUtc,
                ToAgeHours(item.CreatedAtUtc, now),
                ToAgeHours(item.NextReviewAtUtc!.Value, now),
                "Review resident risk flag and decide whether to resolve, dismiss, or continue monitoring."));
        }

        return breaches;
    }

    private async Task<int> CountOverdueFinancialItemsAsync(Guid? compoundId, CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return 0;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var utilityCount = await ApplyOptionalCompoundFilter(
                dbContext.UtilityBills.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
                compoundId)
            .CountAsync(item => item.DueDate < today && item.PaidAmount < item.TotalAmount && item.BillStatus != BillStatus.Cancelled, cancellationToken);
        var rentCount = await ApplyOptionalCompoundFilter(
                dbContext.RentInvoices.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
                compoundId)
            .CountAsync(item => item.DueDate < today && item.PaidAmount < item.TotalAmount && item.RentInvoiceStatus != RentInvoiceStatus.Cancelled, cancellationToken);
        var installmentCount = await ApplyOptionalCompoundFilter(
                dbContext.InstallmentScheduleItems.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
                compoundId)
            .CountAsync(item => item.DueDate < today && item.PaidAmount < item.Amount && item.InstallmentStatus != InstallmentStatus.Cancelled, cancellationToken);

        return utilityCount + rentCount + installmentCount;
    }


    private static CommandCenterDomainCardResponse BuildDomain(
        string domain,
        string label,
        int openItems,
        int criticalItems,
        int attentionItems,
        string recommendation)
    {
        var healthScore = Math.Clamp(100 - Math.Min(90, criticalItems * 8 + attentionItems * 3), 0, 100);
        return new CommandCenterDomainCardResponse(
            domain,
            label,
            openItems,
            criticalItems,
            attentionItems,
            healthScore,
            recommendation);
    }

    private static string ToHealthStatus(int score)
    {
        if (score >= 85)
        {
            return "Healthy";
        }

        if (score >= 70)
        {
            return "Watch";
        }

        if (score >= 50)
        {
            return "AtRisk";
        }

        return "Critical";
    }

    private static int ToSeverityWeight(string severity)
    {
        return severity switch
        {
            "Critical" => 4,
            "High" => 3,
            "Medium" => 2,
            _ => 1
        };
    }

    private static async Task<IReadOnlyCollection<CommandCenterCriticalItemResponse>> BuildFinanceCriticalItemsAsync(
        IQueryable<FinancialDispute> disputes,
        IQueryable<CollectionCase> collectionCases,
        IQueryable<LegalNotice> legalNotices,
        DateTime now,
        int limit,
        CancellationToken cancellationToken)
    {
        var items = new List<CommandCenterCriticalItemResponse>();

        var oldDisputes = await disputes
            .Where(item => item.Status == FinancialDisputeStatus.Open || item.Status == FinancialDisputeStatus.UnderReview || item.Status == FinancialDisputeStatus.NeedResidentResponse)
            .OrderBy(item => item.CreatedAtUtc)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.TargetType, item.Reason, item.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        items.AddRange(oldDisputes.Select(item => new CommandCenterCriticalItemResponse(
            "Finance",
            "FinancialDispute",
            item.Id,
            item.CompoundId,
            string.IsNullOrWhiteSpace(item.Reason) ? $"Open financial dispute for {item.TargetType}" : item.Reason,
            item.CreatedAtUtc <= now.AddDays(-2) ? "High" : "Medium",
            "Review the dispute and either request resident input, accept, or reject it.",
            item.CreatedAtUtc,
            null)));

        var openCollections = await collectionCases
            .Where(item => item.Status == CollectionCaseStatus.Open || item.Status == CollectionCaseStatus.LegalEscalated || item.Status == CollectionCaseStatus.PaymentPlanActive)
            .OrderByDescending(item => item.AmountDue)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.AmountDue, item.Reason, item.DueDate, item.OpenedAtUtc })
            .ToListAsync(cancellationToken);

        items.AddRange(openCollections.Select(item => new CommandCenterCriticalItemResponse(
            "Finance",
            "CollectionCase",
            item.Id,
            item.CompoundId,
            string.IsNullOrWhiteSpace(item.Reason) ? $"Open collection case: {item.AmountDue:N0}" : item.Reason,
            item.DueDate.HasValue && item.DueDate.Value < DateOnly.FromDateTime(now) ? "Critical" : "High",
            "Follow up collection stage, payment plan, or legal escalation.",
            item.OpenedAtUtc,
            item.DueDate?.ToDateTime(TimeOnly.MinValue))));

        var issuedNotices = await legalNotices
            .Where(item => item.Status == LegalNoticeStatus.Issued)
            .OrderBy(item => item.DeadlineDate)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.Title, item.DeadlineDate, item.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        items.AddRange(issuedNotices.Select(item => new CommandCenterCriticalItemResponse(
            "Finance",
            "LegalNotice",
            item.Id,
            item.CompoundId,
            string.IsNullOrWhiteSpace(item.Title) ? "Issued legal notice" : item.Title,
            item.DeadlineDate.HasValue && item.DeadlineDate.Value < DateOnly.FromDateTime(now) ? "Critical" : "High",
            "Track delivery, acknowledgement, or next legal step.",
            item.CreatedAtUtc,
            item.DeadlineDate?.ToDateTime(TimeOnly.MinValue))));

        return items;
    }

    private static async Task<IReadOnlyCollection<CommandCenterCriticalItemResponse>> BuildMaintenanceCriticalItemsAsync(
        IQueryable<WorkOrder> workOrders,
        DateTime now,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await workOrders
            .Where(item => item.SlaStatus == MaintenanceSlaStatus.ResponseBreached || item.SlaStatus == MaintenanceSlaStatus.ResolutionBreached || item.SlaStatus == MaintenanceSlaStatus.Escalated)
            .OrderBy(item => item.ResolutionDueAtUtc ?? item.ResponseDueAtUtc ?? item.DueAtUtc ?? item.CreatedAtUtc)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.Title, item.SlaStatus, item.CreatedAtUtc, item.ResolutionDueAtUtc, item.ResponseDueAtUtc, item.DueAtUtc })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new CommandCenterCriticalItemResponse(
            "Maintenance",
            "WorkOrder",
            item.Id,
            item.CompoundId,
            string.IsNullOrWhiteSpace(item.Title) ? "SLA-breached work order" : item.Title,
            item.SlaStatus == MaintenanceSlaStatus.Escalated ? "Critical" : "High",
            "Escalate owner, update SLA status, and resolve the work order.",
            item.CreatedAtUtc,
            item.ResolutionDueAtUtc ?? item.ResponseDueAtUtc ?? item.DueAtUtc)).ToArray();
    }

    private static async Task<IReadOnlyCollection<CommandCenterCriticalItemResponse>> BuildLifecycleCriticalItemsAsync(
        IQueryable<ResidentLifecycleProcess> lifecycleProcesses,
        DateTime now,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await lifecycleProcesses
            .Where(item => item.Status == ResidentLifecycleStatus.PendingFinancialClearance || item.Status == ResidentLifecycleStatus.PendingCustodyClearance || item.Status == ResidentLifecycleStatus.PendingUnitReadiness)
            .OrderBy(item => item.TargetDate)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.ProcessType, item.Status, item.TargetDate, item.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new CommandCenterCriticalItemResponse(
            "Lifecycle",
            "ResidentLifecycleProcess",
            item.Id,
            item.CompoundId,
            $"{item.ProcessType} blocked by {item.Status}",
            item.TargetDate < DateOnly.FromDateTime(now) ? "High" : "Medium",
            "Clear financial, custody, or unit-readiness gate.",
            item.CreatedAtUtc,
            item.TargetDate.ToDateTime(TimeOnly.MinValue))).ToArray();
    }

    private static async Task<IReadOnlyCollection<CommandCenterCriticalItemResponse>> BuildAccessCriticalItemsAsync(
        IQueryable<ContractorWorkPermit> contractorPermits,
        DateTime now,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await contractorPermits
            .Where(item => item.Status == ContractorWorkPermitStatus.PendingApproval || item.Status == ContractorWorkPermitStatus.CheckedIn)
            .OrderBy(item => item.AllowedUntilUtc)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.Purpose, item.Status, item.CreatedAtUtc, item.AllowedUntilUtc })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new CommandCenterCriticalItemResponse(
            "Access",
            "ContractorWorkPermit",
            item.Id,
            item.CompoundId,
            string.IsNullOrWhiteSpace(item.Purpose) ? $"Contractor permit {item.Status}" : item.Purpose,
            item.Status == ContractorWorkPermitStatus.CheckedIn && item.AllowedUntilUtc < now ? "Critical" : "High",
            "Approve, deny, check out, or close contractor access.",
            item.CreatedAtUtc,
            item.AllowedUntilUtc)).ToArray();
    }

    private static async Task<IReadOnlyCollection<CommandCenterCriticalItemResponse>> BuildOutageCriticalItemsAsync(
        IQueryable<UtilityOutage> outages,
        DateTime now,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await outages
            .Where(item => item.Status == UtilityOutageStatus.Planned || item.Status == UtilityOutageStatus.Active)
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.EstimatedEndAtUtc ?? item.EstimatedStartAtUtc)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.Title, item.Severity, item.Status, item.CreatedAtUtc, item.EstimatedEndAtUtc })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new CommandCenterCriticalItemResponse(
            "Communications",
            "UtilityOutage",
            item.Id,
            item.CompoundId,
            string.IsNullOrWhiteSpace(item.Title) ? $"{item.Status} utility outage" : item.Title,
            item.Severity == UtilityOutageSeverity.Critical ? "Critical" : item.Severity == UtilityOutageSeverity.High ? "High" : "Medium",
            "Publish resident updates and resolve the outage when service is restored.",
            item.CreatedAtUtc,
            item.EstimatedEndAtUtc)).ToArray();
    }

    private static async Task<IReadOnlyCollection<CommandCenterCriticalItemResponse>> BuildSmartMeterCriticalItemsAsync(
        IQueryable<SmartMeterDevice> devices,
        IQueryable<SmartMeterReadingIngestion> ingestions,
        int limit,
        CancellationToken cancellationToken)
    {
        var items = new List<CommandCenterCriticalItemResponse>();
        var offlineDevices = await devices
            .Where(item => item.HealthStatus == SmartMeterDeviceHealthStatus.Offline)
            .OrderBy(item => item.LastSeenAtUtc ?? item.CreatedAtUtc)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.DeviceIdentifier, item.CreatedAtUtc, item.LastSeenAtUtc })
            .ToListAsync(cancellationToken);

        items.AddRange(offlineDevices.Select(item => new CommandCenterCriticalItemResponse(
            "SmartMeters",
            "SmartMeterDevice",
            item.Id,
            item.CompoundId,
            $"Offline smart meter device: {item.DeviceIdentifier}",
            "Critical",
            "Inspect device connectivity before accepting new automated readings.",
            item.LastSeenAtUtc ?? item.CreatedAtUtc,
            null)));

        var suspicious = await ingestions
            .Where(item => item.Status == SmartMeterReadingIngestionStatus.Suspicious || item.BillingHoldRecommended)
            .OrderBy(item => item.CreatedAtUtc)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.MeterId, item.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        items.AddRange(suspicious.Select(item => new CommandCenterCriticalItemResponse(
            "SmartMeters",
            "SmartMeterReadingIngestion",
            item.Id,
            item.CompoundId,
            $"Smart meter reading needs billing review: {item.MeterId}",
            "High",
            "Review anomaly and release or hold billing impact.",
            item.CreatedAtUtc,
            null)));

        return items;
    }

    private async Task<ServiceResult<ExecutiveIntelligenceBundle>> BuildExecutiveIntelligenceAsync(
        ExecutiveIntelligenceQuery query,
        CancellationToken cancellationToken)
    {
        var scopeResult = await GetValidatedScopeAsync(query.CompoundId, cancellationToken);
        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<ExecutiveIntelligenceBundle>.NotFound("Executive intelligence was not found.");
        }

        var scope = scopeResult.Value!;
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var noticeWindowEnd = today.AddDays(3);
        var limit = Math.Clamp(query.ItemLimit, 1, 100);

        var collectionCases = ApplyOptionalCompoundFilter(
            dbContext.CollectionCases.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var legalNotices = ApplyOptionalCompoundFilter(
            dbContext.LegalNotices.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var disputes = ApplyOptionalCompoundFilter(
            dbContext.FinancialDisputes.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var financialAdjustments = ApplyOptionalCompoundFilter(
            dbContext.FinancialAdjustments.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var utilityBills = ApplyOptionalCompoundFilter(
            dbContext.UtilityBills.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var rentInvoices = ApplyOptionalCompoundFilter(
            dbContext.RentInvoices.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var installmentItems = ApplyOptionalCompoundFilter(
            dbContext.InstallmentScheduleItems.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var workOrders = ApplyOptionalCompoundFilter(
            dbContext.WorkOrders.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var assets = ApplyOptionalCompoundFilter(
            dbContext.MaintenanceAssets.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var preventivePlans = ApplyOptionalCompoundFilter(
            dbContext.PreventiveMaintenancePlans.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var contractorPermits = ApplyOptionalCompoundFilter(
            dbContext.ContractorWorkPermits.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var accessCredentials = ApplyOptionalCompoundFilter(
            dbContext.AccessCredentials.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var visitorPasses = ApplyOptionalCompoundFilter(
            dbContext.VisitorPasses.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var outages = ApplyOptionalCompoundFilter(
            dbContext.UtilityOutages.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var announcements = ApplyOptionalCompoundFilter(
            dbContext.Announcements.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var conversations = ApplyOptionalCompoundFilter(
            dbContext.Conversations.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var lifecycleProcesses = ApplyOptionalCompoundFilter(
            dbContext.ResidentLifecycleProcesses.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var custodyItems = ApplyOptionalCompoundFilter(
            dbContext.ResidentCustodyItems.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var damageLiabilities = ApplyOptionalCompoundFilter(
            dbContext.UnitDamageLiabilities.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var stockItems = ApplyOptionalCompoundFilter(
            dbContext.StockItems.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var procurementRequests = ApplyOptionalCompoundFilter(
            dbContext.ProcurementRequests.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var purchaseOrders = ApplyOptionalCompoundFilter(
            dbContext.PurchaseOrders.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var riskFlags = ApplyOptionalCompoundFilter(
            dbContext.ResidentRiskFlags.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);
        var operationalTasks = ApplyOptionalCompoundFilter(
            dbContext.OperationalTasks.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId),
            query.CompoundId);

        var overdueUtilityCount = await utilityBills.CountAsync(item => item.DueDate < today && item.PaidAmount < item.TotalAmount && item.BillStatus != BillStatus.Cancelled, cancellationToken);
        var overdueRentCount = await rentInvoices.CountAsync(item => item.DueDate < today && item.PaidAmount < item.TotalAmount && item.RentInvoiceStatus != RentInvoiceStatus.Cancelled, cancellationToken);
        var overdueInstallmentCount = await installmentItems.CountAsync(item => item.DueDate < today && item.PaidAmount < item.Amount && item.InstallmentStatus != InstallmentStatus.Cancelled, cancellationToken);
        var openDisputeCount = await disputes.CountAsync(item => item.Status == FinancialDisputeStatus.Open || item.Status == FinancialDisputeStatus.UnderReview || item.Status == FinancialDisputeStatus.NeedResidentResponse, cancellationToken);
        var pendingAdjustmentCount = await financialAdjustments.CountAsync(item => item.Status == FinancialAdjustmentStatus.PendingApproval, cancellationToken);

        var openCollectionCount = await collectionCases.CountAsync(item => item.Status == CollectionCaseStatus.Open || item.Status == CollectionCaseStatus.Paused || item.Status == CollectionCaseStatus.PaymentPlanActive || item.Status == CollectionCaseStatus.LegalEscalated, cancellationToken);
        var legalEscalatedCount = await collectionCases.CountAsync(item => item.Status == CollectionCaseStatus.LegalEscalated, cancellationToken);
        var noticeDeadlineCount = await legalNotices.CountAsync(item => item.Status == LegalNoticeStatus.Issued && item.DeadlineDate.HasValue && item.DeadlineDate.Value <= noticeWindowEnd, cancellationToken);

        var openWorkOrderCount = await workOrders.CountAsync(item => item.Status == WorkOrderStatus.New || item.Status == WorkOrderStatus.Assigned || item.Status == WorkOrderStatus.Scheduled || item.Status == WorkOrderStatus.InProgress, cancellationToken);
        var slaBreachCount = await workOrders.CountAsync(item => item.SlaStatus == MaintenanceSlaStatus.ResponseBreached || item.SlaStatus == MaintenanceSlaStatus.ResolutionBreached || item.SlaStatus == MaintenanceSlaStatus.Escalated || (item.DueAtUtc.HasValue && item.DueAtUtc.Value < now && item.Status != WorkOrderStatus.Completed && item.Status != WorkOrderStatus.Cancelled), cancellationToken);
        var unavailableAssetCount = await assets.CountAsync(item => item.Status == MaintenanceAssetStatus.OutOfService || item.Status == MaintenanceAssetStatus.UnderMaintenance, cancellationToken);
        var preventiveDueCount = await preventivePlans.CountAsync(item => item.IsActive && item.NextDueAtUtc <= now, cancellationToken);

        var riskyContractorCount = await contractorPermits.CountAsync(item => (item.Status == ContractorWorkPermitStatus.PendingApproval || item.Status == ContractorWorkPermitStatus.Approved || item.Status == ContractorWorkPermitStatus.CheckedIn) && (item.RiskLevel == ContractorWorkPermitRiskLevel.High || item.RiskLevel == ContractorWorkPermitRiskLevel.Critical), cancellationToken);
        var overdueCheckedInContractorCount = await contractorPermits.CountAsync(item => item.Status == ContractorWorkPermitStatus.CheckedIn && item.AllowedUntilUtc < now, cancellationToken);
        var expiredActiveCredentialCount = await accessCredentials.CountAsync(item => item.Status == AccessCredentialStatus.Active && item.ValidUntilUtc.HasValue && item.ValidUntilUtc.Value < now, cancellationToken);
        var pendingVisitorCount = await visitorPasses.CountAsync(item => item.Status == VisitorPassStatus.Pending || item.Status == VisitorPassStatus.Approved, cancellationToken);

        var activeOutageCount = await outages.CountAsync(item => item.Status == UtilityOutageStatus.Planned || item.Status == UtilityOutageStatus.Active, cancellationToken);
        var criticalOutageCount = await outages.CountAsync(item => (item.Status == UtilityOutageStatus.Planned || item.Status == UtilityOutageStatus.Active) && item.Severity == UtilityOutageSeverity.Critical, cancellationToken);
        var expiredCriticalAnnouncementCount = await announcements.CountAsync(item => item.Status == AnnouncementStatus.Published && item.Priority == AnnouncementPriority.Critical && item.ExpiresAt.HasValue && item.ExpiresAt.Value < now, cancellationToken);
        var staleConversationCount = await conversations.CountAsync(item => (item.Status == ConversationStatus.Open || item.Status == ConversationStatus.PendingAdminReply || item.Status == ConversationStatus.Reopened) && item.LastMessageAtUtc < now.AddHours(-24), cancellationToken);

        var openLifecycleCount = await lifecycleProcesses.CountAsync(item => item.Status != ResidentLifecycleStatus.Completed && item.Status != ResidentLifecycleStatus.Cancelled, cancellationToken);
        var custodyIssueCount = await custodyItems.CountAsync(item => item.Status == CustodyItemStatus.Issued || item.Status == CustodyItemStatus.Lost || item.Status == CustodyItemStatus.Damaged, cancellationToken);
        var damageIssueCount = await damageLiabilities.CountAsync(item => item.Status == DamageLiabilityStatus.Draft || item.Status == DamageLiabilityStatus.Charged || item.Status == DamageLiabilityStatus.Disputed, cancellationToken);

        var lowStockCount = await stockItems.CountAsync(item => item.CurrentQuantity <= item.MinimumQuantity, cancellationToken);
        var procurementPendingCount = await procurementRequests.CountAsync(item => item.Status == ProcurementRequestStatus.PendingApproval, cancellationToken);
        var delayedPurchaseOrderCount = await purchaseOrders.CountAsync(item => (item.Status == PurchaseOrderStatus.Ordered || item.Status == PurchaseOrderStatus.PartiallyReceived) && item.ExpectedDeliveryAtUtc.HasValue && item.ExpectedDeliveryAtUtc.Value < now, cancellationToken);

        var criticalRiskCount = await riskFlags.CountAsync(item => (item.Status == ResidentRiskFlagStatus.Active || item.Status == ResidentRiskFlagStatus.Monitoring) && item.Severity == ResidentRiskFlagSeverity.Critical, cancellationToken);
        var highRiskCount = await riskFlags.CountAsync(item => (item.Status == ResidentRiskFlagStatus.Active || item.Status == ResidentRiskFlagStatus.Monitoring) && item.Severity == ResidentRiskFlagSeverity.High, cancellationToken);
        var overdueRiskReviewCount = await riskFlags.CountAsync(item => (item.Status == ResidentRiskFlagStatus.Active || item.Status == ResidentRiskFlagStatus.Monitoring) && item.NextReviewAtUtc.HasValue && item.NextReviewAtUtc.Value < now, cancellationToken);
        var overdueTaskCount = await operationalTasks.CountAsync(item => (item.Status == OperationalTaskStatus.Open || item.Status == OperationalTaskStatus.InProgress) && item.DueAtUtc.HasValue && item.DueAtUtc.Value < now, cancellationToken);

        var domains = new List<ExecutiveDomainSignalResponse>
        {
            BuildExecutiveDomainSignal("Finance", "Financial Exposure", overdueUtilityCount + overdueRentCount + overdueInstallmentCount, openDisputeCount + pendingAdjustmentCount, "Overdue balances, disputes, and pending financial approvals.", "Freeze avoidable adjustments and close the highest exposure residents first."),
            BuildExecutiveDomainSignal("Legal", "Legal Readiness", legalEscalatedCount + noticeDeadlineCount, openCollectionCount, "Collection cases and legal notice deadlines.", "Prioritize issued notices and cases ready for legal escalation."),
            BuildExecutiveDomainSignal("Maintenance", "Maintenance Reliability", slaBreachCount + unavailableAssetCount + preventiveDueCount, openWorkOrderCount, "SLA breaches, unavailable assets, and overdue preventive maintenance.", "Escalate breached work orders and restore assets blocking resident services."),
            BuildExecutiveDomainSignal("Access", "Access & Security Control", riskyContractorCount + overdueCheckedInContractorCount + expiredActiveCredentialCount, pendingVisitorCount, "Contractor risk, expired credentials, and gate approvals.", "Clean expired access, resolve checked-in contractors, and review high-risk permits."),
            BuildExecutiveDomainSignal("Communications", "Communication & Outage Control", criticalOutageCount + expiredCriticalAnnouncementCount, activeOutageCount + staleConversationCount, "Critical outages, stale conversations, and announcement coverage.", "Publish service updates and force admin response on stale conversations."),
            BuildExecutiveDomainSignal("Lifecycle", "Resident Exit & Unit Turnover", damageIssueCount + custodyIssueCount, openLifecycleCount, "Move-out blockers, custody issues, and damage liabilities.", "Clear custody and damage blockers before exit certificate or turnover."),
            BuildExecutiveDomainSignal("Inventory", "Inventory & Procurement Readiness", lowStockCount + delayedPurchaseOrderCount, procurementPendingCount, "Low stock, delayed purchase orders, and pending procurement.", "Approve urgent procurement and replenish parts used by maintenance."),
            BuildExecutiveDomainSignal("ResidentRisk", "Resident Risk & Compliance", criticalRiskCount + overdueRiskReviewCount, highRiskCount + overdueTaskCount, "Critical risk flags, overdue reviews, and overdue operational tasks.", "Assign owners to critical residents and close overdue risk-review tasks.")
        };

        var criticalActions = await BuildExecutiveCriticalActionsAsync(
            collectionCases,
            legalNotices,
            workOrders,
            outages,
            contractorPermits,
            accessCredentials,
            stockItems,
            lifecycleProcesses,
            riskFlags,
            now,
            today,
            limit,
            cancellationToken);

        var totalCritical = domains.Sum(item => item.CriticalCount);
        var totalAttention = domains.Sum(item => item.AttentionCount);
        var executiveScore = Math.Clamp(100 - Math.Min(90, totalCritical * 5 + totalAttention * 2), 0, 100);
        var orderedDomains = domains
            .OrderBy(item => item.Score)
            .ThenByDescending(item => item.CriticalCount)
            .ThenByDescending(item => item.AttentionCount)
            .ToArray();
        var orderedActions = criticalActions
            .OrderByDescending(item => ToSeverityWeight(item.Severity))
            .ThenBy(item => item.DueAtUtc ?? DateTime.MaxValue)
            .ThenBy(item => item.CreatedAtUtc)
            .Take(limit)
            .ToArray();

        return ServiceResult<ExecutiveIntelligenceBundle>.Success(new ExecutiveIntelligenceBundle(
            executiveScore,
            ToHealthStatus(executiveScore),
            orderedDomains,
            orderedActions,
            now));
    }

    private static ExecutiveDomainSignalResponse BuildExecutiveDomainSignal(
        string domain,
        string label,
        int criticalCount,
        int attentionCount,
        string leadSignal,
        string recommendedAction)
    {
        var score = Math.Clamp(100 - Math.Min(90, criticalCount * 8 + attentionCount * 3), 0, 100);
        return new ExecutiveDomainSignalResponse(
            domain,
            label,
            criticalCount,
            attentionCount,
            score,
            ToHealthStatus(score),
            leadSignal,
            recommendedAction);
    }

    private static IReadOnlyCollection<ExecutiveDecisionBriefResponse> BuildExecutiveDecisionBriefs(
        IReadOnlyCollection<ExecutiveDomainSignalResponse> domainSignals,
        IReadOnlyCollection<ExecutiveCriticalActionResponse> criticalActions)
    {
        var weakestDomains = domainSignals
            .OrderBy(item => item.Score)
            .ThenByDescending(item => item.CriticalCount)
            .Take(3)
            .ToArray();

        var briefs = new List<ExecutiveDecisionBriefResponse>();
        var rank = 1;
        foreach (var domain in weakestDomains)
        {
            if (domain.CriticalCount <= 0 && domain.AttentionCount <= 0)
            {
                continue;
            }

            briefs.Add(new ExecutiveDecisionBriefResponse(
                domain.Domain,
                $"Stabilize {domain.Label}",
                $"{domain.CriticalCount} critical and {domain.AttentionCount} attention signals are active.",
                domain.RecommendedAction,
                rank++));
        }

        if (briefs.Count == 0 && criticalActions.Count > 0)
        {
            var top = criticalActions.First();
            briefs.Add(new ExecutiveDecisionBriefResponse(
                top.Domain,
                top.Title,
                top.Severity,
                top.RecommendedAction,
                rank));
        }

        return briefs;
    }

    private static async Task<IReadOnlyCollection<ExecutiveCriticalActionResponse>> BuildExecutiveCriticalActionsAsync(
        IQueryable<CollectionCase> collectionCases,
        IQueryable<LegalNotice> legalNotices,
        IQueryable<WorkOrder> workOrders,
        IQueryable<UtilityOutage> outages,
        IQueryable<ContractorWorkPermit> contractorPermits,
        IQueryable<AccessCredential> accessCredentials,
        IQueryable<StockItem> stockItems,
        IQueryable<ResidentLifecycleProcess> lifecycleProcesses,
        IQueryable<ResidentRiskFlag> riskFlags,
        DateTime now,
        DateOnly today,
        int limit,
        CancellationToken cancellationToken)
    {
        var items = new List<ExecutiveCriticalActionResponse>();

        var collectionRows = await collectionCases
            .Where(item => item.Status == CollectionCaseStatus.LegalEscalated || item.Status == CollectionCaseStatus.Open || item.Status == CollectionCaseStatus.PaymentPlanActive)
            .OrderByDescending(item => item.Status == CollectionCaseStatus.LegalEscalated)
            .ThenBy(item => item.DueDate ?? DateOnly.MaxValue)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.Reason, item.AmountDue, item.Status, item.OpenedAtUtc, item.DueDate })
            .ToListAsync(cancellationToken);
        items.AddRange(collectionRows.Select(item => new ExecutiveCriticalActionResponse(
            "Legal",
            "CollectionCase",
            item.Id,
            item.CompoundId,
            $"Collection case {item.Status}: {item.AmountDue:N0} IQD",
            item.Status == CollectionCaseStatus.LegalEscalated || (item.DueDate.HasValue && item.DueDate.Value < today) ? "Critical" : "High",
            "Collections / Legal",
            string.IsNullOrWhiteSpace(item.Reason) ? "Review collection case and decide legal escalation." : item.Reason,
            item.OpenedAtUtc,
            item.DueDate.HasValue ? item.DueDate.Value.ToDateTime(TimeOnly.MinValue) : null)));

        var legalNoticeRows = await legalNotices
            .Where(item => item.Status == LegalNoticeStatus.Issued && item.DeadlineDate.HasValue)
            .OrderBy(item => item.DeadlineDate)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.Title, item.CreatedAtUtc, item.DeadlineDate })
            .ToListAsync(cancellationToken);
        items.AddRange(legalNoticeRows.Select(item => new ExecutiveCriticalActionResponse(
            "Legal",
            "LegalNotice",
            item.Id,
            item.CompoundId,
            string.IsNullOrWhiteSpace(item.Title) ? "Issued legal notice deadline" : item.Title,
            item.DeadlineDate.HasValue && item.DeadlineDate.Value < today ? "Critical" : "High",
            "Legal",
            "Serve, acknowledge, or escalate the issued legal notice before deadline breach.",
            item.CreatedAtUtc,
            item.DeadlineDate.HasValue ? item.DeadlineDate.Value.ToDateTime(TimeOnly.MinValue) : null)));

        var workOrderRows = await workOrders
            .Where(item => item.SlaStatus == MaintenanceSlaStatus.ResponseBreached || item.SlaStatus == MaintenanceSlaStatus.ResolutionBreached || item.SlaStatus == MaintenanceSlaStatus.Escalated || (item.DueAtUtc.HasValue && item.DueAtUtc.Value < now && item.Status != WorkOrderStatus.Completed && item.Status != WorkOrderStatus.Cancelled))
            .OrderBy(item => item.DueAtUtc ?? DateTime.MaxValue)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.Title, item.Priority, item.SlaStatus, item.CreatedAtUtc, item.DueAtUtc, item.ResolutionDueAtUtc })
            .ToListAsync(cancellationToken);
        items.AddRange(workOrderRows.Select(item => new ExecutiveCriticalActionResponse(
            "Maintenance",
            "WorkOrder",
            item.Id,
            item.CompoundId,
            string.IsNullOrWhiteSpace(item.Title) ? "Breached work order" : item.Title,
            item.SlaStatus == MaintenanceSlaStatus.Escalated || item.Priority == WorkOrderPriority.Emergency ? "Critical" : "High",
            "Maintenance Operations",
            "Escalate owner, confirm response, and update resident-facing operational status.",
            item.CreatedAtUtc,
            item.ResolutionDueAtUtc ?? item.DueAtUtc)));

        var outageRows = await outages
            .Where(item => (item.Status == UtilityOutageStatus.Planned || item.Status == UtilityOutageStatus.Active) && (item.Severity == UtilityOutageSeverity.Critical || item.Severity == UtilityOutageSeverity.High))
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.EstimatedEndAtUtc ?? DateTime.MaxValue)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.Title, item.Severity, item.CreatedAtUtc, item.EstimatedEndAtUtc })
            .ToListAsync(cancellationToken);
        items.AddRange(outageRows.Select(item => new ExecutiveCriticalActionResponse(
            "Communications",
            "UtilityOutage",
            item.Id,
            item.CompoundId,
            string.IsNullOrWhiteSpace(item.Title) ? $"{item.Severity} utility outage" : item.Title,
            item.Severity == UtilityOutageSeverity.Critical ? "Critical" : "High",
            "Communication Operations",
            "Publish resident update, verify acknowledgement, and close outage when resolved.",
            item.CreatedAtUtc,
            item.EstimatedEndAtUtc)));

        var contractorRows = await contractorPermits
            .Where(item => ((item.Status == ContractorWorkPermitStatus.PendingApproval || item.Status == ContractorWorkPermitStatus.Approved || item.Status == ContractorWorkPermitStatus.CheckedIn) && (item.RiskLevel == ContractorWorkPermitRiskLevel.High || item.RiskLevel == ContractorWorkPermitRiskLevel.Critical)) || (item.Status == ContractorWorkPermitStatus.CheckedIn && item.AllowedUntilUtc < now))
            .OrderByDescending(item => item.RiskLevel)
            .ThenBy(item => item.AllowedUntilUtc)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.Purpose, item.Status, item.RiskLevel, item.CreatedAtUtc, item.AllowedUntilUtc })
            .ToListAsync(cancellationToken);
        items.AddRange(contractorRows.Select(item => new ExecutiveCriticalActionResponse(
            "Access",
            "ContractorWorkPermit",
            item.Id,
            item.CompoundId,
            string.IsNullOrWhiteSpace(item.Purpose) ? $"{item.RiskLevel} contractor permit" : item.Purpose,
            item.RiskLevel == ContractorWorkPermitRiskLevel.Critical || (item.Status == ContractorWorkPermitStatus.CheckedIn && item.AllowedUntilUtc < now) ? "Critical" : "High",
            "Gate / Security",
            "Review contractor permit, escort requirement, and check-out compliance.",
            item.CreatedAtUtc,
            item.AllowedUntilUtc)));

        var expiredCredentials = await accessCredentials
            .Where(item => item.Status == AccessCredentialStatus.Active && item.ValidUntilUtc.HasValue && item.ValidUntilUtc.Value < now)
            .OrderBy(item => item.ValidUntilUtc)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.OwnerDisplayName, item.CreatedAtUtc, item.ValidUntilUtc })
            .ToListAsync(cancellationToken);
        items.AddRange(expiredCredentials.Select(item => new ExecutiveCriticalActionResponse(
            "Access",
            "AccessCredential",
            item.Id,
            item.CompoundId,
            $"Expired active credential: {item.OwnerDisplayName}",
            "Critical",
            "Gate / Security",
            "Revoke, suspend, or replace expired active credential immediately.",
            item.CreatedAtUtc,
            item.ValidUntilUtc)));

        var lowStockRows = await stockItems
            .Where(item => item.CurrentQuantity <= item.MinimumQuantity)
            .OrderBy(item => item.CurrentQuantity - item.MinimumQuantity)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.Name, item.Sku, item.CurrentQuantity, item.MinimumQuantity, item.CreatedAtUtc })
            .ToListAsync(cancellationToken);
        items.AddRange(lowStockRows.Select(item => new ExecutiveCriticalActionResponse(
            "Inventory",
            "StockItem",
            item.Id,
            item.CompoundId,
            $"Low stock: {item.Name} ({item.Sku})",
            item.CurrentQuantity <= 0 ? "Critical" : "High",
            "Procurement / Inventory",
            $"Replenish immediately. Current {item.CurrentQuantity:N2}, minimum {item.MinimumQuantity:N2}.",
            item.CreatedAtUtc,
            null)));

        var lifecycleRows = await lifecycleProcesses
            .Where(item => item.Status == ResidentLifecycleStatus.PendingFinancialClearance || item.Status == ResidentLifecycleStatus.PendingCustodyClearance || item.Status == ResidentLifecycleStatus.PendingUnitReadiness)
            .OrderBy(item => item.TargetDate)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.ProcessType, item.Status, item.TargetDate, item.CreatedAtUtc })
            .ToListAsync(cancellationToken);
        items.AddRange(lifecycleRows.Select(item => new ExecutiveCriticalActionResponse(
            "Lifecycle",
            "ResidentLifecycleProcess",
            item.Id,
            item.CompoundId,
            $"{item.ProcessType} blocked at {item.Status}",
            item.TargetDate < today ? "Critical" : "High",
            "Resident Lifecycle",
            "Clear financial, custody, or unit-readiness blocker before resident transition is closed.",
            item.CreatedAtUtc,
            item.TargetDate.ToDateTime(TimeOnly.MinValue))));

        var riskRows = await riskFlags
            .Where(item => (item.Status == ResidentRiskFlagStatus.Active || item.Status == ResidentRiskFlagStatus.Monitoring) && (item.Severity == ResidentRiskFlagSeverity.Critical || item.Severity == ResidentRiskFlagSeverity.High || (item.NextReviewAtUtc.HasValue && item.NextReviewAtUtc.Value < now)))
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.NextReviewAtUtc ?? DateTime.MaxValue)
            .Take(limit)
            .Select(item => new { item.Id, item.CompoundId, item.Title, item.Severity, item.CreatedAtUtc, item.NextReviewAtUtc, item.RecommendedAction })
            .ToListAsync(cancellationToken);
        items.AddRange(riskRows.Select(item => new ExecutiveCriticalActionResponse(
            "ResidentRisk",
            "ResidentRiskFlag",
            item.Id,
            item.CompoundId,
            string.IsNullOrWhiteSpace(item.Title) ? $"{item.Severity} resident risk flag" : item.Title,
            item.Severity == ResidentRiskFlagSeverity.Critical ? "Critical" : "High",
            "Resident Risk / Compliance",
            string.IsNullOrWhiteSpace(item.RecommendedAction) ? "Review risk flag, assign owner, and record decision." : item.RecommendedAction!,
            item.CreatedAtUtc,
            item.NextReviewAtUtc)));

        return items;
    }

    private async Task<ServiceResult<CompoundAccessScope>> GetValidatedScopeAsync(Guid? compoundId, CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<CompoundAccessScope>.Forbidden("Current user cannot access operations command center.");
        }

        if (compoundId.HasValue && !scope.CanAccess(compoundId.Value))
        {
            return ServiceResult<CompoundAccessScope>.NotFound("Compound was not found.");
        }

        return ServiceResult<CompoundAccessScope>.Success(scope);
    }

    private static IQueryable<T> ApplyOptionalCompoundFilter<T>(IQueryable<T> query, Guid? compoundId)
        where T : class
    {
        if (!compoundId.HasValue)
        {
            return query;
        }

        return query.Where(item => EF.Property<Guid>(item, "CompoundId") == compoundId.Value);
    }

    private static IQueryable<OperationalTask> ApplyTaskFilters(
        IQueryable<OperationalTask> query,
        OperationalTaskSearchQuery searchQuery,
        DateTime now)
    {
        if (searchQuery.Status.HasValue)
        {
            query = query.Where(item => item.Status == searchQuery.Status.Value);
        }

        if (searchQuery.Priority.HasValue)
        {
            query = query.Where(item => item.Priority == searchQuery.Priority.Value);
        }

        if (searchQuery.TaskType.HasValue)
        {
            query = query.Where(item => item.TaskType == searchQuery.TaskType.Value);
        }

        if (searchQuery.AssignedToUserId.HasValue)
        {
            query = query.Where(item => item.AssignedToUserId == searchQuery.AssignedToUserId.Value);
        }

        if (searchQuery.IsOverdue.HasValue)
        {
            query = searchQuery.IsOverdue.Value
                ? query.Where(item =>
                    (item.Status == OperationalTaskStatus.Open || item.Status == OperationalTaskStatus.InProgress)
                    && item.DueAtUtc.HasValue
                    && item.DueAtUtc.Value < now)
                : query.Where(item => !item.DueAtUtc.HasValue || item.DueAtUtc.Value >= now);
        }

        if (!string.IsNullOrWhiteSpace(searchQuery.SearchTerm))
        {
            var term = searchQuery.SearchTerm.Trim();
            query = query.Where(item => item.Title.Contains(term) || item.Description.Contains(term));
        }

        return query;
    }

    private async Task<string?> ValidateAssignedUserScopeAsync(
        Guid compoundId,
        Guid? assignedToUserId,
        CancellationToken cancellationToken)
    {
        if (!assignedToUserId.HasValue)
        {
            return null;
        }

        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == assignedToUserId.Value, cancellationToken);
        if (!userExists)
        {
            return "Assigned user was not found.";
        }

        var isSuperAdmin = await dbContext.UserRoles
            .AsNoTracking()
            .Join(
                dbContext.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .AnyAsync(item => item.UserId == assignedToUserId.Value
                && item.Name == nameof(UserRole.SuperAdmin),
                cancellationToken);
        if (isSuperAdmin)
        {
            return null;
        }

        var hasCompoundAssignment = await dbContext.UserCompoundAssignments
            .AsNoTracking()
            .AnyAsync(assignment => assignment.UserId == assignedToUserId.Value
                && assignment.CompoundId == compoundId
                && assignment.IsActive,
                cancellationToken);

        return hasCompoundAssignment
            ? null
            : "Assigned user must have access to the operational task compound.";
    }

    private static string? ValidateCreateTaskRequest(CreateOperationalTaskRequest request)
    {
        if (request.CompoundId == Guid.Empty)
        {
            return "Compound id is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "Task title is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return "Task description is required.";
        }

        if (request.DueAtUtc.HasValue && request.DueAtUtc.Value <= DateTime.UtcNow.AddMinutes(-1))
        {
            return "Task due date must be in the future.";
        }

        if (request.RelatedEntityType.HasValue != request.RelatedEntityId.HasValue)
        {
            return "Related entity type and id must be supplied together.";
        }

        return null;
    }

    private static OperationalTaskResponse ToTaskResponse(OperationalTask task, DateTime now)
    {
        return new OperationalTaskResponse(
            task.Id,
            task.CompoundId,
            task.TaskType,
            task.Priority,
            task.Status,
            task.Title,
            task.Description,
            task.RelatedEntityType,
            task.RelatedEntityId,
            task.AssignedToUserId,
            task.CreatedByUserId,
            task.CompletedByUserId,
            task.CancelledByUserId,
            task.DueAtUtc,
            (task.Status == OperationalTaskStatus.Open || task.Status == OperationalTaskStatus.InProgress)
                && task.DueAtUtc.HasValue
                && task.DueAtUtc.Value < now,
            task.CreatedAtUtc,
            task.UpdatedAtUtc,
            task.CompletedAtUtc,
            task.CancelledAtUtc,
            task.CompletionNotes,
            task.CancellationReason);
    }

    private static DateRange NormalizeDateRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var to = toUtc ?? DateTime.UtcNow;
        var from = fromUtc ?? to.AddDays(-30);
        if (from > to)
        {
            return new DateRange(from, to, "From date cannot be after to date.");
        }

        return new DateRange(from, to, null);
    }

    private static int CalculateHealthScore(
        int openMaintenance,
        int openComplaints,
        int openWorkOrders,
        int slaBreaches,
        int criticalRiskFlags,
        int overdueRiskReviews,
        int pendingDecisions,
        int overdueTasks)
    {
        var penalty = Math.Min(20, openMaintenance)
            + Math.Min(20, openComplaints * 2)
            + Math.Min(20, openWorkOrders)
            + Math.Min(30, slaBreaches * 3)
            + Math.Min(20, criticalRiskFlags * 5)
            + Math.Min(15, overdueRiskReviews * 3)
            + Math.Min(10, pendingDecisions)
            + Math.Min(10, overdueTasks * 2);
        return Math.Clamp(100 - penalty, 0, 100);
    }

    private static IReadOnlyCollection<CompoundHealthFactorResponse> BuildHealthFactors(
        OperationalCommandCenterResponse center,
        int overdueFinancialItems)
    {
        var factors = new List<CompoundHealthFactorResponse>();
        AddFactor(factors, "Maintenance", Math.Min(20, center.OpenMaintenanceRequestCount), $"{center.OpenMaintenanceRequestCount} open maintenance requests.");
        AddFactor(factors, "Complaints", Math.Min(20, center.OpenComplaintCount * 2), $"{center.OpenComplaintCount} open complaints.");
        AddFactor(factors, "WorkOrders", Math.Min(20, center.OpenWorkOrderCount), $"{center.OpenWorkOrderCount} open work orders.");
        AddFactor(factors, "SLA", Math.Min(30, center.SlaBreachCount * 3), $"{center.SlaBreachCount} operational SLA breaches.");
        AddFactor(factors, "Risk", Math.Min(20, center.CriticalRiskFlagCount * 5), $"{center.CriticalRiskFlagCount} critical resident risk flags.");
        AddFactor(factors, "Finance", Math.Min(20, overdueFinancialItems * 2), $"{overdueFinancialItems} overdue financial items.");
        AddFactor(factors, "Approvals", Math.Min(10, center.PendingApprovalRequestCount), $"{center.PendingApprovalRequestCount} pending approval requests.");
        return factors;
    }

    private static void AddFactor(List<CompoundHealthFactorResponse> factors, string area, int penalty, string reason)
    {
        if (penalty <= 0)
        {
            return;
        }

        factors.Add(new CompoundHealthFactorResponse(area, penalty, reason));
    }

    private static int ToAgeHours(DateTime fromUtc, DateTime toUtc)
    {
        return Math.Max(0, (int)Math.Floor((toUtc - fromUtc).TotalHours));
    }

    private sealed record ExecutiveIntelligenceBundle(
        int ExecutiveScore,
        string ExecutiveStatus,
        IReadOnlyCollection<ExecutiveDomainSignalResponse> DomainSignals,
        IReadOnlyCollection<ExecutiveCriticalActionResponse> CriticalActions,
        DateTime GeneratedAtUtc);

    private sealed record DateRange(DateTime FromUtc, DateTime ToUtc, string? Error);

    private sealed record SlaBreachRow(
        string SourceType,
        Guid SourceId,
        Guid CompoundId,
        string Title,
        string SeverityLabel,
        DateTime CreatedAtUtc,
        DateTime? DueAtUtc,
        int AgeHours,
        int BreachHours,
        string Recommendation);
}
