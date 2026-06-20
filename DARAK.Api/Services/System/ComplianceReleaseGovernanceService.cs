using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class ComplianceReleaseGovernanceService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService)
    : IComplianceReleaseGovernanceService
{
    private const int DefaultDays = 30;
    private const int MaxDays = 365;
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    public async Task<ServiceResult<ReleaseReadinessBoardResponse>> GetReleaseReadinessBoardAsync(
        ReleaseGovernanceQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateScopeAsync(query, cancellationToken);
        if (validation.Error is not null)
        {
            return ServiceResult<ReleaseReadinessBoardResponse>.Forbidden(validation.Error);
        }

        var now = DateTime.UtcNow;
        var fromUtc = now.AddDays(-NormalizeDays(query.Days));

        var license = await dbContext.LicenseProfiles.AsNoTracking()
            .OrderByDescending(item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var activeLicense = license is not null
            && license.Status == LicenseStatus.Active
            && (!license.ExpiresAtUtc.HasValue || license.ExpiresAtUtc.Value > now);

        var hasScopedCompound = await ApplyCompoundFilter(dbContext.Compounds.AsNoTracking(), validation.Scope, query.CompoundId)
            .AnyAsync(cancellationToken);
        var auditLogs = ApplyAuditScope(dbContext.AuditLogEntries.AsNoTracking(), validation.Scope, query.CompoundId)
            .Where(item => item.CreatedAtUtc >= fromUtc && item.CreatedAtUtc <= now);
        var auditCount = await auditLogs.CountAsync(cancellationToken);
        var criticalAuditCount = await auditLogs.CountAsync(item => item.Severity == AuditSeverity.Critical, cancellationToken);
        var highAuditCount = await auditLogs.CountAsync(item => item.Severity == AuditSeverity.High, cancellationToken);

        var notifications = ApplyNotificationScope(dbContext.NotificationOutboxes.AsNoTracking(), validation.Scope, query.CompoundId);
        var failedNotifications = await notifications.CountAsync(item => item.Status == NotificationStatus.Failed, cancellationToken);
        var pendingNotifications = await notifications.CountAsync(item => item.Status == NotificationStatus.Pending || item.Status == NotificationStatus.Processing, cancellationToken);

        var openIntegrations = await dbContext.IntegrationFailureEvents.AsNoTracking()
            .CountAsync(item => item.Status == IntegrationFailureStatus.Open || item.Status == IntegrationFailureStatus.Acknowledged, cancellationToken);
        var failedJobs24h = await dbContext.BackgroundJobRuns.AsNoTracking()
            .CountAsync(item => item.StartedAtUtc >= now.AddHours(-24) && item.Status == BackgroundJobRunStatus.Failed, cancellationToken);
        var latestHealth = await dbContext.SystemHealthSnapshots.AsNoTracking()
            .OrderByDescending(item => item.CapturedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var pendingApprovals = await ApplyCompoundFilter(dbContext.ApprovalRequests.AsNoTracking(), validation.Scope, query.CompoundId)
            .CountAsync(item => item.Status == ApprovalStatus.Pending, cancellationToken);

        var items = new List<ReleaseReadinessItemResponse>
        {
            BuildReadinessItem("license", "Commercial", "Active commercial license profile", activeLicense, true, activeLicense ? "Low" : "Critical", license is null ? "No license profile found." : $"License status: {license.Status}.", "Activate and verify the buyer license profile before release."),
            BuildReadinessItem("compound-scope", "Operations", "At least one accessible compound exists", hasScopedCompound, true, hasScopedCompound ? "Low" : "Critical", hasScopedCompound ? "Accessible compound scope is available." : "No accessible compound exists for this release scope.", "Create the buyer compound and assign administrators."),
            BuildReadinessItem("audit-evidence", "Audit", "Audit trail has recent evidence", auditCount > 0, true, auditCount > 0 ? "Low" : "High", $"{auditCount} audit events in the selected period.", "Run and verify operational workflows so audit evidence is available."),
            BuildReadinessItem("critical-audit", "Audit", "No critical audit events in release window", criticalAuditCount == 0, true, criticalAuditCount == 0 ? "Low" : "Critical", $"{criticalAuditCount} critical audit events and {highAuditCount} high events.", "Review critical audit events before commercial handoff."),
            BuildReadinessItem("notifications", "Operations", "Notification outbox has no failed messages", failedNotifications == 0, true, failedNotifications == 0 ? "Low" : "High", $"{failedNotifications} failed notifications and {pendingNotifications} pending/processing notifications.", "Resolve failed notifications and confirm delivery providers."),
            BuildReadinessItem("integrations", "System", "No unresolved integration failures", openIntegrations == 0, true, openIntegrations == 0 ? "Low" : "Critical", $"{openIntegrations} unresolved integration failures.", "Resolve or acknowledge integrations before release."),
            BuildReadinessItem("background-jobs", "System", "No failed background jobs in last 24 hours", failedJobs24h == 0, true, failedJobs24h == 0 ? "Low" : "High", $"{failedJobs24h} failed background jobs in the last 24 hours.", "Investigate failed background jobs and rerun verification."),
            BuildReadinessItem("health", "System", "Latest system health is not unhealthy", latestHealth is not null && latestHealth.Status != SystemHealthStatus.Unhealthy, true, latestHealth is null ? "High" : latestHealth.Status == SystemHealthStatus.Healthy ? "Low" : "Medium", latestHealth is null ? "No system health snapshot found." : $"Latest health: {latestHealth.Status} at {latestHealth.CapturedAtUtc:O}.", "Capture a fresh healthy system snapshot."),
            BuildReadinessItem("approvals", "Governance", "No pending approval decisions blocking release", pendingApprovals == 0, false, pendingApprovals == 0 ? "Low" : "Medium", $"{pendingApprovals} pending approval requests.", "Close pending approvals or document why they are safe to release.")
        };

        var required = items.Where(item => item.IsRequired).ToArray();
        var passedRequired = required.Count(item => item.IsPassed);
        var readinessScore = required.Length == 0 ? 100 : (int)Math.Round(passedRequired * 100.0 / required.Length);
        var blockerCount = items.Count(item => item.IsRequired && !item.IsPassed && item.Severity == "Critical");
        var warningCount = items.Count(item => !item.IsPassed && item.Severity != "Critical");
        var status = blockerCount > 0 ? "Blocked" : readinessScore >= 90 ? "Ready" : "Conditional";
        var actions = items
            .Where(item => !item.IsPassed)
            .OrderByDescending(item => SeverityRank(item.Severity))
            .ThenBy(item => item.Area)
            .Select((item, index) => new ReleaseGovernanceActionResponse(item.Area, item.Severity, item.Recommendation, ResolveOwner(item.Area), index + 1))
            .ToArray();

        return ServiceResult<ReleaseReadinessBoardResponse>.Success(new ReleaseReadinessBoardResponse(
            query.CompoundId,
            readinessScore,
            status,
            blockerCount,
            warningCount,
            items,
            actions,
            now));
    }

    public async Task<ServiceResult<AuditEvidenceDashboardResponse>> GetAuditEvidenceDashboardAsync(
        ReleaseGovernanceQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateScopeAsync(query, cancellationToken);
        if (validation.Error is not null)
        {
            return ServiceResult<AuditEvidenceDashboardResponse>.Forbidden(validation.Error);
        }

        var now = DateTime.UtcNow;
        var fromUtc = now.AddDays(-NormalizeDays(query.Days));
        var logs = ApplyAuditScope(dbContext.AuditLogEntries.AsNoTracking(), validation.Scope, query.CompoundId)
            .Where(item => item.CreatedAtUtc >= fromUtc && item.CreatedAtUtc <= now);

        var total = await logs.CountAsync(cancellationToken);
        var critical = await logs.CountAsync(item => item.Severity == AuditSeverity.Critical, cancellationToken);
        var high = await logs.CountAsync(item => item.Severity == AuditSeverity.High, cancellationToken);
        var medium = await logs.CountAsync(item => item.Severity == AuditSeverity.Medium, cancellationToken);
        var low = await logs.CountAsync(item => item.Severity == AuditSeverity.Low, cancellationToken);
        var systemGenerated = await logs.CountAsync(item => item.IsSystemGenerated, cancellationToken);
        var missingCorrelation = await logs.CountAsync(item => item.CorrelationId == null || item.CorrelationId == string.Empty, cancellationToken);
        var latestCritical = await logs
            .Where(item => item.Severity == AuditSeverity.Critical)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => (DateTime?)item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var auditRows = await logs.ToArrayAsync(cancellationToken);
        var modules = auditRows
            .GroupBy(item => item.SourceModule)
            .Select(group => new AuditEvidenceSourceModuleResponse(
                group.Key,
                group.Count(),
                group.Count(item => item.Severity == AuditSeverity.Critical),
                group.Count(item => item.Severity == AuditSeverity.High)))
            .OrderByDescending(item => item.EventCount)
            .ThenBy(item => item.SourceModule)
            .Take(8)
            .ToArray();

        var score = 100;
        if (total == 0)
        {
            score -= 40;
        }
        if (critical > 0)
        {
            score -= 30;
        }
        if (missingCorrelation > total / 2 && total > 0)
        {
            score -= 15;
        }
        if (modules.Length == 0)
        {
            score -= 15;
        }
        score = Math.Max(0, score);

        return ServiceResult<AuditEvidenceDashboardResponse>.Success(new AuditEvidenceDashboardResponse(
            query.CompoundId,
            fromUtc,
            now,
            total,
            critical,
            high,
            medium,
            low,
            systemGenerated,
            missingCorrelation,
            latestCritical,
            score,
            modules,
            now));
    }

    public async Task<ServiceResult<ComplianceExceptionQueueResponse>> GetComplianceExceptionQueueAsync(
        ReleaseGovernanceQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateScopeAsync(query, cancellationToken);
        if (validation.Error is not null)
        {
            return ServiceResult<ComplianceExceptionQueueResponse>.Forbidden(validation.Error);
        }

        var now = DateTime.UtcNow;
        var fromUtc = now.AddDays(-NormalizeDays(query.Days));
        var limit = NormalizeLimit(query.ItemLimit);
        var items = new List<ComplianceExceptionItemResponse>();

        var failedNotifications = await ApplyNotificationScope(dbContext.NotificationOutboxes.AsNoTracking(), validation.Scope, query.CompoundId)
            .Where(item => item.Status == NotificationStatus.Failed)
            .OrderByDescending(item => item.FailedAtUtc ?? item.CreatedAtUtc)
            .Take(limit)
            .Select(item => new ComplianceExceptionItemResponse(
                "NotificationOutbox",
                item.Id,
                item.CompoundId,
                item.Priority == NotificationPriority.Urgent ? "Critical" : "High",
                "Failed notification delivery",
                item.LastError ?? item.Subject,
                "Operations",
                "Review provider error, fix recipient data, then retry delivery.",
                item.FailedAtUtc ?? item.CreatedAtUtc,
                item.NextRetryAtUtc))
            .ToArrayAsync(cancellationToken);
        items.AddRange(failedNotifications);

        var integrations = await dbContext.IntegrationFailureEvents.AsNoTracking()
            .Where(item => item.Status == IntegrationFailureStatus.Open || item.Status == IntegrationFailureStatus.Acknowledged)
            .OrderByDescending(item => item.LastOccurredAtUtc)
            .Take(limit)
            .Select(item => new ComplianceExceptionItemResponse(
                "IntegrationFailure",
                item.Id,
                null,
                item.OccurrenceCount >= 3 ? "Critical" : "High",
                item.IntegrationName + " / " + item.OperationName,
                item.ErrorMessage,
                "System",
                "Resolve the integration failure or document an accepted workaround.",
                item.LastOccurredAtUtc,
                null))
            .ToArrayAsync(cancellationToken);
        items.AddRange(integrations);

        var failedJobs = await dbContext.BackgroundJobRuns.AsNoTracking()
            .Where(item => item.StartedAtUtc >= fromUtc && item.Status == BackgroundJobRunStatus.Failed)
            .OrderByDescending(item => item.StartedAtUtc)
            .Take(limit)
            .Select(item => new ComplianceExceptionItemResponse(
                "BackgroundJobRun",
                item.Id,
                null,
                "High",
                item.JobName,
                item.ErrorMessage ?? "Background job failed.",
                "System",
                "Investigate the failure and rerun the job if the workflow is release-critical.",
                item.StartedAtUtc,
                item.CompletedAtUtc))
            .ToArrayAsync(cancellationToken);
        items.AddRange(failedJobs);

        var approvalRows = await ApplyCompoundFilter(dbContext.ApprovalRequests.AsNoTracking(), validation.Scope, query.CompoundId)
            .Where(item => item.Status == ApprovalStatus.Pending)
            .OrderBy(item => item.DueAtUtc ?? item.CreatedAtUtc)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        items.AddRange(approvalRows.Select(item => new ComplianceExceptionItemResponse(
            "ApprovalRequest",
            item.Id,
            item.CompoundId,
            item.Priority == ApprovalPriority.Critical ? "Critical" : item.Priority == ApprovalPriority.High ? "High" : "Medium",
            item.ActionType.ToString(),
            item.Reason,
            "Governance",
            "Approve, reject, cancel, or explicitly defer this request before release.",
            item.CreatedAtUtc,
            item.DueAtUtc)));

        var criticalAuditRows = await ApplyAuditScope(dbContext.AuditLogEntries.AsNoTracking(), validation.Scope, query.CompoundId)
            .Where(item => item.CreatedAtUtc >= fromUtc && (item.Severity == AuditSeverity.Critical || item.Severity == AuditSeverity.High))
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        items.AddRange(criticalAuditRows.Select(item => new ComplianceExceptionItemResponse(
            "AuditLogEntry",
            item.Id,
            item.CompoundId,
            item.Severity == AuditSeverity.Critical ? "Critical" : "High",
            item.ActionType.ToString(),
            item.Description,
            "Audit",
            "Review the audit event and attach release evidence if accepted.",
            item.CreatedAtUtc,
            null)));

        var ordered = items
            .OrderByDescending(item => SeverityRank(item.Severity))
            .ThenByDescending(item => item.CreatedAtUtc)
            .Take(limit)
            .ToArray();

        return ServiceResult<ComplianceExceptionQueueResponse>.Success(new ComplianceExceptionQueueResponse(
            query.CompoundId,
            ordered.Length,
            ordered.Count(item => item.Severity == "Critical"),
            ordered.Count(item => item.Severity == "High"),
            ordered,
            now));
    }

    public async Task<ServiceResult<BuyerHandoffReadinessResponse>> GetBuyerHandoffReadinessAsync(
        ReleaseGovernanceQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateScopeAsync(query, cancellationToken);
        if (validation.Error is not null)
        {
            return ServiceResult<BuyerHandoffReadinessResponse>.Forbidden(validation.Error);
        }

        var now = DateTime.UtcNow;
        var license = await dbContext.LicenseProfiles.AsNoTracking().OrderByDescending(item => item.UpdatedAtUtc ?? item.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        var compounds = await ApplyCompoundFilter(dbContext.Compounds.AsNoTracking(), validation.Scope, query.CompoundId).CountAsync(cancellationToken);
        var units = await ApplyCompoundFilter(dbContext.PropertyUnits.AsNoTracking(), validation.Scope, query.CompoundId).CountAsync(cancellationToken);
        var auditEvents = await ApplyAuditScope(dbContext.AuditLogEntries.AsNoTracking(), validation.Scope, query.CompoundId).CountAsync(cancellationToken);
        var templates = await dbContext.NotificationTemplates.AsNoTracking().CountAsync(cancellationToken);
        var settings = await dbContext.SystemSettings.AsNoTracking().CountAsync(cancellationToken);
        var reportExports = await dbContext.ReportExportJobs.AsNoTracking().CountAsync(cancellationToken);
        var health = await dbContext.SystemHealthSnapshots.AsNoTracking().OrderByDescending(item => item.CapturedAtUtc).FirstOrDefaultAsync(cancellationToken);

        var commercialItems = new[]
        {
            new BuyerHandoffItemResponse("Commercial", "License profile", license is not null && license.Status == LicenseStatus.Active, license is null ? "No license profile." : $"License for {license.LicensedTo} is {license.Status}.", "Activate the buyer license profile."),
            new BuyerHandoffItemResponse("Commercial", "Compound and units configured", compounds > 0 && units > 0, $"{compounds} compounds and {units} units in scope.", "Configure buyer compounds, units, and assignments."),
            new BuyerHandoffItemResponse("Operational", "Notification templates available", templates > 0, $"{templates} notification templates.", "Prepare templates for billing, support, legal, and outage communication."),
            new BuyerHandoffItemResponse("Operational", "Audit evidence available", auditEvents > 0, $"{auditEvents} audit events.", "Run sample operational workflows before handoff."),
            new BuyerHandoffItemResponse("Technical", "System settings configured", settings > 0, $"{settings} system settings.", "Configure production settings, backup notes, maintenance mode, and support references."),
            new BuyerHandoffItemResponse("Technical", "Recent system health captured", health is not null && health.Status != SystemHealthStatus.Unhealthy, health is null ? "No health snapshot." : $"Latest health is {health.Status}.", "Capture a healthy system snapshot during final verification."),
            new BuyerHandoffItemResponse("Commercial", "Reporting/export evidence exists", reportExports > 0, $"{reportExports} report export jobs.", "Generate buyer-facing evidence exports if required.")
        };

        var readyCount = commercialItems.Count(item => item.IsReady);
        var score = (int)Math.Round(readyCount * 100.0 / commercialItems.Length);
        var status = score >= 90 ? "Ready" : score >= 65 ? "Conditional" : "Blocked";
        var notes = commercialItems
            .Where(item => !item.IsReady)
            .Select(item => item.Recommendation)
            .Distinct()
            .ToArray();

        return ServiceResult<BuyerHandoffReadinessResponse>.Success(new BuyerHandoffReadinessResponse(
            query.CompoundId,
            status,
            score,
            commercialItems.Count(item => item.Area == "Commercial" && item.IsReady),
            commercialItems.Count(item => item.Area == "Operational" && item.IsReady),
            commercialItems.Count(item => item.Area == "Technical" && item.IsReady),
            commercialItems,
            notes,
            now));
    }

    public async Task<ServiceResult<GovernanceTimelineResponse>> GetGovernanceTimelineAsync(
        ReleaseGovernanceQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateScopeAsync(query, cancellationToken);
        if (validation.Error is not null)
        {
            return ServiceResult<GovernanceTimelineResponse>.Forbidden(validation.Error);
        }

        var now = DateTime.UtcNow;
        var fromUtc = now.AddDays(-NormalizeDays(query.Days));
        var limit = NormalizeLimit(query.ItemLimit);
        var items = new List<GovernanceTimelineItemResponse>();

        var auditRows = await ApplyAuditScope(dbContext.AuditLogEntries.AsNoTracking(), validation.Scope, query.CompoundId)
            .Where(item => item.CreatedAtUtc >= fromUtc)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        items.AddRange(auditRows.Select(item => new GovernanceTimelineItemResponse(
            "Audit",
            item.Id,
            item.CompoundId,
            item.Severity.ToString(),
            item.ActionType.ToString(),
            item.Description,
            item.CreatedAtUtc,
            item.SourceModule)));

        var integrationRows = await dbContext.IntegrationFailureEvents.AsNoTracking()
            .Where(item => item.LastOccurredAtUtc >= fromUtc)
            .OrderByDescending(item => item.LastOccurredAtUtc)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        items.AddRange(integrationRows.Select(item => new GovernanceTimelineItemResponse(
            "IntegrationFailure",
            item.Id,
            null,
            item.Status == IntegrationFailureStatus.Resolved ? "Low" : "High",
            item.IntegrationName + " / " + item.OperationName,
            item.ErrorMessage,
            item.LastOccurredAtUtc,
            "System")));

        var jobRows = await dbContext.BackgroundJobRuns.AsNoTracking()
            .Where(item => item.StartedAtUtc >= fromUtc)
            .OrderByDescending(item => item.StartedAtUtc)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        items.AddRange(jobRows.Select(item => new GovernanceTimelineItemResponse(
            "BackgroundJob",
            item.Id,
            null,
            item.Status == BackgroundJobRunStatus.Failed ? "High" : "Low",
            item.JobName,
            item.ErrorMessage ?? item.Status.ToString(),
            item.StartedAtUtc,
            "System")));

        var healthRows = await dbContext.SystemHealthSnapshots.AsNoTracking()
            .Where(item => item.CapturedAtUtc >= fromUtc)
            .OrderByDescending(item => item.CapturedAtUtc)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        items.AddRange(healthRows.Select(item => new GovernanceTimelineItemResponse(
            "SystemHealth",
            item.Id,
            null,
            item.Status == SystemHealthStatus.Unhealthy ? "Critical" : item.Status == SystemHealthStatus.Degraded ? "Medium" : "Low",
            item.Status.ToString(),
            item.Summary,
            item.CapturedAtUtc,
            "System")));

        var ordered = items
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(limit)
            .ToArray();

        return ServiceResult<GovernanceTimelineResponse>.Success(new GovernanceTimelineResponse(query.CompoundId, ordered.Length, ordered, now));
    }

    private async Task<(CompoundAccessScope Scope, string? Error)> ValidateScopeAsync(ReleaseGovernanceQuery query, CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return (scope, "Current user cannot access release governance.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return (scope, "Release governance scope was not found.");
        }

        return (scope, null);
    }

    private static int NormalizeDays(int days)
    {
        if (days <= 0)
        {
            return DefaultDays;
        }

        return Math.Min(days, MaxDays);
    }

    private static int NormalizeLimit(int limit)
    {
        if (limit <= 0)
        {
            return DefaultLimit;
        }

        return Math.Min(limit, MaxLimit);
    }

    private static ReleaseReadinessItemResponse BuildReadinessItem(
        string key,
        string area,
        string title,
        bool passed,
        bool required,
        string severity,
        string evidence,
        string recommendation)
    {
        return new ReleaseReadinessItemResponse(key, area, title, passed, required, severity, evidence, recommendation);
    }

    private static IQueryable<Compound> ApplyCompoundFilter(IQueryable<Compound> query, CompoundAccessScope scope, Guid? compoundId)
    {
        if (compoundId.HasValue)
        {
            return query.Where(item => item.Id == compoundId.Value);
        }

        if (scope.IsSuperAdmin)
        {
            return query;
        }

        return query.Where(item => scope.AllowedCompoundIds.Contains(item.Id));
    }

    private static IQueryable<PropertyUnit> ApplyCompoundFilter(IQueryable<PropertyUnit> query, CompoundAccessScope scope, Guid? compoundId)
    {
        if (compoundId.HasValue)
        {
            return query.Where(item => item.CompoundId == compoundId.Value);
        }

        if (scope.IsSuperAdmin)
        {
            return query;
        }

        return query.Where(item => scope.AllowedCompoundIds.Contains(item.CompoundId));
    }

    private static IQueryable<ApprovalRequest> ApplyCompoundFilter(IQueryable<ApprovalRequest> query, CompoundAccessScope scope, Guid? compoundId)
    {
        if (compoundId.HasValue)
        {
            return query.Where(item => item.CompoundId == compoundId.Value);
        }

        if (scope.IsSuperAdmin)
        {
            return query;
        }

        return query.Where(item => scope.AllowedCompoundIds.Contains(item.CompoundId));
    }

    private static IQueryable<NotificationOutbox> ApplyNotificationScope(IQueryable<NotificationOutbox> query, CompoundAccessScope scope, Guid? compoundId)
    {
        if (compoundId.HasValue)
        {
            return query.Where(item => item.CompoundId == compoundId.Value);
        }

        if (scope.IsSuperAdmin)
        {
            return query;
        }

        return query.Where(item => item.CompoundId.HasValue && scope.AllowedCompoundIds.Contains(item.CompoundId.Value));
    }

    private static IQueryable<AuditLogEntry> ApplyAuditScope(IQueryable<AuditLogEntry> query, CompoundAccessScope scope, Guid? compoundId)
    {
        if (compoundId.HasValue)
        {
            return query.Where(item => item.CompoundId == compoundId.Value);
        }

        if (scope.IsSuperAdmin)
        {
            return query;
        }

        return query.Where(item => item.CompoundId.HasValue && scope.AllowedCompoundIds.Contains(item.CompoundId.Value));
    }

    private static int SeverityRank(string severity)
    {
        return severity switch
        {
            "Critical" => 4,
            "High" => 3,
            "Medium" => 2,
            _ => 1
        };
    }

    private static string ResolveOwner(string area)
    {
        return area switch
        {
            "Commercial" => "SuperAdmin",
            "Audit" => "Audit/Admin",
            "System" => "System Admin",
            "Governance" => "Compound Admin",
            _ => "Operations"
        };
    }
}
