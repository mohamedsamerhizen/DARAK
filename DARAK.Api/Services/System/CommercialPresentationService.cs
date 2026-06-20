using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class CommercialPresentationService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService)
    : ICommercialPresentationService
{
    public async Task<ServiceResult<DemoSeedBlueprintResponse>> GetDemoSeedBlueprintAsync(
        CommercialPresentationQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateScopeAsync(query, cancellationToken);
        if (validation.Error is not null)
        {
            return ServiceResult<DemoSeedBlueprintResponse>.Forbidden(validation.Error);
        }

        var metrics = await BuildMetricsAsync(query, validation.Scope, cancellationToken);
        var plans = BuildEntityPlans(metrics);
        var scenarios = BuildScenarios(metrics, query.ScenarioLimit);
        var readyPlans = plans.Count(item => item.SuggestedToCreate == 0);
        var score = ToPercentage(readyPlans, plans.Length);
        var missingEvidence = plans
            .Where(item => item.SuggestedToCreate > 0)
            .Select(item => $"Seed {item.SuggestedToCreate} {item.DisplayName} record(s) to make the {item.Purpose} demo convincing.")
            .Take(12)
            .ToArray();

        var response = new DemoSeedBlueprintResponse(
            query.CompoundId,
            score,
            ToStatus(score),
            plans,
            scenarios,
            missingEvidence,
            [
                "Use a dedicated demo database or a clearly named demo compound.",
                "Never seed fake buyer/demo records into a live production compound.",
                "Keep demo residents, phone numbers, access codes, and payment references obviously synthetic.",
                "Run build, tests, database update, and pending-model checks after demo seed changes.",
                "Demo data is for buyer presentation and training; it must not bypass authorization or audit controls."
            ],
            DateTime.UtcNow);

        return ServiceResult<DemoSeedBlueprintResponse>.Success(response);
    }

    public async Task<ServiceResult<CommercialDemoModeResponse>> GetCommercialDemoModeAsync(
        CommercialPresentationQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateScopeAsync(query, cancellationToken);
        if (validation.Error is not null)
        {
            return ServiceResult<CommercialDemoModeResponse>.Forbidden(validation.Error);
        }

        var metrics = await BuildMetricsAsync(query, validation.Scope, cancellationToken);
        var sections = BuildDemoSections(metrics);
        var readySections = sections.Count(item => item.Status == "Ready");
        var score = ToPercentage(readySections, sections.Length);

        var response = new CommercialDemoModeResponse(
            query.CompoundId,
            score,
            ToStatus(score),
            "DARAK demo mode presents the product as a living residential compound: management starts from executive command, drills into finance, resident lifecycle, access, maintenance, communications, compliance, and then closes with buyer handoff readiness.",
            sections,
            BuildWalkthrough(),
            BuildBuyerQuestions(),
            DateTime.UtcNow);

        return ServiceResult<CommercialDemoModeResponse>.Success(response);
    }

    public async Task<ServiceResult<BuyerPresentationPackResponse>> GetBuyerPresentationPackAsync(
        CommercialPresentationQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateScopeAsync(query, cancellationToken);
        if (validation.Error is not null)
        {
            return ServiceResult<BuyerPresentationPackResponse>.Forbidden(validation.Error);
        }

        var response = new BuyerPresentationPackResponse(
            query.CompoundId,
            "DARAK is a commercial backend platform for operating residential compounds across finance, residents, maintenance, access control, communications, legal follow-up, compliance, and executive intelligence.",
            "The product replaces fragmented Excel, WhatsApp, paper approvals, and isolated accounting workflows with one auditable operational command layer for compound management companies.",
            [
                "Residential compound owners",
                "Property management companies",
                "Facility management operators",
                "Real-estate developers with after-sale operations",
                "Investors evaluating compound management automation"
            ],
            [
                new BuyerDemoAgendaItemResponse(1, "Executive opening", "Start with command center and commercial readiness, not raw Swagger endpoints.", "Buyer immediately sees that the product is operationally organized."),
                new BuyerDemoAgendaItemResponse(2, "Resident and unit story", "Show a resident, their unit, financial pressure, requests, visitors, and communications.", "Buyer understands the daily operational value."),
                new BuyerDemoAgendaItemResponse(3, "Financial control", "Show billing, payments, reconciliation, aging, disputes, and collection follow-up.", "Buyer sees revenue protection and governance."),
                new BuyerDemoAgendaItemResponse(4, "Maintenance and vendors", "Show assets, work orders, preventive maintenance, vendor performance, and inventory signals.", "Buyer sees cost control and reliability."),
                new BuyerDemoAgendaItemResponse(5, "Access and communications", "Show visitors, contractors, guard handoff, announcements, outage updates, and notification reliability.", "Buyer sees resident experience and security."),
                new BuyerDemoAgendaItemResponse(6, "Compliance close", "Show audit, release governance, deployment docs, and buyer handoff material.", "Buyer sees delivery maturity instead of only code."),
            ],
            BuildFeatureBuckets(),
            [
                new BuyerObjectionHandlingResponse("Is this only a backend?", "Yes, intentionally: it is the core system that can power web, mobile, guard screens, and resident portals.", "Show API coverage, modules, tests, and demo-mode endpoints."),
                new BuyerObjectionHandlingResponse("Can this run for a real compound?", "The backend models real operational workflows: residents, units, payments, maintenance, access, legal, communication, compliance, and reporting.", "Show the module registry and clean deployment runbook."),
                new BuyerObjectionHandlingResponse("Is it safe to hand over?", "The project has release governance, audit evidence, authorization tests, environment references, and final hardening checks.", "Show docs/Security-Checklist.md, Testing-Evidence.md, and Production-Readiness-Checklist.md."),
                new BuyerObjectionHandlingResponse("Can it be customized?", "The modules are separated by services/controllers/DTOs, making customer-specific extension possible without rewriting the core.", "Show service registration and module folders."),
            ],
            [
                "docs/Commercial-Handover-Report.md",
                "docs/Commercial-Feature-Matrix.md",
                "docs/Deployment-Runbook.md",
                "docs/Buyer-Handoff.md",
                "docs/Production-Readiness-Checklist.md",
                "docs/Phase2-Commercial-Demo-Buyer-Presentation.md"
            ],
            [
                "Keep a clean demo database snapshot.",
                "Prepare a 12-minute buyer walkthrough script.",
                "Add a minimal Admin Demo Dashboard in the next UI phase.",
                "Keep Swagger as developer evidence, not as the main sales demo.",
                "Do not add more backend features before stabilizing demo and buyer handoff."
            ],
            DateTime.UtcNow);

        return ServiceResult<BuyerPresentationPackResponse>.Success(response);
    }

    private static DemoSeedEntityPlanResponse[] BuildEntityPlans(CommercialPresentationMetrics metrics)
    {
        return [
            Plan("compound", "compound", metrics.Compounds, 1, "commercial opening and scope demonstration"),
            Plan("unit", "property unit", metrics.PropertyUnits, 24, "unit inventory, occupancy, and turnover story"),
            Plan("resident", "resident", metrics.Residents, 12, "resident portal, finance, communication, and risk story"),
            Plan("occupancy", "active occupancy", metrics.ActiveOccupancies, 10, "move-in and ownership/rent relationship story"),
            Plan("bill", "utility/rent bill", metrics.Bills, 24, "billing, aging, dispute, and collection story"),
            Plan("payment", "payment", metrics.Payments, 12, "payment behavior and reconciliation story"),
            Plan("maintenance", "maintenance request/work order", metrics.MaintenanceRecords, 8, "maintenance SLA, asset, vendor, and cost story"),
            Plan("access", "visitor/access record", metrics.AccessRecords, 8, "guard and visitor control story"),
            Plan("communication", "announcement/outage/notification", metrics.CommunicationRecords, 8, "resident communication and outage response story"),
            Plan("legal", "collection/legal record", metrics.LegalRecords, 4, "legal escalation and collections governance story"),
            Plan("compliance", "license/health/compliance record", metrics.ComplianceRecords, 3, "buyer handoff and release governance story")
        ];
    }

    private static DemoSeedEntityPlanResponse Plan(
        string key,
        string displayName,
        int existingCount,
        int minimum,
        string purpose)
    {
        return new DemoSeedEntityPlanResponse(
            key,
            displayName,
            existingCount,
            minimum,
            Math.Max(0, minimum - existingCount),
            purpose);
    }

    private static DemoSeedScenarioResponse[] BuildScenarios(
        CommercialPresentationMetrics metrics,
        int scenarioLimit)
    {
        var limit = Math.Clamp(scenarioLimit <= 0 ? 12 : scenarioLimit, 1, 20);

        return new DemoSeedScenarioResponse[]
        {
            new DemoSeedScenarioResponse("executive-opening", "Executive command opening", "Open with total units, residents, finance pressure, active operations, and readiness evidence.", "compound + units + residents + financial records", metrics.Compounds > 0 && metrics.PropertyUnits >= 1 && metrics.Residents >= 1 && metrics.Bills >= 1),
            new DemoSeedScenarioResponse("finance-collections", "Finance and collections story", "Show unpaid bills, successful payments, reconciliation pressure, collection follow-up, and legal readiness.", "bills + payments + collection/legal records", metrics.Bills >= 2 && metrics.Payments >= 1 && metrics.LegalRecords >= 1),
            new DemoSeedScenarioResponse("maintenance-reliability", "Maintenance reliability story", "Show maintenance demand, work orders, assets, vendors, cost, and escalation signals.", "maintenance requests/work orders + assets", metrics.MaintenanceRecords >= 2),
            new DemoSeedScenarioResponse("guard-access", "Guard and visitor control story", "Show today visitor access, contractor readiness, access audit, and handoff evidence.", "visitor/access records", metrics.AccessRecords >= 2),
            new DemoSeedScenarioResponse("resident-communication", "Resident communications and outages story", "Show a published announcement, active outage, resident notification, and update cadence.", "announcements/outages/notifications", metrics.CommunicationRecords >= 2),
            new DemoSeedScenarioResponse("move-out-turnover", "Move-out and unit turnover story", "Show financial clearance, final meters, custody, damage liability, exit certificate, and unit readiness.", "resident lifecycle and readiness records", metrics.LifecycleRecords >= 1),
            new DemoSeedScenarioResponse("buyer-handoff", "Buyer handoff and release governance", "Show license, release docs, operational runbook, health checks, and final delivery evidence.", "license/system/compliance evidence", metrics.ComplianceRecords >= 1)
        }.Take(limit).ToArray();
    }

    private static CommercialDemoSectionResponse[] BuildDemoSections(CommercialPresentationMetrics metrics)
    {
        return [
            new CommercialDemoSectionResponse("executive", "Executive view", Ready(metrics.Compounds > 0 && metrics.PropertyUnits > 0), "Start by showing DARAK as a command layer, not a CRUD API.", [
                new CommercialDemoSignalResponse("Compounds", metrics.Compounds, "Commercial operating scope"),
                new CommercialDemoSignalResponse("Units", metrics.PropertyUnits, "Real-estate inventory under control"),
                new CommercialDemoSignalResponse("Residents", metrics.Residents, "People affected by every financial and operational decision")
            ]),
            new CommercialDemoSectionResponse("finance", "Financial control", Ready(metrics.Bills > 0 && metrics.Payments > 0), "Show revenue governance: bills, payments, reconciliation, aging, disputes, and collections.", [
                new CommercialDemoSignalResponse("Bills", metrics.Bills, "Billable financial obligations"),
                new CommercialDemoSignalResponse("Payments", metrics.Payments, "Cashflow and settlement evidence"),
                new CommercialDemoSignalResponse("Legal records", metrics.LegalRecords, "Escalation readiness")
            ]),
            new CommercialDemoSectionResponse("operations", "Maintenance and operations", Ready(metrics.MaintenanceRecords > 0), "Show that maintenance is managed through lifecycle, SLA, asset, and vendor evidence.", [
                new CommercialDemoSignalResponse("Maintenance records", metrics.MaintenanceRecords, "Operational workload"),
                new CommercialDemoSignalResponse("Vendors", metrics.Vendors, "Third-party service dependency"),
                new CommercialDemoSignalResponse("Lifecycle records", metrics.LifecycleRecords, "Move-out and turnover readiness")
            ]),
            new CommercialDemoSectionResponse("access", "Access and guard operations", Ready(metrics.AccessRecords > 0), "Show guard-facing control over visitors, credentials, contractor permits, and audit trail.", [
                new CommercialDemoSignalResponse("Access records", metrics.AccessRecords, "Visitor and gate workload")
            ]),
            new CommercialDemoSectionResponse("communication", "Communication and outage response", Ready(metrics.CommunicationRecords > 0), "Show that residents are notified, outages are tracked, and critical messages are visible.", [
                new CommercialDemoSignalResponse("Communication records", metrics.CommunicationRecords, "Resident-facing operational transparency")
            ]),
            new CommercialDemoSectionResponse("delivery", "Commercial handoff", Ready(metrics.ComplianceRecords > 0), "Close by showing test evidence, deployment documentation, release governance, and buyer handoff readiness.", [
                new CommercialDemoSignalResponse("Compliance records", metrics.ComplianceRecords, "Handoff and readiness evidence")
            ])
        ];
    }

    private static CommercialDemoWalkthroughStepResponse[] BuildWalkthrough()
    {
        return [
            new CommercialDemoWalkthroughStepResponse(1, "Open with commercial delivery", "GET /api/admin/commercial-delivery/final-scorecard", "Show final readiness and modules.", "Frames the product as sellable and governed."),
            new CommercialDemoWalkthroughStepResponse(2, "Switch to demo mode", "GET /api/admin/commercial-presentation/demo-mode", "Show demo sections and buyer-facing storyline.", "Prevents a cold technical Swagger-only demo."),
            new CommercialDemoWalkthroughStepResponse(3, "Show financial pressure", "Finance and payment endpoints", "Show bills, payments, reconciliation, aging, and collections.", "Connects DARAK to revenue protection."),
            new CommercialDemoWalkthroughStepResponse(4, "Show resident daily life", "Resident, communication, and portal endpoints", "Show resident data, notifications, disputes, and announcements.", "Shows resident experience management."),
            new CommercialDemoWalkthroughStepResponse(5, "Show maintenance reliability", "Operations and maintenance endpoints", "Show work orders, assets, vendors, inventory, and SLA signals.", "Shows cost and service reliability control."),
            new CommercialDemoWalkthroughStepResponse(6, "Show guard and access", "Access control endpoints", "Show visitors, contractor permits, and gate audit.", "Shows security and entry governance."),
            new CommercialDemoWalkthroughStepResponse(7, "Show handoff evidence", "docs + compliance endpoints", "Show deployment, testing, release, and buyer handoff documentation.", "Reduces buyer risk."),
            new CommercialDemoWalkthroughStepResponse(8, "Close with next roadmap", "docs/Phase2-Commercial-Demo-Buyer-Presentation.md", "Explain Demo UI, 360 Profiles, Intelligence, SaaS, and Ø°ÙƒØ§Ø¡ Ø§Ù„ØªØ±ØªÙŠØ¨ as staged expansions.", "Creates a credible growth path without feature chaos.")
        ];
    }

    private static BuyerQuestionAnswerResponse[] BuildBuyerQuestions()
    {
        return [
            new BuyerQuestionAnswerResponse("What problem does DARAK solve?", "It centralizes residential compound operations across finance, residents, maintenance, access, communications, legal, compliance, and reporting.", "Show the demo-mode sections and module registry."),
            new BuyerQuestionAnswerResponse("How is it better than Excel and WhatsApp?", "It gives scoped, auditable, role-based workflows instead of disconnected manual tracking.", "Show audit, approvals, conversations, and command center endpoints."),
            new BuyerQuestionAnswerResponse("Can a non-technical buyer understand it?", "Yes, the demo mode provides a storyline, talk tracks, readiness signals, and buyer objection handling.", "Show buyer-presentation-pack."),
            new BuyerQuestionAnswerResponse("What is still missing?", "A UI demo, live seed data package, final product branding, and optional SaaS/multi-tenant expansion.", "Show next actions from the buyer presentation pack.")
        ];
    }

    private static BuyerFeatureBucketResponse[] BuildFeatureBuckets()
    {
        return [
            new BuyerFeatureBucketResponse("Financial governance", "Protects revenue and gives management control over receivables, reconciliation, disputes, and collections.", ["Billing", "Payments", "Reconciliation", "Aging", "Disputes", "Collections", "Penalty rules"]),
            new BuyerFeatureBucketResponse("Resident lifecycle", "Controls the full journey from occupancy to move-out, final meters, damage settlement, exit certificate, and unit turnover.", ["Residents", "Occupancy", "Move-out", "Final meters", "Custody", "Damage", "Exit certificate"]),
            new BuyerFeatureBucketResponse("Operations and maintenance", "Turns maintenance from informal requests into assets, work orders, SLA, vendors, inventory, procurement, and reliability reporting.", ["Maintenance", "Assets", "Preventive maintenance", "Vendors", "Inventory", "Procurement", "SLA"]),
            new BuyerFeatureBucketResponse("Access and communications", "Controls entry, visitor flow, contractor access, announcements, outage communication, resident notifications, and conversation workflows.", ["Visitors", "Guard", "Contractors", "Announcements", "Outages", "Notifications", "Conversations"]),
            new BuyerFeatureBucketResponse("Governance and delivery", "Gives the buyer assurance through audit, compliance, documentation, system readiness, release gates, and commercial scorecards.", ["Audit", "Compliance", "Reporting", "System administration", "Release governance", "Commercial delivery"])
        ];
    }

    private async Task<(CompoundAccessScope Scope, string? Error)> ValidateScopeAsync(
        CommercialPresentationQuery query,
        CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return (scope, "Authenticated compound access is required.");
        }

        if (query.CompoundId.HasValue && !scope.CanAccess(query.CompoundId.Value))
        {
            return (scope, "You do not have access to this compound.");
        }

        if (!query.CompoundId.HasValue && !scope.IsSuperAdmin && scope.AllowedCompoundIds.Length == 0)
        {
            return (scope, "No compound access scope is available for this user.");
        }

        return (scope, null);
    }

    private async Task<CommercialPresentationMetrics> BuildMetricsAsync(
        CommercialPresentationQuery query,
        CompoundAccessScope scope,
        CancellationToken cancellationToken)
    {
        var compounds = dbContext.Compounds.AsNoTracking();
        var units = dbContext.PropertyUnits.AsNoTracking();
        var residents = dbContext.ResidentProfiles.AsNoTracking();
        var occupancies = dbContext.OccupancyRecords.AsNoTracking();
        var bills = dbContext.UtilityBills.AsNoTracking();
        var payments = dbContext.Payments.AsNoTracking();
        var maintenance = dbContext.MaintenanceRequests.AsNoTracking();
        var workOrders = dbContext.WorkOrders.AsNoTracking();
        var assets = dbContext.MaintenanceAssets.AsNoTracking();
        var visitors = dbContext.VisitorPasses.AsNoTracking();
        var credentials = dbContext.AccessCredentials.AsNoTracking();
        var permits = dbContext.ContractorWorkPermits.AsNoTracking();
        var announcements = dbContext.Announcements.AsNoTracking();
        var outages = dbContext.UtilityOutages.AsNoTracking();
        var notifications = dbContext.ResidentNotifications.AsNoTracking();
        var collections = dbContext.CollectionCases.AsNoTracking();
        var legalNotices = dbContext.LegalNotices.AsNoTracking();
        var lifecycle = dbContext.ResidentLifecycleProcesses.AsNoTracking();
        var licenses = dbContext.LicenseProfiles.AsNoTracking();
        var health = dbContext.SystemHealthSnapshots.AsNoTracking();
        var jobs = dbContext.BackgroundJobRuns.AsNoTracking();

        if (query.CompoundId.HasValue)
        {
            var compoundId = query.CompoundId.Value;
            compounds = compounds.Where(item => item.Id == compoundId);
            units = units.Where(item => item.CompoundId == compoundId);
            residents = residents.Where(item => item.CompoundId == compoundId);
            occupancies = occupancies.Where(item => item.CompoundId == compoundId);
            bills = bills.Where(item => item.CompoundId == compoundId);
            payments = payments.Where(item => item.CompoundId == compoundId);
            maintenance = maintenance.Where(item => item.CompoundId == compoundId);
            workOrders = workOrders.Where(item => item.CompoundId == compoundId);
            assets = assets.Where(item => item.CompoundId == compoundId);
            visitors = visitors.Where(item => item.CompoundId == compoundId);
            credentials = credentials.Where(item => item.CompoundId == compoundId);
            permits = permits.Where(item => item.CompoundId == compoundId);
            announcements = announcements.Where(item => item.CompoundId == compoundId);
            outages = outages.Where(item => item.CompoundId == compoundId);
            var scopedNotificationUserIds = occupancies.Select(record => record.ResidentProfile.UserId);
            notifications = notifications.Where(item => scopedNotificationUserIds.Contains(item.UserId));
            collections = collections.Where(item => item.CompoundId == compoundId);
            legalNotices = legalNotices.Where(item => item.CompoundId == compoundId);
            lifecycle = lifecycle.Where(item => item.CompoundId == compoundId);
        }
        else if (!scope.IsSuperAdmin)
        {
            var allowed = scope.AllowedCompoundIds;
            compounds = compounds.Where(item => allowed.Contains(item.Id));
            units = units.Where(item => allowed.Contains(item.CompoundId));
            residents = residents.Where(item => allowed.Contains(item.CompoundId));
            occupancies = occupancies.Where(item => allowed.Contains(item.CompoundId));
            bills = bills.Where(item => allowed.Contains(item.CompoundId));
            payments = payments.Where(item => allowed.Contains(item.CompoundId));
            maintenance = maintenance.Where(item => allowed.Contains(item.CompoundId));
            workOrders = workOrders.Where(item => allowed.Contains(item.CompoundId));
            assets = assets.Where(item => allowed.Contains(item.CompoundId));
            visitors = visitors.Where(item => allowed.Contains(item.CompoundId));
            credentials = credentials.Where(item => allowed.Contains(item.CompoundId));
            permits = permits.Where(item => allowed.Contains(item.CompoundId));
            announcements = announcements.Where(item => allowed.Contains(item.CompoundId));
            outages = outages.Where(item => allowed.Contains(item.CompoundId));
            var scopedNotificationUserIds = occupancies.Select(record => record.ResidentProfile.UserId);
            notifications = notifications.Where(item => scopedNotificationUserIds.Contains(item.UserId));
            collections = collections.Where(item => allowed.Contains(item.CompoundId));
            legalNotices = legalNotices.Where(item => allowed.Contains(item.CompoundId));
            lifecycle = lifecycle.Where(item => allowed.Contains(item.CompoundId));
        }

        var billCount = await bills.CountAsync(cancellationToken);
        var paymentCount = await payments.CountAsync(cancellationToken);
        var maintenanceCount = await maintenance.CountAsync(cancellationToken);
        var workOrderCount = await workOrders.CountAsync(cancellationToken);
        var visitorCount = await visitors.CountAsync(cancellationToken);
        var credentialCount = await credentials.CountAsync(cancellationToken);
        var permitCount = await permits.CountAsync(cancellationToken);
        var announcementCount = await announcements.CountAsync(cancellationToken);
        var outageCount = await outages.CountAsync(cancellationToken);
        var notificationCount = await notifications.CountAsync(cancellationToken);
        var collectionCount = await collections.CountAsync(cancellationToken);
        var legalNoticeCount = await legalNotices.CountAsync(cancellationToken);
        var licenseCount = await licenses.CountAsync(cancellationToken);
        var healthCount = await health.CountAsync(cancellationToken);
        var jobCount = await jobs.CountAsync(cancellationToken);

        return new CommercialPresentationMetrics(
            await compounds.CountAsync(cancellationToken),
            await units.CountAsync(cancellationToken),
            await residents.CountAsync(cancellationToken),
            await occupancies.CountAsync(cancellationToken),
            billCount,
            paymentCount,
            maintenanceCount + workOrderCount + await assets.CountAsync(cancellationToken),
            visitorCount + credentialCount + permitCount,
            announcementCount + outageCount + notificationCount,
            collectionCount + legalNoticeCount,
            await lifecycle.CountAsync(cancellationToken),
            await dbContext.ServiceVendors.AsNoTracking().CountAsync(cancellationToken),
            licenseCount + healthCount + jobCount);
    }

    private static int ToPercentage(int value, int total)
    {
        return total <= 0 ? 0 : (int)Math.Round((decimal)value / total * 100, MidpointRounding.AwayFromZero);
    }

    private static string ToStatus(int score)
    {
        return score >= 85 ? "Ready" : score >= 60 ? "Conditional" : "NeedsDemoData";
    }

    private static string Ready(bool isReady)
    {
        return isReady ? "Ready" : "NeedsDemoData";
    }

    private sealed record CommercialPresentationMetrics(
        int Compounds,
        int PropertyUnits,
        int Residents,
        int ActiveOccupancies,
        int Bills,
        int Payments,
        int MaintenanceRecords,
        int AccessRecords,
        int CommunicationRecords,
        int LegalRecords,
        int LifecycleRecords,
        int Vendors,
        int ComplianceRecords);
}

