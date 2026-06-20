using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class SaasTenantIntelligenceService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService)
    : ISaasTenantIntelligenceService
{
    public async Task<ServiceResult<SaasPortfolioOverviewResponse>> GetPortfolioOverviewAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<SaasPortfolioOverviewResponse>.Forbidden("Authenticated compound access is required.");
        }

        var compounds = await GetAccessibleCompoundsAsync(scope, cancellationToken);
        var summaries = new List<SaasTenantPriorityItemResponse>();

        foreach (var compound in compounds)
        {
            summaries.Add(await BuildPriorityItemAsync(compound, cancellationToken));
        }

        var ordered = summaries
            .OrderByDescending(item => item.PriorityScore)
            .ThenByDescending(item => item.OutstandingAmount)
            .ToArray();

        var capacity = await BuildCapacitySnapshotAsync(compounds, cancellationToken);
        var license = await BuildLicenseSnapshotAsync(capacity, cancellationToken);
        var normalizedLimit = NormalizeLimit(limit, 20, 100);

        var response = new SaasPortfolioOverviewResponse(
            DateTime.UtcNow,
            license,
            capacity,
            compounds.Length,
            ordered.Count(item => item.PriorityBand == "Critical"),
            ordered.Count(item => item.PriorityBand == "High"),
            ordered.Count(item => item.PriorityBand == "Medium"),
            ordered.Count(item => item.PriorityBand == "Low"),
            ordered.Take(normalizedLimit).ToArray(),
            BuildCommercialActions(license, capacity, ordered));

        return ServiceResult<SaasPortfolioOverviewResponse>.Success(response);
    }

    public async Task<ServiceResult<SaasTenantReadinessResponse>> GetTenantReadinessAsync(
        Guid compoundId,
        CancellationToken cancellationToken = default)
    {
        if (compoundId == Guid.Empty)
        {
            return ServiceResult<SaasTenantReadinessResponse>.BadRequest("Compound id is required.");
        }

        var access = await ValidateCompoundAccessAsync(compoundId, cancellationToken);
        if (access is not null)
        {
            return ServiceResult<SaasTenantReadinessResponse>.Forbidden(access);
        }

        var compound = await dbContext.Compounds
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == compoundId, cancellationToken);

        if (compound is null)
        {
            return ServiceResult<SaasTenantReadinessResponse>.NotFound("Compound was not found.");
        }

        var priority = await BuildPriorityItemAsync(compound, cancellationToken);
        var operational = await BuildOperationalSnapshotAsync(compound.Id, cancellationToken);
        var financial = await BuildFinancialSnapshotAsync(compound.Id, cancellationToken);
        var reliability = await BuildReliabilitySnapshotAsync(compound.Id, cancellationToken);
        var blockers = BuildReadinessBlockers(priority, operational, financial, reliability);
        var readinessScore = Math.Clamp(100 - priority.PriorityScore - (blockers.Count * 8), 0, 100);
        var readinessBand = GetReadinessBand(readinessScore);

        var response = new SaasTenantReadinessResponse(
            compound.Id,
            compound.Name,
            DateTime.UtcNow,
            priority.PriorityBand,
            priority.PriorityScore,
            readinessBand,
            blockers.Count == 0 && readinessScore >= 70,
            operational,
            financial,
            reliability,
            blockers,
            BuildTenantReadinessActions(priority, readinessBand, blockers));

        return ServiceResult<SaasTenantReadinessResponse>.Success(response);
    }

    public async Task<ServiceResult<DarakPrioritizationBrainResponse>> GetPrioritizationBrainAsync(
        string? area = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<DarakPrioritizationBrainResponse>.Forbidden("Authenticated compound access is required.");
        }

        var compounds = await GetAccessibleCompoundsAsync(scope, cancellationToken);
        var actions = new List<DarakPriorityActionResponse>();

        foreach (var compound in compounds)
        {
            var priority = await BuildPriorityItemAsync(compound, cancellationToken);
            actions.AddRange(BuildPriorityActions(compound, priority));
        }

        var normalizedLimit = NormalizeLimit(limit, 50, 200);
        var filtered = actions
            .Where(item => MatchesFilter(item.Area, area))
            .OrderByDescending(item => item.PriorityScore)
            .ThenBy(item => item.DueAtUtc ?? DateTime.MaxValue)
            .Take(normalizedLimit)
            .ToArray();

        var response = new DarakPrioritizationBrainResponse(
            DateTime.UtcNow,
            area,
            filtered.Length,
            filtered,
            BuildPrioritizationSummary(filtered));

        return ServiceResult<DarakPrioritizationBrainResponse>.Success(response);
    }

    private async Task<string?> ValidateCompoundAccessAsync(Guid compoundId, CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return "Authenticated compound access is required.";
        }

        if (!scope.CanAccess(compoundId))
        {
            return "You do not have access to this compound.";
        }

        return null;
    }

    private async Task<Compound[]> GetAccessibleCompoundsAsync(
        CompoundAccessScope scope,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Compounds.AsNoTracking();

        if (!scope.IsSuperAdmin)
        {
            var allowed = scope.AllowedCompoundIds;
            query = query.Where(item => allowed.Contains(item.Id));
        }

        return await query
            .OrderBy(item => item.Name)
            .ToArrayAsync(cancellationToken);
    }

    private async Task<SaasTenantPriorityItemResponse> BuildPriorityItemAsync(
        Compound compound,
        CancellationToken cancellationToken)
    {
        var operational = await BuildOperationalSnapshotAsync(compound.Id, cancellationToken);
        var financial = await BuildFinancialSnapshotAsync(compound.Id, cancellationToken);
        var reliability = await BuildReliabilitySnapshotAsync(compound.Id, cancellationToken);

        var score = 0;
        score += financial.OutstandingAmount > 0 ? 18 : 0;
        score += Math.Min(financial.OpenCollectionCases * 8, 24);
        score += Math.Min(financial.OpenLegalNotices * 10, 30);
        score += Math.Min(financial.ActiveRiskFlags * 7, 28);
        score += Math.Min(operational.OpenMaintenanceRequests * 4, 16);
        score += Math.Min(operational.OpenWorkOrders * 5, 20);
        score += Math.Min(operational.OpenSupportCases * 6, 24);
        score += Math.Min(reliability.FailedNotifications * 5, 20);
        score += reliability.ReliabilityBand == "Unhealthy" ? 15 : reliability.ReliabilityBand == "Degraded" ? 8 : 0;
        score = Math.Clamp(score, 0, 100);

        var reasons = BuildPriorityReasons(operational, financial, reliability);
        var actions = BuildPriorityRecommendations(operational, financial, reliability);

        return new SaasTenantPriorityItemResponse(
            compound.Id,
            compound.Name,
            compound.Code,
            GetPriorityBand(score),
            score,
            operational.Units,
            operational.Residents,
            financial.OutstandingAmount,
            operational.OpenMaintenanceRequests + operational.OpenWorkOrders,
            operational.OpenSupportCases,
            financial.OpenCollectionCases + financial.OpenLegalNotices,
            reliability.FailedNotifications,
            financial.ActiveRiskFlags,
            reasons,
            actions);
    }

    private async Task<SaasCapacitySnapshotResponse> BuildCapacitySnapshotAsync(
        IReadOnlyCollection<Compound> compounds,
        CancellationToken cancellationToken)
    {
        var compoundIds = compounds.Select(item => item.Id).ToArray();
        var units = await dbContext.PropertyUnits
            .AsNoTracking()
            .CountAsync(item => compoundIds.Contains(item.CompoundId), cancellationToken);
        var residents = await dbContext.ResidentProfiles
            .AsNoTracking()
            .CountAsync(item => compoundIds.Contains(item.CompoundId) && item.IsActive, cancellationToken);
        var latestLicense = await GetLatestLicenseAsync(cancellationToken);
        var maxCompounds = latestLicense?.MaxCompounds ?? 0;
        var maxUnits = latestLicense?.MaxUnits ?? 0;
        var compoundUtilization = CalculateUtilization(compounds.Count, maxCompounds);
        var unitUtilization = CalculateUtilization(units, maxUnits);

        return new SaasCapacitySnapshotResponse(
            compounds.Count,
            units,
            residents,
            maxCompounds,
            maxUnits,
            compoundUtilization,
            unitUtilization,
            GetUtilizationBand(Math.Max(compoundUtilization, unitUtilization)));
    }

    private async Task<SaasLicenseSnapshotResponse> BuildLicenseSnapshotAsync(
        SaasCapacitySnapshotResponse capacity,
        CancellationToken cancellationToken)
    {
        var license = await GetLatestLicenseAsync(cancellationToken);
        var now = DateTime.UtcNow;

        if (license is null)
        {
            return new SaasLicenseSnapshotResponse(
                null,
                "Unlicensed DARAK instance",
                "Unconfigured",
                "Unconfigured",
                0,
                0,
                null,
                false,
                false,
                0,
                "License profile is not configured yet.");
        }

        var isExpired = license.ExpiresAtUtc.HasValue && license.ExpiresAtUtc.Value < now;
        var isCapacityExceeded = (license.MaxCompounds > 0 && capacity.Compounds > license.MaxCompounds)
            || (license.MaxUnits > 0 && capacity.Units > license.MaxUnits);
        var daysUntilExpiry = license.ExpiresAtUtc.HasValue
            ? (int)Math.Floor((license.ExpiresAtUtc.Value - now).TotalDays)
            : 9999;
        var commercialState = BuildCommercialState(license, isExpired, isCapacityExceeded, daysUntilExpiry);

        return new SaasLicenseSnapshotResponse(
            license.Id,
            license.LicensedTo,
            license.Plan.ToString(),
            license.Status.ToString(),
            license.MaxCompounds,
            license.MaxUnits,
            license.ExpiresAtUtc,
            isExpired,
            isCapacityExceeded,
            daysUntilExpiry,
            commercialState);
    }

    private async Task<LicenseProfile?> GetLatestLicenseAsync(CancellationToken cancellationToken)
    {
        return await dbContext.LicenseProfiles
            .AsNoTracking()
            .OrderByDescending(item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
            .ThenByDescending(item => item.IssuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<SaasTenantOperationalSnapshotResponse> BuildOperationalSnapshotAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        var units = await dbContext.PropertyUnits
            .AsNoTracking()
            .CountAsync(item => item.CompoundId == compoundId, cancellationToken);
        var occupiedUnits = await dbContext.PropertyUnits
            .AsNoTracking()
            .CountAsync(item => item.CompoundId == compoundId
                && (item.UnitStatus == UnitStatus.Occupied
                    || item.UnitStatus == UnitStatus.Rented
                    || item.UnitStatus == UnitStatus.SoldCash
                    || item.UnitStatus == UnitStatus.SoldInstallment), cancellationToken);
        var residents = await dbContext.ResidentProfiles
            .AsNoTracking()
            .CountAsync(item => item.CompoundId == compoundId && item.IsActive, cancellationToken);
        var openMaintenance = await dbContext.MaintenanceRequests
            .AsNoTracking()
            .CountAsync(item => item.CompoundId == compoundId
                && item.Status != MaintenanceStatus.Resolved
                && item.Status != MaintenanceStatus.Closed
                && item.Status != MaintenanceStatus.Rejected
                && item.Status != MaintenanceStatus.Cancelled, cancellationToken);
        var openWorkOrders = await dbContext.WorkOrders
            .AsNoTracking()
            .CountAsync(item => item.CompoundId == compoundId
                && item.Status != WorkOrderStatus.Completed
                && item.Status != WorkOrderStatus.Cancelled, cancellationToken);
        var openSupport = await dbContext.SupportCases
            .AsNoTracking()
            .CountAsync(item => item.CompoundId == compoundId
                && item.Status != SupportCaseStatus.Resolved
                && item.Status != SupportCaseStatus.Closed
                && item.Status != SupportCaseStatus.Cancelled, cancellationToken);

        return new SaasTenantOperationalSnapshotResponse(
            units,
            occupiedUnits,
            residents,
            openMaintenance,
            openWorkOrders,
            openSupport);
    }

    private async Task<SaasTenantFinancialSnapshotResponse> BuildFinancialSnapshotAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        var openBillsQuery = dbContext.UtilityBills
            .AsNoTracking()
            .Where(item => item.CompoundId == compoundId
                && item.BillStatus != BillStatus.Paid
                && item.BillStatus != BillStatus.Cancelled
                && item.TotalAmount > item.PaidAmount);
        var openBills = await openBillsQuery.CountAsync(cancellationToken);
        var outstanding = await openBillsQuery
            .SumAsync(item => (decimal?)(item.TotalAmount - item.PaidAmount), cancellationToken) ?? 0;
        var openCollections = await dbContext.CollectionCases
            .AsNoTracking()
            .CountAsync(item => item.CompoundId == compoundId
                && item.Status != CollectionCaseStatus.Settled
                && item.Status != CollectionCaseStatus.Closed
                && item.Status != CollectionCaseStatus.Cancelled, cancellationToken);
        var openLegal = await dbContext.LegalNotices
            .AsNoTracking()
            .CountAsync(item => item.CompoundId == compoundId
                && item.Status != LegalNoticeStatus.Acknowledged
                && item.Status != LegalNoticeStatus.Cancelled, cancellationToken);
        var activeRiskFlags = await dbContext.ResidentRiskFlags
            .AsNoTracking()
            .CountAsync(item => item.CompoundId == compoundId
                && (item.Status == ResidentRiskFlagStatus.Active
                    || item.Status == ResidentRiskFlagStatus.Monitoring), cancellationToken);

        return new SaasTenantFinancialSnapshotResponse(
            outstanding,
            openBills,
            openCollections,
            openLegal,
            activeRiskFlags);
    }

    private async Task<SaasTenantReliabilitySnapshotResponse> BuildReliabilitySnapshotAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var pendingNotifications = await dbContext.NotificationOutboxes
            .AsNoTracking()
            .CountAsync(item => item.CompoundId == compoundId
                && (item.Status == NotificationStatus.Pending || item.Status == NotificationStatus.Processing), cancellationToken);
        var failedNotifications = await dbContext.NotificationOutboxes
            .AsNoTracking()
            .CountAsync(item => item.CompoundId == compoundId && item.Status == NotificationStatus.Failed, cancellationToken);
        var openIntegrationFailures = await dbContext.IntegrationFailureEvents
            .AsNoTracking()
            .CountAsync(item => item.Status != IntegrationFailureStatus.Resolved, cancellationToken);
        var failedJobs24h = await dbContext.BackgroundJobRuns
            .AsNoTracking()
            .CountAsync(item => item.Status == BackgroundJobRunStatus.Failed && item.StartedAtUtc >= since, cancellationToken);
        var reliabilityBand = failedNotifications > 5 || openIntegrationFailures > 3 || failedJobs24h > 2
            ? "Unhealthy"
            : failedNotifications > 0 || openIntegrationFailures > 0 || failedJobs24h > 0
                ? "Degraded"
                : "Healthy";

        return new SaasTenantReliabilitySnapshotResponse(
            pendingNotifications,
            failedNotifications,
            openIntegrationFailures,
            failedJobs24h,
            reliabilityBand);
    }

    private static IReadOnlyCollection<string> BuildPriorityReasons(
        SaasTenantOperationalSnapshotResponse operational,
        SaasTenantFinancialSnapshotResponse financial,
        SaasTenantReliabilitySnapshotResponse reliability)
    {
        var reasons = new List<string>();

        if (financial.OutstandingAmount > 0)
        {
            reasons.Add($"Outstanding balance is {financial.OutstandingAmount:N0} IQD.");
        }

        if (financial.OpenCollectionCases > 0 || financial.OpenLegalNotices > 0)
        {
            reasons.Add("Collection or legal workflow needs management attention.");
        }

        if (financial.ActiveRiskFlags > 0)
        {
            reasons.Add("Active resident risk flags are present.");
        }

        if (operational.OpenMaintenanceRequests > 0 || operational.OpenWorkOrders > 0)
        {
            reasons.Add("Open operational work may affect resident experience.");
        }

        if (operational.OpenSupportCases > 0)
        {
            reasons.Add("Open support cases require follow-up.");
        }

        if (reliability.ReliabilityBand != "Healthy")
        {
            reasons.Add("Notification or integration reliability is degraded.");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("Tenant is stable with no major commercial blocker.");
        }

        return reasons;
    }

    private static IReadOnlyCollection<string> BuildPriorityRecommendations(
        SaasTenantOperationalSnapshotResponse operational,
        SaasTenantFinancialSnapshotResponse financial,
        SaasTenantReliabilitySnapshotResponse reliability)
    {
        var actions = new List<string>();

        if (financial.OutstandingAmount > 0 || financial.OpenCollectionCases > 0)
        {
            actions.Add("Run finance follow-up and update collection ownership.");
        }

        if (financial.OpenLegalNotices > 0)
        {
            actions.Add("Review legal notice deadlines and escalation stage.");
        }

        if (operational.OpenMaintenanceRequests > 0 || operational.OpenWorkOrders > 0)
        {
            actions.Add("Review maintenance and work-order backlog by SLA risk.");
        }

        if (operational.OpenSupportCases > 0)
        {
            actions.Add("Assign support cases and close stale conversations.");
        }

        if (reliability.ReliabilityBand != "Healthy")
        {
            actions.Add("Clear failed notifications, integrations, and background jobs.");
        }

        if (actions.Count == 0)
        {
            actions.Add("Keep tenant on standard monitoring cadence.");
        }

        return actions;
    }

    private static IReadOnlyCollection<string> BuildReadinessBlockers(
        SaasTenantPriorityItemResponse priority,
        SaasTenantOperationalSnapshotResponse operational,
        SaasTenantFinancialSnapshotResponse financial,
        SaasTenantReliabilitySnapshotResponse reliability)
    {
        var blockers = new List<string>();

        if (priority.PriorityBand is "Critical" or "High")
        {
            blockers.Add("Tenant priority is too high for clean commercial handoff.");
        }

        if (financial.OutstandingAmount > 0)
        {
            blockers.Add("Outstanding financial exposure must be reviewed.");
        }

        if (financial.OpenLegalNotices > 0)
        {
            blockers.Add("Open legal notices require governance review.");
        }

        if (operational.OpenSupportCases > 0)
        {
            blockers.Add("Open support cases should be assigned or closed.");
        }

        if (reliability.ReliabilityBand == "Unhealthy")
        {
            blockers.Add("System reliability is unhealthy for this tenant.");
        }

        return blockers;
    }

    private static IReadOnlyCollection<string> BuildTenantReadinessActions(
        SaasTenantPriorityItemResponse priority,
        string readinessBand,
        IReadOnlyCollection<string> blockers)
    {
        var actions = new List<string>();

        if (blockers.Count > 0)
        {
            actions.Add("Assign an owner for every blocker before buyer or tenant handoff.");
        }

        if (priority.PriorityBand is "Critical" or "High")
        {
            actions.Add("Run a senior management review for this tenant.");
        }

        if (readinessBand != "Ready")
        {
            actions.Add("Re-run readiness after finance, support, and reliability cleanup.");
        }

        if (actions.Count == 0)
        {
            actions.Add("Tenant is ready for standard SaaS onboarding or executive demo.");
        }

        return actions;
    }

    private static IReadOnlyCollection<DarakPriorityActionResponse> BuildPriorityActions(
        Compound compound,
        SaasTenantPriorityItemResponse priority)
    {
        var actions = new List<DarakPriorityActionResponse>();
        var now = DateTime.UtcNow;

        if (priority.OutstandingAmount > 0)
        {
            actions.Add(new DarakPriorityActionResponse(
                $"finance-{compound.Id:N}",
                compound.Id,
                compound.Name,
                "Financial",
                priority.PriorityBand,
                Math.Clamp(priority.PriorityScore + 5, 0, 100),
                "Financial exposure follow-up",
                $"Outstanding amount is {priority.OutstandingAmount:N0} IQD.",
                "Finance manager",
                "Review balances, collection case ownership, and payment plan eligibility.",
                now.AddDays(1)));
        }

        if (priority.OpenLegalItems > 0)
        {
            actions.Add(new DarakPriorityActionResponse(
                $"legal-{compound.Id:N}",
                compound.Id,
                compound.Name,
                "Legal",
                priority.PriorityBand,
                Math.Clamp(priority.PriorityScore + 8, 0, 100),
                "Legal and collection governance review",
                "Open legal or collection workflows exist.",
                "Legal/compliance owner",
                "Verify deadlines, notices, and escalation evidence.",
                now.AddDays(2)));
        }

        if (priority.OpenOperationalItems > 0)
        {
            actions.Add(new DarakPriorityActionResponse(
                $"operations-{compound.Id:N}",
                compound.Id,
                compound.Name,
                "Operations",
                priority.PriorityBand,
                priority.PriorityScore,
                "Operations backlog cleanup",
                "Maintenance or work-order backlog may affect service quality.",
                "Operations manager",
                "Prioritize overdue and high-severity operational work.",
                now.AddDays(3)));
        }

        if (priority.OpenSupportCases > 0)
        {
            actions.Add(new DarakPriorityActionResponse(
                $"support-{compound.Id:N}",
                compound.Id,
                compound.Name,
                "Support",
                priority.PriorityBand,
                priority.PriorityScore,
                "Support case assignment review",
                "Open support cases require ownership.",
                "Support lead",
                "Assign open cases and close stale escalations.",
                now.AddDays(2)));
        }

        if (priority.FailedNotifications > 0)
        {
            actions.Add(new DarakPriorityActionResponse(
                $"reliability-{compound.Id:N}",
                compound.Id,
                compound.Name,
                "Reliability",
                priority.PriorityBand,
                Math.Clamp(priority.PriorityScore + 4, 0, 100),
                "Notification reliability repair",
                "Failed notification deliveries exist.",
                "System administrator",
                "Inspect notification outbox failures and provider configuration.",
                now.AddDays(1)));
        }

        if (actions.Count == 0)
        {
            actions.Add(new DarakPriorityActionResponse(
                $"monitor-{compound.Id:N}",
                compound.Id,
                compound.Name,
                "Monitoring",
                priority.PriorityBand,
                priority.PriorityScore,
                "Standard monitoring",
                "No urgent SaaS or operational blocker detected.",
                "Compound admin",
                "Keep tenant on normal monitoring cadence.",
                null));
        }

        return actions;
    }

    private static IReadOnlyCollection<string> BuildCommercialActions(
        SaasLicenseSnapshotResponse license,
        SaasCapacitySnapshotResponse capacity,
        IReadOnlyCollection<SaasTenantPriorityItemResponse> tenants)
    {
        var actions = new List<string>();

        if (license.Status is "Expired" or "Suspended" or "Revoked" || license.IsExpired)
        {
            actions.Add("Resolve license status before production rollout.");
        }

        if (license.IsCapacityExceeded)
        {
            actions.Add("Upgrade license capacity or reduce tenant scope before onboarding more compounds.");
        }

        if (capacity.UtilizationBand == "NearLimit")
        {
            actions.Add("Prepare commercial upsell discussion because tenant capacity is near limit.");
        }

        if (tenants.Any(item => item.PriorityBand == "Critical"))
        {
            actions.Add("Review critical tenants before buyer demo or production handoff.");
        }

        if (actions.Count == 0)
        {
            actions.Add("Portfolio is ready for managed SaaS monitoring and buyer demonstration.");
        }

        return actions;
    }

    private static IReadOnlyCollection<string> BuildPrioritizationSummary(
        IReadOnlyCollection<DarakPriorityActionResponse> actions)
    {
        if (actions.Count == 0)
        {
            return new[] { "No actions matched the selected filter." };
        }

        var critical = actions.Count(item => item.PriorityBand == "Critical");
        var high = actions.Count(item => item.PriorityBand == "High");
        var areas = actions
            .GroupBy(item => item.Area)
            .OrderByDescending(group => group.Count())
            .Select(group => $"{group.Key}: {group.Count()}")
            .ToArray();

        return new[]
        {
            $"Actions: {actions.Count}, critical: {critical}, high: {high}.",
            $"Area distribution: {string.Join(", ", areas)}."
        };
    }

    private static decimal CalculateUtilization(int current, int max)
    {
        if (max <= 0)
        {
            return 0;
        }

        return Math.Round((decimal)current / max * 100, 2);
    }

    private static string GetUtilizationBand(decimal utilization)
    {
        if (utilization >= 100)
        {
            return "Exceeded";
        }

        if (utilization >= 80)
        {
            return "NearLimit";
        }

        return "Normal";
    }

    private static string BuildCommercialState(
        LicenseProfile license,
        bool isExpired,
        bool isCapacityExceeded,
        int daysUntilExpiry)
    {
        if (license.Status is LicenseStatus.Suspended or LicenseStatus.Revoked)
        {
            return "License is blocked for commercial use.";
        }

        if (isExpired || license.Status == LicenseStatus.Expired)
        {
            return "License has expired.";
        }

        if (isCapacityExceeded)
        {
            return "License capacity is exceeded.";
        }

        if (daysUntilExpiry <= 30)
        {
            return "License renewal should be prepared within 30 days.";
        }

        return "License is commercially usable.";
    }

    private static string GetPriorityBand(int score)
    {
        if (score >= 70)
        {
            return "Critical";
        }

        if (score >= 45)
        {
            return "High";
        }

        if (score >= 20)
        {
            return "Medium";
        }

        return "Low";
    }

    private static string GetReadinessBand(int score)
    {
        if (score >= 80)
        {
            return "Ready";
        }

        if (score >= 60)
        {
            return "NeedsCleanup";
        }

        return "Blocked";
    }

    private static int NormalizeLimit(int value, int defaultValue, int max)
    {
        if (value <= 0)
        {
            return defaultValue;
        }

        return Math.Clamp(value, 1, max);
    }

    private static bool MatchesFilter(string value, string? filter)
    {
        return string.IsNullOrWhiteSpace(filter)
            || value.Equals(filter, StringComparison.OrdinalIgnoreCase);
    }
}
