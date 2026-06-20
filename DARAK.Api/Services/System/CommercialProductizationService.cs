using System.Linq.Expressions;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class CommercialProductizationService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService)
    : ICommercialProductizationService
{
    private const int DefaultDays = 30;
    private const int MaxDays = 365;

    public async Task<ServiceResult<CommercialModuleRegistryResponse>> GetModuleRegistryAsync(
        FinalDeliveryQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateScopeAsync(query, cancellationToken);
        if (validation.Error is not null)
        {
            return ServiceResult<CommercialModuleRegistryResponse>.Forbidden(validation.Error);
        }

        var metrics = await BuildMetricsAsync(query, validation.Scope, cancellationToken);
        var modules = BuildModuleRegistry(metrics);
        var response = new CommercialModuleRegistryResponse(
            query.CompoundId,
            modules.Length,
            modules.Count(item => item.Status == "Ready"),
            modules.Count(item => item.Status == "Conditional"),
            modules.Count(item => item.Status == "Blocked"),
            modules,
            DateTime.UtcNow);

        return ServiceResult<CommercialModuleRegistryResponse>.Success(response);
    }

    public async Task<ServiceResult<ProductCapabilityMapResponse>> GetProductCapabilityMapAsync(
        FinalDeliveryQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateScopeAsync(query, cancellationToken);
        if (validation.Error is not null)
        {
            return ServiceResult<ProductCapabilityMapResponse>.Forbidden(validation.Error);
        }

        var metrics = await BuildMetricsAsync(query, validation.Scope, cancellationToken);
        var capabilities = BuildCapabilities(metrics);
        var response = new ProductCapabilityMapResponse(
            query.CompoundId,
            capabilities.Length,
            capabilities.Count(item => item.Status == "Released"),
            capabilities.Count(item => item.Status == "Conditional"),
            capabilities,
            DateTime.UtcNow);

        return ServiceResult<ProductCapabilityMapResponse>.Success(response);
    }

    public async Task<ServiceResult<BuyerDemoReadinessResponse>> GetBuyerDemoReadinessAsync(
        FinalDeliveryQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateScopeAsync(query, cancellationToken);
        if (validation.Error is not null)
        {
            return ServiceResult<BuyerDemoReadinessResponse>.Forbidden(validation.Error);
        }

        var metrics = await BuildMetricsAsync(query, validation.Scope, cancellationToken);
        var scenarios = BuildDemoScenarios(metrics);
        var ready = scenarios.Count(item => item.IsReady);
        var score = ToPercentage(ready, scenarios.Length);
        var warnings = scenarios
            .Where(item => !item.IsReady)
            .Select(item => item.RiskIfMissing)
            .Distinct()
            .Take(10)
            .ToArray();

        var response = new BuyerDemoReadinessResponse(
            query.CompoundId,
            score,
            ToStatus(score, scenarios.Length - ready),
            ready,
            scenarios.Length - ready,
            scenarios,
            warnings,
            DateTime.UtcNow);

        return ServiceResult<BuyerDemoReadinessResponse>.Success(response);
    }

    public async Task<ServiceResult<ClientOnboardingReadinessResponse>> GetClientOnboardingReadinessAsync(
        FinalDeliveryQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateScopeAsync(query, cancellationToken);
        if (validation.Error is not null)
        {
            return ServiceResult<ClientOnboardingReadinessResponse>.Forbidden(validation.Error);
        }

        var metrics = await BuildMetricsAsync(query, validation.Scope, cancellationToken);
        var steps = BuildOnboardingSteps(metrics);
        var ready = steps.Count(item => item.IsReady);
        var score = ToPercentage(ready, steps.Length);
        var actions = steps
            .Where(item => !item.IsReady)
            .Select(item => item.RequiredAction)
            .Distinct()
            .Take(10)
            .ToArray();

        var response = new ClientOnboardingReadinessResponse(
            query.CompoundId,
            score,
            ToStatus(score, steps.Length - ready),
            ready,
            steps.Length - ready,
            steps,
            actions,
            DateTime.UtcNow);

        return ServiceResult<ClientOnboardingReadinessResponse>.Success(response);
    }

    public async Task<ServiceResult<FinalCommercialDeliveryScorecardResponse>> GetFinalDeliveryScorecardAsync(
        FinalDeliveryQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateScopeAsync(query, cancellationToken);
        if (validation.Error is not null)
        {
            return ServiceResult<FinalCommercialDeliveryScorecardResponse>.Forbidden(validation.Error);
        }

        var metrics = await BuildMetricsAsync(query, validation.Scope, cancellationToken);
        var modules = BuildModuleRegistry(metrics);
        var demos = BuildDemoScenarios(metrics);
        var onboarding = BuildOnboardingSteps(metrics);
        var moduleScore = ToPercentage(modules.Count(item => item.Status == "Ready"), modules.Length);
        var demoScore = ToPercentage(demos.Count(item => item.IsReady), demos.Length);
        var onboardingScore = ToPercentage(onboarding.Count(item => item.IsReady), onboarding.Length);
        var riskPenalty = Math.Min(25, metrics.CriticalAuditEvents * 5 + metrics.FailedNotifications * 2 + metrics.OpenIntegrationFailures * 4 + metrics.FailedJobs24h * 3);
        var finalScore = Math.Clamp((int)Math.Round((moduleScore * 0.35) + (demoScore * 0.35) + (onboardingScore * 0.30)) - riskPenalty, 0, 100);
        var criticalBlockers = modules.Count(item => item.Status == "Blocked")
            + onboarding.Count(item => !item.IsReady && item.StepKey is "license" or "compound-scope")
            + (metrics.OpenIntegrationFailures > 0 ? 1 : 0);
        var warnings = modules.Count(item => item.Status == "Conditional")
            + onboarding.Count(item => !item.IsReady)
            + metrics.FailedNotifications
            + metrics.FailedJobs24h;
        var actions = BuildFinalActions(metrics, modules, demos, onboarding);
        var response = new FinalCommercialDeliveryScorecardResponse(
            query.CompoundId,
            finalScore,
            ToStatus(finalScore, criticalBlockers),
            criticalBlockers,
            warnings,
            modules.Count(item => item.Status == "Ready"),
            modules.Length,
            demos.Count(item => item.IsReady),
            demos.Length,
            metrics.ToFootprint(),
            actions,
            BuildValueSummary(metrics),
            DateTime.UtcNow);

        return ServiceResult<FinalCommercialDeliveryScorecardResponse>.Success(response);
    }

    private async Task<(CompoundAccessScope Scope, string? Error)> ValidateScopeAsync(FinalDeliveryQuery query, CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return (scope, "Current user cannot access commercial delivery closure.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return (scope, "Commercial delivery scope was not found.");
        }

        return (scope, null);
    }

    private async Task<FinalDeliveryMetrics> BuildMetricsAsync(
        FinalDeliveryQuery query,
        CompoundAccessScope scope,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var fromUtc = now.AddDays(-NormalizeDays(query.Days));
        var compounds = ApplyRequiredCompoundScope(dbContext.Compounds.AsNoTracking(), scope, query.CompoundId, item => item.Id);
        var units = ApplyRequiredCompoundScope(dbContext.PropertyUnits.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var residents = ApplyRequiredCompoundScope(dbContext.ResidentProfiles.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var occupancies = ApplyRequiredCompoundScope(dbContext.OccupancyRecords.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var bills = ApplyRequiredCompoundScope(dbContext.UtilityBills.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var payments = ApplyRequiredCompoundScope(dbContext.Payments.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var rentContracts = ApplyRequiredCompoundScope(dbContext.RentContracts.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var saleContracts = ApplyRequiredCompoundScope(dbContext.PropertySaleContracts.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var workOrders = ApplyRequiredCompoundScope(dbContext.WorkOrders.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var assets = ApplyRequiredCompoundScope(dbContext.MaintenanceAssets.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var stockItems = ApplyRequiredCompoundScope(dbContext.StockItems.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var procurementRequests = ApplyRequiredCompoundScope(dbContext.ProcurementRequests.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var visitorPasses = ApplyRequiredCompoundScope(dbContext.VisitorPasses.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var contractorPermits = ApplyRequiredCompoundScope(dbContext.ContractorWorkPermits.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var announcements = ApplyRequiredCompoundScope(dbContext.Announcements.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var outages = ApplyRequiredCompoundScope(dbContext.UtilityOutages.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var conversations = ApplyRequiredCompoundScope(dbContext.Conversations.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var collectionCases = ApplyRequiredCompoundScope(dbContext.CollectionCases.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var legalNotices = ApplyRequiredCompoundScope(dbContext.LegalNotices.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var auditLogs = ApplyNullableCompoundScope(dbContext.AuditLogEntries.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId)
            .Where(item => item.CreatedAtUtc >= fromUtc && item.CreatedAtUtc <= now);
        var notificationOutbox = ApplyNullableCompoundScope(dbContext.NotificationOutboxes.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var approvals = ApplyRequiredCompoundScope(dbContext.ApprovalRequests.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var settings = ApplyNullableCompoundScope(dbContext.SystemSettings.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var reportExports = ApplyNullableCompoundScope(dbContext.ReportExportJobs.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var savedReports = ApplyNullableCompoundScope(dbContext.SavedReports.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId);
        var documents = ApplyRequiredCompoundScope(dbContext.DocumentFiles.AsNoTracking(), scope, query.CompoundId, item => item.CompoundId)
            .Where(item => !item.IsDeleted);

        var license = await dbContext.LicenseProfiles.AsNoTracking()
            .OrderByDescending(item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var activeLicense = license is not null
            && license.Status == LicenseStatus.Active
            && (!license.ExpiresAtUtc.HasValue || license.ExpiresAtUtc.Value > now);

        return new FinalDeliveryMetrics(
            await compounds.CountAsync(cancellationToken),
            await units.CountAsync(cancellationToken),
            await residents.CountAsync(item => item.IsActive, cancellationToken),
            await occupancies.CountAsync(item => item.OccupancyStatus == OccupancyStatus.Active, cancellationToken),
            await bills.CountAsync(cancellationToken),
            await payments.CountAsync(cancellationToken),
            await rentContracts.CountAsync(cancellationToken),
            await saleContracts.CountAsync(cancellationToken),
            await workOrders.CountAsync(cancellationToken),
            await assets.CountAsync(cancellationToken),
            await stockItems.CountAsync(cancellationToken),
            await procurementRequests.CountAsync(cancellationToken),
            await visitorPasses.CountAsync(cancellationToken),
            await contractorPermits.CountAsync(cancellationToken),
            await announcements.CountAsync(cancellationToken),
            await outages.CountAsync(cancellationToken),
            await conversations.CountAsync(cancellationToken),
            await collectionCases.CountAsync(cancellationToken),
            await legalNotices.CountAsync(cancellationToken),
            await auditLogs.CountAsync(cancellationToken),
            await auditLogs.CountAsync(item => item.Severity == AuditSeverity.Critical, cancellationToken),
            await auditLogs.CountAsync(item => item.Severity == AuditSeverity.High, cancellationToken),
            await notificationOutbox.CountAsync(item => item.Status == NotificationStatus.Failed, cancellationToken),
            await dbContext.IntegrationFailureEvents.AsNoTracking().CountAsync(item => item.Status == IntegrationFailureStatus.Open || item.Status == IntegrationFailureStatus.Acknowledged, cancellationToken),
            await dbContext.BackgroundJobRuns.AsNoTracking().CountAsync(item => item.StartedAtUtc >= now.AddHours(-24) && item.Status == BackgroundJobRunStatus.Failed, cancellationToken),
            await approvals.CountAsync(item => item.Status == ApprovalStatus.Pending, cancellationToken),
            await settings.CountAsync(cancellationToken),
            await dbContext.NotificationTemplates.AsNoTracking().CountAsync(cancellationToken),
            await reportExports.CountAsync(cancellationToken),
            await savedReports.CountAsync(item => item.IsActive, cancellationToken),
            await documents.CountAsync(cancellationToken),
            activeLicense,
            license is null ? "No license profile configured." : $"License status: {license.Status}, plan: {license.Plan}." );
    }

    private static CommercialModuleRegistryItemResponse[] BuildModuleRegistry(FinalDeliveryMetrics metrics)
    {
        return
        [
            BuildModule("structure", "Compound Structure & Units", "Core", metrics.Compounds > 0, metrics.PropertyUnits > 0, metrics.Compounds + metrics.PropertyUnits, "Physical inventory of compounds, buildings, units, and operational scope.", "/api/admin/property-structure/units"),
            BuildModule("residents", "Residents & Occupancy", "Core", metrics.Residents > 0, metrics.ActiveOccupancies > 0, metrics.Residents + metrics.ActiveOccupancies, "Resident registry, occupancy state, and buyer-controlled unit ownership/rental context.", "/api/admin/residents"),
            BuildModule("finance", "Financial Closure & Reconciliation", "Finance", metrics.FinancialRecords > 0, metrics.Payments > 0 || metrics.UtilityBills > 0, metrics.FinancialRecords, "Bills, payments, reconciliation, aging, disputes, collection, and financial closure evidence.", "/api/admin/finance/closure-summary"),
            BuildModule("lifecycle", "Resident Exit & Unit Turnover", "Lifecycle", metrics.ActiveOccupancies > 0, metrics.PropertyUnits > 0, metrics.ActiveOccupancies + metrics.PropertyUnits, "Move-out readiness, final meters, custody, damages, exit certificate, and unit turnover.", "/api/admin/resident-lifecycle/move-out-readiness"),
            BuildModule("maintenance", "Maintenance Reliability", "Operations", metrics.MaintenanceAssets > 0 || metrics.WorkOrders > 0, metrics.WorkOrders > 0, metrics.MaintenanceRecords, "Asset reliability, preventive maintenance, SLA, vendor performance, and work order cost visibility.", "/api/admin/maintenance-reliability/pro-dashboard"),
            BuildModule("inventory", "Procurement & Inventory", "Operations", metrics.StockItems > 0 || metrics.ProcurementRequests > 0, metrics.InventoryRecords > 0, metrics.InventoryRecords, "Spare parts availability, consumption, procurement requests, and operational inventory readiness.", "/api/admin/procurement-inventory/spare-parts/availability"),
            BuildModule("legal", "Legal Notices & Case Management", "Legal", metrics.CollectionCases > 0 || metrics.LegalNotices > 0, metrics.LegalRecords > 0, metrics.LegalRecords, "Collection cases, legal notices, escalation readiness, case file, and legal timeline.", "/api/admin/collections-legal-compliance/legal-cases/dashboard"),
            BuildModule("access", "Access, Visitors & Contractors", "Security", metrics.AccessRecords > 0, metrics.VisitorPasses > 0 || metrics.ContractorPermits > 0, metrics.AccessRecords, "Gate situation, visitor verification, contractor compliance, credentials, and guard handover.", "/api/admin/access-control-operations/gate-situation-report"),
            BuildModule("communication", "Communications & Outages", "Communications", metrics.CommunicationRecords > 0, metrics.Announcements > 0 || metrics.Conversations > 0 || metrics.Outages > 0, metrics.CommunicationRecords, "Announcements, outage operations, resident impact, response intelligence, and risk dashboard.", "/api/admin/communication-operations/command-center"),
            BuildModule("executive", "Admin Command Center", "Executive", metrics.GovernanceRecords > 0 || metrics.ComplianceRecords > 0, metrics.AuditEvents > 0, metrics.GovernanceRecords + metrics.ComplianceRecords, "Executive daily summary, domain signal board, critical action queue, and cross-domain decision signals.", "/api/admin/operations/executive-daily-summary"),
            BuildModule("compliance", "Compliance & Release Governance", "Governance", metrics.ComplianceRecords > 0, metrics.AuditEvents > 0 || metrics.SystemSettings > 0, metrics.ComplianceRecords, "Audit evidence, readiness board, exceptions, buyer handoff, and governance timeline.", "/api/admin/compliance-release/readiness-board"),
            BuildModule("delivery", "Commercial Product Delivery", "Commercial", metrics.ActiveLicense, metrics.Compounds > 0 && metrics.PropertyUnits > 0, metrics.Compounds + metrics.PropertyUnits + metrics.SystemSettings, "Final productization, buyer demo readiness, onboarding checklist, and delivery scorecard.", "/api/admin/commercial-delivery/final-scorecard")
        ];
    }

    private static ProductCapabilityItemResponse[] BuildCapabilities(FinalDeliveryMetrics metrics)
    {
        return
        [
            BuildCapability("Finance", "Financial reconciliation and closure governance", metrics.FinancialRecords > 0, $"{metrics.FinancialRecords} financial records available.", "Buyer can inspect collections, disputes, reconciliation, and closure risk from one administrative surface."),
            BuildCapability("Lifecycle", "Resident exit and unit turnover governance", metrics.ActiveOccupancies > 0 && metrics.PropertyUnits > 0, $"{metrics.ActiveOccupancies} active occupancies and {metrics.PropertyUnits} units.", "Buyer can control move-out readiness and unit readiness before the next resident."),
            BuildCapability("Maintenance", "Maintenance reliability and vendor performance", metrics.MaintenanceRecords > 0, $"{metrics.MaintenanceRecords} maintenance reliability records.", "Buyer can prove asset reliability and operational cost discipline."),
            BuildCapability("Legal", "Legal case and notice control", metrics.LegalRecords > 0, $"{metrics.LegalRecords} legal/collection records.", "Buyer can escalate collection cases with traceable legal dossiers."),
            BuildCapability("Security", "Gate, visitor, and contractor control", metrics.AccessRecords > 0, $"{metrics.AccessRecords} access-control records.", "Buyer can operate guard workflows and contractor compliance with a security dashboard."),
            BuildCapability("Communications", "Announcement and utility outage command", metrics.CommunicationRecords > 0, $"{metrics.CommunicationRecords} communication records.", "Buyer can manage resident-wide communications and outages from operational boards."),
            BuildCapability("Executive", "Cross-domain command center", metrics.GovernanceRecords > 0 || metrics.ComplianceRecords > 0, $"{metrics.GovernanceRecords + metrics.ComplianceRecords} governance/compliance records.", "Buyer gets executive signals instead of isolated module screens."),
            BuildCapability("Delivery", "Commercial handoff readiness", metrics.ActiveLicense && metrics.Compounds > 0, metrics.LicenseEvidence, "Buyer sees license, scope, evidence, onboarding, and readiness in final delivery endpoints.")
        ];
    }

    private static BuyerDemoScenarioResponse[] BuildDemoScenarios(FinalDeliveryMetrics metrics)
    {
        return
        [
            BuildScenario("finance-demo", "Show financial closure and risk control", "Finance", metrics.FinancialRecords > 0, $"Financial records: {metrics.FinancialRecords}.", "Open closure summary, aging, dispute control, and collection follow-up.", "Finance demo will look empty without bills/payments/reconciliation evidence."),
            BuildScenario("moveout-demo", "Show move-out readiness through turnover", "Lifecycle", metrics.PropertyUnits > 0 && metrics.ActiveOccupancies > 0, $"Units: {metrics.PropertyUnits}, active occupancies: {metrics.ActiveOccupancies}.", "Open move-out readiness, settlement, exit certificate, and unit turnover timeline.", "Lifecycle demo requires at least one unit and resident occupancy."),
            BuildScenario("maintenance-demo", "Show maintenance reliability board", "Maintenance", metrics.MaintenanceRecords > 0, $"Maintenance records: {metrics.MaintenanceRecords}.", "Open reliability dashboard, SLA queue, vendor performance, and spare part consumption.", "Maintenance demo needs asset/work order evidence."),
            BuildScenario("access-demo", "Show gate and contractor control", "Security", metrics.AccessRecords > 0, $"Access records: {metrics.AccessRecords}.", "Open gate situation report, visitor verification board, contractor compliance, and guard handover.", "Access demo needs visitor or contractor records."),
            BuildScenario("communication-demo", "Show communication and outage operations", "Communications", metrics.CommunicationRecords > 0, $"Communication records: {metrics.CommunicationRecords}.", "Open communication command center, acknowledgements, outage board, and impact report.", "Communication demo needs announcements, conversations, or outages."),
            BuildScenario("legal-demo", "Show legal case management", "Legal", metrics.LegalRecords > 0, $"Legal records: {metrics.LegalRecords}.", "Open legal dashboard, escalation queue, notice service queue, case file, and timeline.", "Legal demo needs collection or legal notice records."),
            BuildScenario("executive-demo", "Show executive daily command center", "Executive", metrics.AuditEvents > 0 || metrics.GovernanceRecords > 0, $"Audit events: {metrics.AuditEvents}, governance records: {metrics.GovernanceRecords}.", "Open executive summary, domain signal board, critical action queue, and release governance.", "Executive demo needs audit/governance evidence."),
            BuildScenario("handoff-demo", "Show buyer handoff and delivery score", "Commercial", metrics.ActiveLicense && metrics.SystemSettings > 0, metrics.LicenseEvidence + $" System settings: {metrics.SystemSettings}.", "Open buyer handoff readiness, onboarding checklist, and final commercial delivery scorecard.", "Handoff demo needs license and configuration evidence.")
        ];
    }

    private static ClientOnboardingStepResponse[] BuildOnboardingSteps(FinalDeliveryMetrics metrics)
    {
        return
        [
            BuildStep("license", "SuperAdmin", "Activate buyer license profile", metrics.ActiveLicense, metrics.LicenseEvidence, "Create/activate a commercial license profile for the buyer."),
            BuildStep("compound-scope", "SuperAdmin", "Create buyer compound scope", metrics.Compounds > 0, $"Compounds: {metrics.Compounds}.", "Create the buyer compound and assign administrators."),
            BuildStep("unit-seed", "Compound Admin", "Seed operational units", metrics.PropertyUnits > 0, $"Property units: {metrics.PropertyUnits}.", "Import or create the buyer property units."),
            BuildStep("resident-seed", "Compound Admin", "Seed resident and occupancy data", metrics.Residents > 0 && metrics.ActiveOccupancies > 0, $"Residents: {metrics.Residents}, active occupancies: {metrics.ActiveOccupancies}.", "Import resident profiles and active occupancy records."),
            BuildStep("system-settings", "System Admin", "Configure production system settings", metrics.SystemSettings > 0, $"System settings: {metrics.SystemSettings}.", "Configure backup, support, notification, and release settings."),
            BuildStep("notification-templates", "System Admin", "Prepare notification templates", metrics.NotificationTemplates > 0, $"Notification templates: {metrics.NotificationTemplates}.", "Create email/SMS templates for critical workflows."),
            BuildStep("reports", "Accountant/Admin", "Prepare buyer-facing reports and exports", metrics.SavedReports > 0 || metrics.ReportExports > 0, $"Saved reports: {metrics.SavedReports}, exports: {metrics.ReportExports}.", "Create saved reports or export evidence for handoff."),
            BuildStep("audit", "Audit/Admin", "Verify audit trail evidence", metrics.AuditEvents > 0, $"Audit events in window: {metrics.AuditEvents}.", "Run representative workflows to generate audit evidence."),
            BuildStep("integrations", "System Admin", "Resolve open integration failures", metrics.OpenIntegrationFailures == 0, $"Open integration failures: {metrics.OpenIntegrationFailures}.", "Resolve or document integration failures before handoff."),
            BuildStep("documents", "Document Admin", "Prepare document evidence", metrics.Documents > 0, $"Documents: {metrics.Documents}.", "Upload buyer handoff documents, contracts, or governance evidence.")
        ];
    }

    private static FinalDeliveryActionResponse[] BuildFinalActions(
        FinalDeliveryMetrics metrics,
        IReadOnlyCollection<CommercialModuleRegistryItemResponse> modules,
        IReadOnlyCollection<BuyerDemoScenarioResponse> demos,
        IReadOnlyCollection<ClientOnboardingStepResponse> onboarding)
    {
        var actions = new List<FinalDeliveryActionResponse>();
        actions.AddRange(modules
            .Where(item => item.Status != "Ready")
            .Select(item => new FinalDeliveryActionResponse(item.Area, item.Status == "Blocked" ? "High" : "Medium", "Complete evidence for " + item.DisplayName + ".", ResolveOwner(item.Area), 0)));
        actions.AddRange(demos
            .Where(item => !item.IsReady)
            .Select(item => new FinalDeliveryActionResponse(item.Module, "Medium", item.RiskIfMissing, ResolveOwner(item.Module), 0)));
        actions.AddRange(onboarding
            .Where(item => !item.IsReady)
            .Select(item => new FinalDeliveryActionResponse("Onboarding", item.StepKey is "license" or "compound-scope" ? "Critical" : "Medium", item.RequiredAction, item.Owner, 0)));

        if (metrics.CriticalAuditEvents > 0)
        {
            actions.Add(new FinalDeliveryActionResponse("Audit", "Critical", "Review critical audit events before buyer delivery.", "Audit/Admin", 0));
        }

        if (metrics.FailedNotifications > 0)
        {
            actions.Add(new FinalDeliveryActionResponse("Notifications", "High", "Resolve failed notification outbox items.", "System Admin", 0));
        }

        if (metrics.OpenIntegrationFailures > 0)
        {
            actions.Add(new FinalDeliveryActionResponse("Integrations", "Critical", "Resolve open integration failures or document accepted limitations.", "System Admin", 0));
        }

        return actions
            .GroupBy(item => new { item.Area, item.Action })
            .Select(group => group.First())
            .OrderByDescending(item => SeverityRank(item.Severity))
            .ThenBy(item => item.Area)
            .Select((item, index) => item with { PriorityRank = index + 1 })
            .Take(20)
            .ToArray();
    }

    private static string[] BuildValueSummary(FinalDeliveryMetrics metrics)
    {
        return
        [
            $"Commercial coverage includes {metrics.Compounds} compounds, {metrics.PropertyUnits} units, and {metrics.Residents} resident profiles.",
            $"Financial evidence footprint includes {metrics.FinancialRecords} bill/payment/contract records.",
            $"Operations footprint includes {metrics.MaintenanceRecords} maintenance records and {metrics.InventoryRecords} inventory/procurement records.",
            $"Security and communication footprint includes {metrics.AccessRecords} access records and {metrics.CommunicationRecords} communication records.",
            $"Governance footprint includes {metrics.GovernanceRecords} governance records and {metrics.ComplianceRecords} compliance records."
        ];
    }

    private static CommercialModuleRegistryItemResponse BuildModule(
        string moduleKey,
        string displayName,
        string area,
        bool hasConfiguration,
        bool hasOperationalEvidence,
        int evidenceCount,
        string commercialValue,
        string buyerDemoPath)
    {
        return new CommercialModuleRegistryItemResponse(
            moduleKey,
            displayName,
            area,
            hasConfiguration,
            hasOperationalEvidence,
            evidenceCount,
            ToModuleStatus(hasConfiguration, hasOperationalEvidence),
            commercialValue,
            buyerDemoPath);
    }

    private static ProductCapabilityItemResponse BuildCapability(
        string area,
        string capability,
        bool released,
        string evidence,
        string buyerValue)
    {
        return new ProductCapabilityItemResponse(area, capability, released ? "Released" : "Conditional", evidence, buyerValue);
    }

    private static BuyerDemoScenarioResponse BuildScenario(
        string scenarioKey,
        string title,
        string module,
        bool isReady,
        string evidence,
        string demoScript,
        string riskIfMissing)
    {
        return new BuyerDemoScenarioResponse(scenarioKey, title, module, isReady, evidence, demoScript, riskIfMissing);
    }

    private static ClientOnboardingStepResponse BuildStep(
        string stepKey,
        string owner,
        string title,
        bool isReady,
        string evidence,
        string requiredAction)
    {
        return new ClientOnboardingStepResponse(stepKey, owner, title, isReady, evidence, requiredAction);
    }

    private static IQueryable<T> ApplyRequiredCompoundScope<T>(
        IQueryable<T> query,
        CompoundAccessScope scope,
        Guid? compoundId,
        Expression<Func<T, Guid>> compoundIdSelector)
    {
        if (compoundId.HasValue)
        {
            var equals = Expression.Equal(compoundIdSelector.Body, Expression.Constant(compoundId.Value));
            return query.Where(Expression.Lambda<Func<T, bool>>(equals, compoundIdSelector.Parameters));
        }

        if (scope.IsSuperAdmin)
        {
            return query;
        }

        if (scope.AllowedCompoundIds.Length == 0)
        {
            return query.Where(_ => false);
        }

        return query.ApplyCompoundAccess(scope, compoundIdSelector);
    }

    private static IQueryable<T> ApplyNullableCompoundScope<T>(
        IQueryable<T> query,
        CompoundAccessScope scope,
        Guid? compoundId,
        Expression<Func<T, Guid?>> compoundIdSelector)
    {
        if (compoundId.HasValue)
        {
            var equals = Expression.Equal(compoundIdSelector.Body, Expression.Constant(compoundId.Value, typeof(Guid?)));
            return query.Where(Expression.Lambda<Func<T, bool>>(equals, compoundIdSelector.Parameters));
        }

        if (scope.IsSuperAdmin)
        {
            return query;
        }

        if (scope.AllowedCompoundIds.Length == 0)
        {
            return query.Where(_ => false);
        }

        var hasValue = Expression.Property(compoundIdSelector.Body, nameof(Nullable<Guid>.HasValue));
        var value = Expression.Property(compoundIdSelector.Body, nameof(Nullable<Guid>.Value));
        var contains = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Contains),
            [typeof(Guid)],
            Expression.Constant(scope.AllowedCompoundIds),
            value);
        var and = Expression.AndAlso(hasValue, contains);
        return query.Where(Expression.Lambda<Func<T, bool>>(and, compoundIdSelector.Parameters));
    }

    private static int NormalizeDays(int days)
    {
        if (days <= 0)
        {
            return DefaultDays;
        }

        return Math.Min(days, MaxDays);
    }

    private static int ToPercentage(int numerator, int denominator)
    {
        if (denominator <= 0)
        {
            return 100;
        }

        return (int)Math.Round(numerator * 100.0 / denominator);
    }

    private static string ToModuleStatus(bool hasConfiguration, bool hasOperationalEvidence)
    {
        if (hasConfiguration && hasOperationalEvidence)
        {
            return "Ready";
        }

        return hasConfiguration ? "Conditional" : "Blocked";
    }

    private static string ToStatus(int score, int blockers)
    {
        if (blockers > 0 || score < 65)
        {
            return "Blocked";
        }

        return score >= 90 ? "Ready" : "Conditional";
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
            "Finance" => "Accountant/Admin",
            "Legal" => "Legal/Admin",
            "Security" => "Guard Supervisor",
            "Communications" => "Compound Admin",
            "Commercial" => "SuperAdmin",
            "Governance" => "System Admin",
            "Onboarding" => "SuperAdmin",
            _ => "Operations"
        };
    }

    private sealed record FinalDeliveryMetrics(
        int Compounds,
        int PropertyUnits,
        int Residents,
        int ActiveOccupancies,
        int UtilityBills,
        int Payments,
        int RentContracts,
        int SaleContracts,
        int WorkOrders,
        int MaintenanceAssets,
        int StockItems,
        int ProcurementRequests,
        int VisitorPasses,
        int ContractorPermits,
        int Announcements,
        int Outages,
        int Conversations,
        int CollectionCases,
        int LegalNotices,
        int AuditEvents,
        int CriticalAuditEvents,
        int HighAuditEvents,
        int FailedNotifications,
        int OpenIntegrationFailures,
        int FailedJobs24h,
        int PendingApprovals,
        int SystemSettings,
        int NotificationTemplates,
        int ReportExports,
        int SavedReports,
        int Documents,
        bool ActiveLicense,
        string LicenseEvidence)
    {
        public int FinancialRecords => UtilityBills + Payments + RentContracts + SaleContracts;

        public int MaintenanceRecords => WorkOrders + MaintenanceAssets;

        public int InventoryRecords => StockItems + ProcurementRequests;

        public int AccessRecords => VisitorPasses + ContractorPermits;

        public int CommunicationRecords => Announcements + Outages + Conversations;

        public int LegalRecords => CollectionCases + LegalNotices;

        public int GovernanceRecords => PendingApprovals + SavedReports + ReportExports;

        public int ComplianceRecords => AuditEvents + SystemSettings + NotificationTemplates + Documents;

        public CommercialDataFootprintResponse ToFootprint()
        {
            return new CommercialDataFootprintResponse(
                Compounds,
                PropertyUnits,
                Residents,
                ActiveOccupancies,
                FinancialRecords,
                MaintenanceRecords,
                AccessRecords,
                CommunicationRecords,
                GovernanceRecords,
                ComplianceRecords);
        }
    }
}
