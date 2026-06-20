using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class IntelligenceEscalationService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService)
    : IIntelligenceEscalationService
{
    public async Task<ServiceResult<IntelligenceEscalationDashboardResponse>> GetCompoundEscalationDashboardAsync(
        Guid compoundId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (compoundId == Guid.Empty)
        {
            return ServiceResult<IntelligenceEscalationDashboardResponse>.BadRequest("Compound id is required.");
        }

        var access = await ValidateCompoundAccessAsync(compoundId, cancellationToken);
        if (access is not null)
        {
            return ServiceResult<IntelligenceEscalationDashboardResponse>.Forbidden(access);
        }

        var exists = await dbContext.Compounds
            .AsNoTracking()
            .AnyAsync(item => item.Id == compoundId, cancellationToken);

        if (!exists)
        {
            return ServiceResult<IntelligenceEscalationDashboardResponse>.NotFound("Compound was not found.");
        }

        var items = await BuildCompoundQueueAsync(compoundId, cancellationToken);
        var ordered = OrderQueue(items).ToArray();
        var normalizedLimit = NormalizeLimit(limit, 20, 100);
        var topItems = ordered.Take(normalizedLimit).ToArray();

        var response = new IntelligenceEscalationDashboardResponse(
            compoundId,
            DateTime.UtcNow,
            ordered.Length,
            CountSeverity(ordered, "Critical"),
            CountSeverity(ordered, "High"),
            CountSeverity(ordered, "Medium"),
            CountSeverity(ordered, "Low"),
            CountArea(ordered, "Financial"),
            CountArea(ordered, "Communication"),
            CountArea(ordered, "Operations"),
            CountArea(ordered, "Legal"),
            CountArea(ordered, "Notification"),
            ordered.Length == 0 ? 0 : (int)Math.Round(ordered.Average(item => item.Score)),
            topItems,
            BuildExecutiveActions(ordered));

        return ServiceResult<IntelligenceEscalationDashboardResponse>.Success(response);
    }

    public async Task<ServiceResult<IntelligenceEscalationQueueResponse>> GetCompoundEscalationQueueAsync(
        Guid compoundId,
        string? area = null,
        string? severity = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (compoundId == Guid.Empty)
        {
            return ServiceResult<IntelligenceEscalationQueueResponse>.BadRequest("Compound id is required.");
        }

        var access = await ValidateCompoundAccessAsync(compoundId, cancellationToken);
        if (access is not null)
        {
            return ServiceResult<IntelligenceEscalationQueueResponse>.Forbidden(access);
        }

        var exists = await dbContext.Compounds
            .AsNoTracking()
            .AnyAsync(item => item.Id == compoundId, cancellationToken);

        if (!exists)
        {
            return ServiceResult<IntelligenceEscalationQueueResponse>.NotFound("Compound was not found.");
        }

        var normalizedLimit = NormalizeLimit(limit, 50, 200);
        var items = await BuildCompoundQueueAsync(compoundId, cancellationToken);
        var filtered = OrderQueue(items)
            .Where(item => MatchesFilter(item.Area, area) && MatchesFilter(item.Severity, severity))
            .Take(normalizedLimit)
            .ToArray();

        var response = new IntelligenceEscalationQueueResponse(
            compoundId,
            area,
            severity,
            filtered.Length,
            filtered,
            DateTime.UtcNow);

        return ServiceResult<IntelligenceEscalationQueueResponse>.Success(response);
    }

    public async Task<ServiceResult<ResidentDecisionBriefResponse>> GetResidentDecisionBriefAsync(
        Guid residentId,
        CancellationToken cancellationToken = default)
    {
        if (residentId == Guid.Empty)
        {
            return ServiceResult<ResidentDecisionBriefResponse>.BadRequest("Resident id is required.");
        }

        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == residentId, cancellationToken);

        if (resident is null)
        {
            return ServiceResult<ResidentDecisionBriefResponse>.NotFound("Resident profile was not found.");
        }

        var access = await ValidateCompoundAccessAsync(resident.CompoundId, cancellationToken);
        if (access is not null)
        {
            return ServiceResult<ResidentDecisionBriefResponse>.Forbidden(access);
        }

        var queue = await BuildCompoundQueueAsync(resident.CompoundId, cancellationToken);
        var related = OrderQueue(queue)
            .Where(item => item.ResidentProfileId == resident.Id)
            .Take(20)
            .ToArray();

        var financialExposure = await CalculateResidentFinancialExposureAsync(resident.Id, cancellationToken);
        var activeRiskFlags = await dbContext.ResidentRiskFlags
            .AsNoTracking()
            .CountAsync(item => item.ResidentProfileId == resident.Id
                && (item.Status == ResidentRiskFlagStatus.Active || item.Status == ResidentRiskFlagStatus.Monitoring), cancellationToken);

        var score = Math.Clamp(
            related.Sum(item => item.Score) + (activeRiskFlags * 8) + (financialExposure > 0 ? 10 : 0),
            0,
            100);

        var response = new ResidentDecisionBriefResponse(
            resident.Id,
            resident.CompoundId,
            resident.FullName,
            DateTime.UtcNow,
            score,
            GetDecisionBand(score),
            financialExposure,
            related.Count(item => item.Area == "Financial" || item.Area == "Legal"),
            related.Count(item => item.Area == "Operations"),
            related.Count(item => item.Area == "Communication" || item.Area == "Notification"),
            activeRiskFlags,
            BuildDecisionBlockers(related, financialExposure, activeRiskFlags),
            BuildResidentRecommendedActions(related, financialExposure, activeRiskFlags),
            related);

        return ServiceResult<ResidentDecisionBriefResponse>.Success(response);
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

    private async Task<IReadOnlyCollection<IntelligenceEscalationQueueItemResponse>> BuildCompoundQueueAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var items = new List<IntelligenceEscalationQueueItemResponse>();

        await AddFinancialEscalationsAsync(items, compoundId, now, today, cancellationToken);
        await AddCommunicationEscalationsAsync(items, compoundId, now, cancellationToken);
        await AddOperationsEscalationsAsync(items, compoundId, now, cancellationToken);
        await AddLegalEscalationsAsync(items, compoundId, now, today, cancellationToken);
        await AddNotificationEscalationsAsync(items, compoundId, now, cancellationToken);

        return items;
    }

    private async Task AddFinancialEscalationsAsync(
        List<IntelligenceEscalationQueueItemResponse> items,
        Guid compoundId,
        DateTime now,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var bills = await dbContext.UtilityBills
            .AsNoTracking()
            .Where(item => item.CompoundId == compoundId
                && item.BillStatus != BillStatus.Paid
                && item.BillStatus != BillStatus.Cancelled
                && item.TotalAmount > item.PaidAmount
                && item.DueDate < today)
            .ToArrayAsync(cancellationToken);

        foreach (var bill in bills)
        {
            var ageHours = Math.Max(0, (today.DayNumber - bill.DueDate.DayNumber) * 24);
            var outstanding = bill.TotalAmount - bill.PaidAmount;
            items.Add(CreateItem(
                $"utility-bill:{bill.Id}",
                "Financial",
                outstanding >= 500000 || ageHours >= 720 ? "High" : "Medium",
                "UtilityBill",
                bill.Id,
                bill.CompoundId,
                bill.ResidentProfileId,
                bill.PropertyUnitId,
                $"Overdue utility bill {bill.BillNumber}",
                $"Outstanding amount {outstanding:N0} IQD is overdue by {ageHours / 24} days.",
                "Review payment status and move to collection follow-up if unpaid.",
                ToEndOfDayUtc(bill.DueDate),
                ageHours));
        }

        var rentInvoices = await dbContext.RentInvoices
            .AsNoTracking()
            .Where(item => item.CompoundId == compoundId
                && item.RentInvoiceStatus != RentInvoiceStatus.Paid
                && item.RentInvoiceStatus != RentInvoiceStatus.Cancelled
                && item.TotalAmount > item.PaidAmount
                && item.DueDate < today)
            .ToArrayAsync(cancellationToken);

        foreach (var invoice in rentInvoices)
        {
            var ageHours = Math.Max(0, (today.DayNumber - invoice.DueDate.DayNumber) * 24);
            var outstanding = invoice.TotalAmount - invoice.PaidAmount;
            items.Add(CreateItem(
                $"rent-invoice:{invoice.Id}",
                "Financial",
                outstanding >= 1000000 || ageHours >= 720 ? "Critical" : "High",
                "RentInvoice",
                invoice.Id,
                invoice.CompoundId,
                invoice.ResidentProfileId,
                invoice.PropertyUnitId,
                $"Overdue rent invoice {invoice.InvoiceNumber}",
                $"Outstanding rent amount {outstanding:N0} IQD is overdue by {ageHours / 24} days.",
                "Prioritize resident account review and collection decision.",
                ToEndOfDayUtc(invoice.DueDate),
                ageHours));
        }

        var disputes = await dbContext.FinancialDisputes
            .AsNoTracking()
            .Where(item => item.CompoundId == compoundId
                && (item.Status == FinancialDisputeStatus.Open
                    || item.Status == FinancialDisputeStatus.UnderReview
                    || item.Status == FinancialDisputeStatus.NeedResidentResponse))
            .ToArrayAsync(cancellationToken);

        foreach (var dispute in disputes)
        {
            var ageHours = GetAgeHours(dispute.CreatedAtUtc, now);
            items.Add(CreateItem(
                $"financial-dispute:{dispute.Id}",
                "Financial",
                ageHours >= 168 ? "High" : "Medium",
                "FinancialDispute",
                dispute.Id,
                dispute.CompoundId,
                dispute.ResidentProfileId,
                null,
                "Financial dispute awaiting controlled decision",
                $"Dispute status is {dispute.Status} and has been open for {ageHours} hours.",
                "Route to finance review and keep resident conversation aligned.",
                dispute.CreatedAtUtc.AddDays(3),
                ageHours));
        }
    }

    private async Task AddCommunicationEscalationsAsync(
        List<IntelligenceEscalationQueueItemResponse> items,
        Guid compoundId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var conversations = await dbContext.Conversations
            .AsNoTracking()
            .Where(item => item.CompoundId == compoundId
                && item.Status != ConversationStatus.Resolved
                && item.Status != ConversationStatus.Closed)
            .ToArrayAsync(cancellationToken);

        foreach (var conversation in conversations)
        {
            var ageHours = GetAgeHours(conversation.LastMessageAtUtc, now);
            var requiresEscalation = conversation.EscalationLevel != ConversationEscalationLevel.None
                || conversation.Priority == ConversationPriority.High
                || conversation.Priority == ConversationPriority.Urgent
                || conversation.Status == ConversationStatus.PendingAdminReply
                || ageHours >= 48;

            if (!requiresEscalation)
            {
                continue;
            }

            items.Add(CreateItem(
                $"conversation:{conversation.Id}",
                "Communication",
                conversation.Priority == ConversationPriority.Urgent || conversation.EscalationLevel == ConversationEscalationLevel.Critical
                    ? "Critical"
                    : ageHours >= 72 || conversation.Priority == ConversationPriority.High
                        ? "High"
                        : "Medium",
                "Conversation",
                conversation.Id,
                conversation.CompoundId,
                conversation.ResidentProfileId,
                conversation.PropertyUnitId,
                "Resident conversation requires attention",
                $"Conversation status is {conversation.Status}, priority is {conversation.Priority}, last message age is {ageHours} hours.",
                "Assign a responsible employee, respond, or escalate with clear ownership.",
                conversation.LastMessageAtUtc.AddDays(2),
                ageHours));
        }
    }

    private async Task AddOperationsEscalationsAsync(
        List<IntelligenceEscalationQueueItemResponse> items,
        Guid compoundId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var maintenanceRequests = await dbContext.MaintenanceRequests
            .AsNoTracking()
            .Where(item => item.CompoundId == compoundId
                && item.Status != MaintenanceStatus.Resolved
                && item.Status != MaintenanceStatus.Closed
                && item.Status != MaintenanceStatus.Cancelled
                && item.Status != MaintenanceStatus.Rejected)
            .ToArrayAsync(cancellationToken);

        foreach (var request in maintenanceRequests)
        {
            var ageHours = GetAgeHours(request.CreatedAt, now);
            var highMaintenancePriority = request.Priority == MaintenancePriority.High || request.Priority == MaintenancePriority.Emergency;
            if (!highMaintenancePriority && ageHours < 72)
            {
                continue;
            }

            items.Add(CreateItem(
                $"maintenance-request:{request.Id}",
                "Operations",
                request.Priority == MaintenancePriority.Emergency || ageHours >= 168 ? "Critical" : highMaintenancePriority ? "High" : "Medium",
                "MaintenanceRequest",
                request.Id,
                request.CompoundId,
                request.ResidentProfileId,
                request.PropertyUnitId,
                request.Title,
                $"Maintenance request priority is {request.Priority}, status is {request.Status}, age is {ageHours} hours.",
                "Confirm assignment, work status, and resident communication.",
                request.CreatedAt.AddDays(3),
                ageHours));
        }

        var workOrders = await dbContext.WorkOrders
            .AsNoTracking()
            .Where(item => item.CompoundId == compoundId
                && item.Status != WorkOrderStatus.Completed
                && item.Status != WorkOrderStatus.Cancelled)
            .ToArrayAsync(cancellationToken);

        foreach (var workOrder in workOrders)
        {
            var dueAt = workOrder.ResolutionDueAtUtc ?? workOrder.DueAtUtc ?? workOrder.CreatedAtUtc.AddDays(3);
            var ageHours = GetAgeHours(workOrder.CreatedAtUtc, now);
            var overdue = dueAt < now;
            var highPriority = workOrder.Priority == WorkOrderPriority.High
                || workOrder.Priority == WorkOrderPriority.Urgent
                || workOrder.Priority == WorkOrderPriority.Emergency;

            if (!overdue && !highPriority)
            {
                continue;
            }

            items.Add(CreateItem(
                $"work-order:{workOrder.Id}",
                "Operations",
                workOrder.Priority == WorkOrderPriority.Emergency || overdue && ageHours >= 168 ? "Critical" : highPriority ? "High" : "Medium",
                "WorkOrder",
                workOrder.Id,
                workOrder.CompoundId,
                null,
                workOrder.PropertyUnitId,
                workOrder.Title,
                $"Work order status is {workOrder.Status}, priority is {workOrder.Priority}, due time is {dueAt:u}.",
                "Escalate to operations owner and verify SLA response/resolution.",
                dueAt,
                ageHours));
        }

        var supportCases = await dbContext.SupportCases
            .AsNoTracking()
            .Where(item => item.CompoundId == compoundId
                && item.Status != SupportCaseStatus.Resolved
                && item.Status != SupportCaseStatus.Closed
                && item.Status != SupportCaseStatus.Cancelled)
            .ToArrayAsync(cancellationToken);

        foreach (var supportCase in supportCases)
        {
            var ageHours = GetAgeHours(supportCase.CreatedAtUtc, now);
            var due = supportCase.DueAtUtc <= now;
            var highPriority = supportCase.Priority == SupportCasePriority.High
                || supportCase.Priority == SupportCasePriority.Urgent
                || supportCase.Priority == SupportCasePriority.Critical;

            if (!due && !highPriority)
            {
                continue;
            }

            items.Add(CreateItem(
                $"support-case:{supportCase.Id}",
                "Operations",
                supportCase.Priority == SupportCasePriority.Critical || due && ageHours >= 72 ? "High" : "Medium",
                "SupportCase",
                supportCase.Id,
                supportCase.CompoundId,
                supportCase.ResidentProfileId,
                supportCase.PropertyUnitId,
                supportCase.Title,
                $"Support case status is {supportCase.Status}, priority is {supportCase.Priority}, due time is {supportCase.DueAtUtc:u}.",
                "Assign or escalate support ownership before customer experience degrades.",
                supportCase.DueAtUtc,
                ageHours));
        }
    }

    private async Task AddLegalEscalationsAsync(
        List<IntelligenceEscalationQueueItemResponse> items,
        Guid compoundId,
        DateTime now,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var cases = await dbContext.CollectionCases
            .AsNoTracking()
            .Where(item => item.CompoundId == compoundId
                && item.Status != CollectionCaseStatus.Settled
                && item.Status != CollectionCaseStatus.Closed
                && item.Status != CollectionCaseStatus.Cancelled)
            .ToArrayAsync(cancellationToken);

        foreach (var collectionCase in cases)
        {
            var stageIsLegal = collectionCase.Stage == CollectionStage.FinalNotice || collectionCase.Stage == CollectionStage.LegalReview;
            var overdue = collectionCase.DueDate.HasValue && collectionCase.DueDate.Value < today;
            if (!stageIsLegal && !overdue)
            {
                continue;
            }

            var openedAge = GetAgeHours(collectionCase.OpenedAtUtc, now);
            items.Add(CreateItem(
                $"collection-case:{collectionCase.Id}",
                "Legal",
                collectionCase.Stage == CollectionStage.LegalReview || collectionCase.Status == CollectionCaseStatus.LegalEscalated ? "Critical" : "High",
                "CollectionCase",
                collectionCase.Id,
                collectionCase.CompoundId,
                collectionCase.ResidentProfileId,
                null,
                "Collection case requires escalation decision",
                $"Collection case stage is {collectionCase.Stage}, status is {collectionCase.Status}, amount due is {collectionCase.AmountDue:N0} {collectionCase.Currency}.",
                "Review legal notice readiness, payment plan option, and approval requirements.",
                collectionCase.DueDate.HasValue ? ToEndOfDayUtc(collectionCase.DueDate.Value) : collectionCase.OpenedAtUtc.AddDays(7),
                openedAge));
        }

        var notices = await dbContext.LegalNotices
            .AsNoTracking()
            .Where(item => item.CompoundId == compoundId
                && item.Status != LegalNoticeStatus.Acknowledged
                && item.Status != LegalNoticeStatus.Cancelled)
            .ToArrayAsync(cancellationToken);

        foreach (var notice in notices)
        {
            var overdue = notice.DeadlineDate.HasValue && notice.DeadlineDate.Value < today;
            if (!overdue && notice.Status != LegalNoticeStatus.Issued)
            {
                continue;
            }

            var ageHours = GetAgeHours(notice.CreatedAtUtc, now);
            items.Add(CreateItem(
                $"legal-notice:{notice.Id}",
                "Legal",
                overdue ? "High" : "Medium",
                "LegalNotice",
                notice.Id,
                notice.CompoundId,
                notice.ResidentProfileId,
                null,
                notice.Title,
                $"Legal notice status is {notice.Status} and deadline is {notice.DeadlineDate?.ToString() ?? "not set"}.",
                "Confirm delivery evidence, deadline handling, and next legal step.",
                notice.DeadlineDate.HasValue ? ToEndOfDayUtc(notice.DeadlineDate.Value) : notice.CreatedAtUtc.AddDays(3),
                ageHours));
        }
    }

    private async Task AddNotificationEscalationsAsync(
        List<IntelligenceEscalationQueueItemResponse> items,
        Guid compoundId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var notifications = await dbContext.NotificationOutboxes
            .AsNoTracking()
            .Where(item => item.CompoundId == compoundId
                && (item.Status == NotificationStatus.Failed
                    || item.Status == NotificationStatus.Pending
                    || item.Status == NotificationStatus.Processing))
            .ToArrayAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            var overdue = notification.ScheduledAtUtc < now.AddHours(-2);
            var failed = notification.Status == NotificationStatus.Failed;
            var urgent = notification.Priority == NotificationPriority.High || notification.Priority == NotificationPriority.Urgent;

            if (!failed && !overdue && !urgent)
            {
                continue;
            }

            var ageHours = GetAgeHours(notification.CreatedAtUtc, now);
            items.Add(CreateItem(
                $"notification:{notification.Id}",
                "Notification",
                failed && urgent ? "High" : failed || overdue ? "Medium" : "Low",
                "NotificationOutbox",
                notification.Id,
                compoundId,
                notification.ResidentProfileId,
                null,
                notification.Subject,
                $"Notification status is {notification.Status}, priority is {notification.Priority}, retry count is {notification.RetryCount}.",
                "Review delivery configuration and retry or cancel with audit trail.",
                notification.ScheduledAtUtc,
                ageHours));
        }
    }

    private async Task<decimal> CalculateResidentFinancialExposureAsync(Guid residentId, CancellationToken cancellationToken)
    {
        var utilityExposure = await dbContext.UtilityBills
            .AsNoTracking()
            .Where(item => item.ResidentProfileId == residentId
                && item.BillStatus != BillStatus.Paid
                && item.BillStatus != BillStatus.Cancelled)
            .SumAsync(item => item.TotalAmount - item.PaidAmount, cancellationToken);

        var rentExposure = await dbContext.RentInvoices
            .AsNoTracking()
            .Where(item => item.ResidentProfileId == residentId
                && item.RentInvoiceStatus != RentInvoiceStatus.Paid
                && item.RentInvoiceStatus != RentInvoiceStatus.Cancelled)
            .SumAsync(item => item.TotalAmount - item.PaidAmount, cancellationToken);

        return utilityExposure + rentExposure;
    }

    private static IntelligenceEscalationQueueItemResponse CreateItem(
        string queueKey,
        string area,
        string severity,
        string entityType,
        Guid entityId,
        Guid compoundId,
        Guid? residentProfileId,
        Guid? propertyUnitId,
        string title,
        string reason,
        string recommendedAction,
        DateTime? dueAtUtc,
        int ageHours)
    {
        var score = Math.Clamp(GetSeverityWeight(severity) + Math.Min(ageHours / 24, 20), 1, 100);
        return new IntelligenceEscalationQueueItemResponse(
            queueKey,
            area,
            severity,
            entityType,
            entityId,
            compoundId,
            residentProfileId,
            propertyUnitId,
            title,
            reason,
            recommendedAction,
            dueAtUtc,
            ageHours,
            score);
    }

    private static IEnumerable<IntelligenceEscalationQueueItemResponse> OrderQueue(
        IEnumerable<IntelligenceEscalationQueueItemResponse> items)
    {
        return items
            .OrderByDescending(item => GetSeverityWeight(item.Severity))
            .ThenByDescending(item => item.Score)
            .ThenBy(item => item.DueAtUtc ?? DateTime.MaxValue)
            .ThenByDescending(item => item.AgeHours);
    }

    private static IReadOnlyCollection<string> BuildExecutiveActions(
        IReadOnlyCollection<IntelligenceEscalationQueueItemResponse> items)
    {
        var actions = new List<string>();

        if (items.Any(item => item.Severity == "Critical"))
        {
            actions.Add("Open an executive review for all critical financial, legal, and operations escalations today.");
        }

        if (items.Count(item => item.Area == "Financial" || item.Area == "Legal") >= 3)
        {
            actions.Add("Run a finance and legal collection huddle before sending additional notices.");
        }

        if (items.Count(item => item.Area == "Communication") >= 3)
        {
            actions.Add("Rebalance conversation assignments to a named responsible employee.");
        }

        if (items.Count(item => item.Area == "Operations") >= 3)
        {
            actions.Add("Audit overdue maintenance and support ownership with SLA evidence.");
        }

        if (items.Count(item => item.Area == "Notification") > 0)
        {
            actions.Add("Verify notification delivery reliability before relying on automated reminders.");
        }

        if (actions.Count == 0)
        {
            actions.Add("No urgent escalation cluster detected. Continue daily review cadence.");
        }

        return actions;
    }

    private static IReadOnlyCollection<string> BuildDecisionBlockers(
        IReadOnlyCollection<IntelligenceEscalationQueueItemResponse> related,
        decimal financialExposure,
        int activeRiskFlags)
    {
        var blockers = new List<string>();

        if (financialExposure > 0)
        {
            blockers.Add("Resident has unresolved financial exposure.");
        }

        if (activeRiskFlags > 0)
        {
            blockers.Add("Resident has active or monitoring risk flags.");
        }

        if (related.Any(item => item.Severity == "Critical"))
        {
            blockers.Add("Resident is linked to at least one critical escalation.");
        }

        if (related.Any(item => item.Area == "Legal"))
        {
            blockers.Add("Resident has legal or collection workflow dependency.");
        }

        return blockers;
    }

    private static IReadOnlyCollection<string> BuildResidentRecommendedActions(
        IReadOnlyCollection<IntelligenceEscalationQueueItemResponse> related,
        decimal financialExposure,
        int activeRiskFlags)
    {
        var actions = new List<string>();

        if (financialExposure > 0)
        {
            actions.Add("Review account statement and decide payment plan, dispute closure, or collection escalation.");
        }

        if (activeRiskFlags > 0)
        {
            actions.Add("Review active risk flags before approving service, move-out, or financial exceptions.");
        }

        if (related.Any(item => item.Area == "Communication"))
        {
            actions.Add("Resolve pending resident communication before making final administrative decisions.");
        }

        if (related.Any(item => item.Area == "Operations"))
        {
            actions.Add("Confirm maintenance/support blockers and assign accountable staff owner.");
        }

        if (actions.Count == 0)
        {
            actions.Add("Resident is clear for normal workflow handling.");
        }

        return actions;
    }

    private static string GetDecisionBand(int score)
    {
        if (score >= 80)
        {
            return "Critical";
        }

        if (score >= 55)
        {
            return "High";
        }

        if (score >= 30)
        {
            return "Watch";
        }

        return "Clear";
    }

    private static int CountSeverity(IEnumerable<IntelligenceEscalationQueueItemResponse> items, string severity)
    {
        return items.Count(item => item.Severity == severity);
    }

    private static int CountArea(IEnumerable<IntelligenceEscalationQueueItemResponse> items, string area)
    {
        return items.Count(item => item.Area == area);
    }

    private static int GetSeverityWeight(string severity)
    {
        return severity switch
        {
            "Critical" => 80,
            "High" => 55,
            "Medium" => 30,
            "Low" => 10,
            _ => 1
        };
    }

    private static bool MatchesFilter(string value, string? filter)
    {
        return string.IsNullOrWhiteSpace(filter)
            || value.Equals(filter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static int NormalizeLimit(int limit, int defaultLimit, int maxLimit)
    {
        if (limit <= 0)
        {
            return defaultLimit;
        }

        return Math.Clamp(limit, 1, maxLimit);
    }

    private static int GetAgeHours(DateTime createdAtUtc, DateTime nowUtc)
    {
        return Math.Max(0, (int)Math.Floor((nowUtc - createdAtUtc).TotalHours));
    }

    private static DateTime ToEndOfDayUtc(DateOnly date)
    {
        return date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
    }
}
