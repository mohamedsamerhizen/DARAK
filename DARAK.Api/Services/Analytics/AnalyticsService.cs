using DARAK.Api.Data;
using DARAK.Api.DTOs.Analytics;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class AnalyticsService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IAnalyticsService
{
    public async Task<ServiceResult<AdminDashboardSummaryResponse>> GetAdminDashboardSummaryAsync(
        Guid? currentUserId,
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateDateRange(query)
            ?? await ValidateCompoundAccessAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<AdminDashboardSummaryResponse>(validation);
        }

        var now = DateTime.UtcNow;
        var residents = ApplyDateRange(dbContext.ResidentProfiles.AsNoTracking(), query);
        var totalUsers = await residents
            .Select(profile => profile.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
        var units = ApplyDateRange(dbContext.PropertyUnits.AsNoTracking(), query);
        var bills = ApplyDateRange(dbContext.UtilityBills.AsNoTracking(), query);
        var payments = ApplyDateRange(dbContext.Payments.AsNoTracking(), query);
        var maintenanceRequests = ApplyDateRange(dbContext.MaintenanceRequests.AsNoTracking(), query);
        var complaints = ApplyDateRange(dbContext.Complaints.AsNoTracking(), query);
        var violations = ApplyDateRange(dbContext.Violations.AsNoTracking(), query);
        var visitorPasses = ApplyDateRange(dbContext.VisitorPasses.AsNoTracking(), query);
        var documents = ApplyDateRange(dbContext.DocumentFiles.AsNoTracking(), query)
            .Where(document => !document.IsDeleted);
        var workOrders = ApplyDateRange(dbContext.WorkOrders.AsNoTracking(), query);

        var unreadNotifications = currentUserId.HasValue
            ? await ApplyDateRange(dbContext.ResidentNotifications.AsNoTracking(), query)
                .CountAsync(notification =>
                    notification.UserId == currentUserId.Value && !notification.IsRead,
                    cancellationToken)
            : 0;

        var response = new AdminDashboardSummaryResponse(
            totalUsers,
            await residents.CountAsync(cancellationToken),
            await units.CountAsync(cancellationToken),
            await units.CountAsync(unit =>
                unit.UnitStatus == UnitStatus.Occupied
                || unit.UnitStatus == UnitStatus.Rented
                || unit.UnitStatus == UnitStatus.SoldCash
                || unit.UnitStatus == UnitStatus.SoldInstallment,
                cancellationToken),
            await units.CountAsync(unit => unit.UnitStatus == UnitStatus.Available, cancellationToken),
            await bills.CountAsync(cancellationToken),
            await bills.CountAsync(bill => bill.BillStatus == BillStatus.Paid, cancellationToken),
            await bills.CountAsync(bill =>
                bill.BillStatus == BillStatus.Unpaid
                || bill.BillStatus == BillStatus.PartiallyPaid,
                cancellationToken),
            await bills.CountAsync(bill => bill.BillStatus == BillStatus.Overdue, cancellationToken),
            await payments.CountAsync(cancellationToken),
            await SumDecimalAsync(
                payments.Where(payment => payment.PaymentStatus == PaymentStatus.Succeeded)
                    .Select(payment => (decimal?)payment.Amount),
                cancellationToken),
            await maintenanceRequests.CountAsync(IsOpenMaintenanceRequest(), cancellationToken),
            await complaints.CountAsync(IsOpenComplaint(), cancellationToken),
            await CountOpenViolationsAsync(violations, cancellationToken),
            await visitorPasses.CountAsync(pass => pass.Status == VisitorPassStatus.Pending, cancellationToken),
            unreadNotifications,
            await documents.CountAsync(cancellationToken),
            await workOrders.CountAsync(IsOpenWorkOrder(), cancellationToken),
            await CountOverdueWorkOrdersAsync(workOrders, now, cancellationToken));

        return ServiceResult<AdminDashboardSummaryResponse>.Success(response);
    }

    public async Task<ServiceResult<FinancialReportResponse>> GetFinancialReportAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateDateRange(query)
            ?? await ValidateCompoundAccessAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<FinancialReportResponse>(validation);
        }

        var bills = ApplyDateRange(dbContext.UtilityBills.AsNoTracking(), query)
            .Where(bill => bill.BillStatus != BillStatus.Cancelled);
        var payments = ApplyDateRange(dbContext.Payments.AsNoTracking(), query);
        var succeededPayments = payments.Where(payment => payment.PaymentStatus == PaymentStatus.Succeeded);

        var paymentsByStatusGroups = await payments
            .GroupBy(payment => payment.PaymentStatus)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count(),
                Amount = group.Sum(payment => payment.Amount)
            })
            .ToArrayAsync(cancellationToken);
        var paymentsByTargetTypeGroups = await payments
            .GroupBy(payment => payment.TargetType)
            .Select(group => new
            {
                TargetType = group.Key,
                Count = group.Count(),
                Amount = group.Sum(payment => payment.Amount)
            })
            .ToArrayAsync(cancellationToken);

        var totalBilledAmount = await SumDecimalAsync(
            bills.Select(bill => (decimal?)bill.TotalAmount),
            cancellationToken);
        var totalOutstandingAmount = await SumDecimalAsync(
            bills.Select(bill => (decimal?)(bill.TotalAmount - bill.PaidAmount)),
            cancellationToken);

        var response = new FinancialReportResponse(
            totalBilledAmount,
            await SumDecimalAsync(succeededPayments.Select(payment => (decimal?)payment.Amount), cancellationToken),
            totalOutstandingAmount,
            await bills.CountAsync(bill => bill.BillStatus == BillStatus.Paid, cancellationToken),
            await bills.CountAsync(bill =>
                bill.BillStatus == BillStatus.Unpaid
                || bill.BillStatus == BillStatus.PartiallyPaid,
                cancellationToken),
            await bills.CountAsync(bill => bill.BillStatus == BillStatus.Overdue, cancellationToken),
            await payments.CountAsync(cancellationToken),
            paymentsByStatusGroups
                .OrderBy(group => group.Status)
                .Select(group => new ChartPointResponse(group.Status.ToString(), group.Count, group.Amount))
                .ToArray(),
            paymentsByTargetTypeGroups
                .OrderBy(group => group.TargetType)
                .Select(group => new ChartPointResponse(group.TargetType.ToString(), group.Count, group.Amount))
                .ToArray());

        return ServiceResult<FinancialReportResponse>.Success(response);
    }

    public async Task<ServiceResult<MaintenanceOperationsReportResponse>> GetMaintenanceOperationsReportAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateDateRange(query)
            ?? await ValidateCompoundAccessAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<MaintenanceOperationsReportResponse>(validation);
        }

        var now = DateTime.UtcNow;
        var maintenanceRequests = ApplyDateRange(dbContext.MaintenanceRequests.AsNoTracking(), query);
        var workOrders = ApplyDateRange(dbContext.WorkOrders.AsNoTracking(), query);
        var averageCompletionMinutes = await maintenanceRequests
            .Where(request => request.ResolvedAt.HasValue || request.ClosedAt.HasValue)
            .Select(request => (double?)EF.Functions.DateDiffMinute(
                request.CreatedAt,
                request.ClosedAt ?? request.ResolvedAt!.Value))
            .AverageAsync(cancellationToken);

        var response = new MaintenanceOperationsReportResponse(
            await maintenanceRequests.CountAsync(cancellationToken),
            await maintenanceRequests.CountAsync(request => request.Status == MaintenanceStatus.Open, cancellationToken),
            await maintenanceRequests.CountAsync(request => request.Status == MaintenanceStatus.InProgress, cancellationToken),
            await maintenanceRequests.CountAsync(request =>
                request.Status == MaintenanceStatus.Resolved
                || request.Status == MaintenanceStatus.Closed,
                cancellationToken),
            await maintenanceRequests.CountAsync(request => request.Status == MaintenanceStatus.Cancelled, cancellationToken),
            averageCompletionMinutes.HasValue
                ? Math.Round((decimal)averageCompletionMinutes.Value / 60m, 2, MidpointRounding.AwayFromZero)
                : null,
            await workOrders.CountAsync(cancellationToken),
            await workOrders.CountAsync(IsOpenWorkOrder(), cancellationToken),
            await workOrders.CountAsync(workOrder => workOrder.Status == WorkOrderStatus.Completed, cancellationToken),
            await CountOverdueWorkOrdersAsync(workOrders, now, cancellationToken),
            await SumDecimalAsync(workOrders.Select(workOrder => workOrder.EstimatedCost), cancellationToken),
            await SumDecimalAsync(workOrders.Select(workOrder => workOrder.ActualCost), cancellationToken));

        return ServiceResult<MaintenanceOperationsReportResponse>.Success(response);
    }

    public async Task<ServiceResult<CommunityReportResponse>> GetCommunityReportAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateDateRange(query)
            ?? await ValidateCompoundAccessAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<CommunityReportResponse>(validation);
        }

        var now = DateTime.UtcNow;
        var announcements = ApplyDateRange(dbContext.Announcements.AsNoTracking(), query);
        var polls = ApplyDateRange(dbContext.CommunityPolls.AsNoTracking(), query);
        var pollVotes = ApplyDateRange(dbContext.CommunityPollVotes.AsNoTracking(), query);
        var notifications = ApplyDateRange(dbContext.ResidentNotifications.AsNoTracking(), query);
        var complaints = ApplyDateRange(dbContext.Complaints.AsNoTracking(), query);
        var violations = ApplyDateRange(dbContext.Violations.AsNoTracking(), query);

        var response = new CommunityReportResponse(
            await announcements.CountAsync(cancellationToken),
            await announcements.CountAsync(announcement => announcement.Status == AnnouncementStatus.Published, cancellationToken),
            await polls.CountAsync(poll =>
                poll.Status == CommunityPollStatus.Open
                && poll.StartsAt <= now
                && poll.EndsAt >= now,
                cancellationToken),
            await pollVotes.CountAsync(cancellationToken),
            await notifications.CountAsync(cancellationToken),
            await notifications.CountAsync(notification => !notification.IsRead, cancellationToken),
            await complaints.CountAsync(cancellationToken),
            await complaints.CountAsync(IsOpenComplaint(), cancellationToken),
            await violations.CountAsync(cancellationToken),
            await CountOpenViolationsAsync(violations, cancellationToken));

        return ServiceResult<CommunityReportResponse>.Success(response);
    }


    public async Task<ServiceResult<VisitorsReportResponse>> GetVisitorsReportAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateDateRange(query)
            ?? await ValidateCompoundAccessAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<VisitorsReportResponse>(validation);
        }

        var visitorPasses = ApplyDateRange(dbContext.VisitorPasses.AsNoTracking(), query);
        var response = new VisitorsReportResponse(
            await visitorPasses.CountAsync(cancellationToken),
            await visitorPasses.CountAsync(pass => pass.Status == VisitorPassStatus.Pending, cancellationToken),
            await visitorPasses.CountAsync(pass => pass.Status == VisitorPassStatus.Approved, cancellationToken),
            await visitorPasses.CountAsync(pass => pass.Status == VisitorPassStatus.CheckedIn, cancellationToken),
            await visitorPasses.CountAsync(pass => pass.Status == VisitorPassStatus.CheckedOut, cancellationToken),
            await visitorPasses.CountAsync(pass => pass.Status == VisitorPassStatus.Denied, cancellationToken));

        return ServiceResult<VisitorsReportResponse>.Success(response);
    }

    public async Task<ServiceResult<DocumentsReportResponse>> GetDocumentsReportAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateDateRange(query)
            ?? await ValidateCompoundAccessAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<DocumentsReportResponse>(validation);
        }

        var documents = ApplyDateRange(dbContext.DocumentFiles.AsNoTracking(), query)
            .Where(document => !document.IsDeleted);
        var categoryGroups = await documents
            .GroupBy(document => document.Category)
            .Select(group => new
            {
                Category = group.Key,
                Count = group.Count(),
                StorageBytes = group.Sum(document => document.SizeInBytes)
            })
            .ToArrayAsync(cancellationToken);
        var visibilityGroups = await documents
            .GroupBy(document => document.Visibility)
            .Select(group => new
            {
                Visibility = group.Key,
                Count = group.Count(),
                StorageBytes = group.Sum(document => document.SizeInBytes)
            })
            .ToArrayAsync(cancellationToken);
        var totalStorageBytes = await SumLongAsync(
            documents.Select(document => (long?)document.SizeInBytes),
            cancellationToken);

        var response = new DocumentsReportResponse(
            await documents.CountAsync(cancellationToken),
            categoryGroups
                .OrderBy(group => group.Category)
                .Select(group => new ChartPointResponse(group.Category.ToString(), group.Count, group.StorageBytes))
                .ToArray(),
            visibilityGroups
                .OrderBy(group => group.Visibility)
                .Select(group => new ChartPointResponse(group.Visibility.ToString(), group.Count, group.StorageBytes))
                .ToArray(),
            totalStorageBytes,
            Math.Round(totalStorageBytes / 1024m / 1024m, 2, MidpointRounding.AwayFromZero));

        return ServiceResult<DocumentsReportResponse>.Success(response);
    }

    public async Task<ServiceResult<OperationsReportResponse>> GetOperationsReportAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateDateRange(query)
            ?? await ValidateCompoundAccessAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<OperationsReportResponse>(validation);
        }

        var now = DateTime.UtcNow;
        var staffMembers = ApplyDateRange(dbContext.StaffMembers.AsNoTracking(), query);
        var vendors = ApplyDateRange(dbContext.ServiceVendors.AsNoTracking(), query);
        var workOrders = ApplyDateRange(dbContext.WorkOrders.AsNoTracking(), query);
        var statusGroups = await workOrders
            .GroupBy(workOrder => workOrder.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count(),
                Amount = group.Sum(workOrder => workOrder.ActualCost)
            })
            .ToArrayAsync(cancellationToken);
        var priorityGroups = await workOrders
            .GroupBy(workOrder => workOrder.Priority)
            .Select(group => new
            {
                Priority = group.Key,
                Count = group.Count(),
                Amount = group.Sum(workOrder => workOrder.ActualCost)
            })
            .ToArrayAsync(cancellationToken);

        var response = new OperationsReportResponse(
            await staffMembers.CountAsync(cancellationToken),
            await staffMembers.CountAsync(staffMember => staffMember.Status == StaffStatus.Active, cancellationToken),
            await vendors.CountAsync(cancellationToken),
            await vendors.CountAsync(vendor => vendor.Status == VendorStatus.Active, cancellationToken),
            await workOrders.CountAsync(cancellationToken),
            statusGroups
                .OrderBy(group => group.Status)
                .Select(group => new ChartPointResponse(group.Status.ToString(), group.Count, group.Amount ?? 0m))
                .ToArray(),
            priorityGroups
                .OrderBy(group => group.Priority)
                .Select(group => new ChartPointResponse(group.Priority.ToString(), group.Count, group.Amount ?? 0m))
                .ToArray(),
            await CountOverdueWorkOrdersAsync(workOrders, now, cancellationToken),
            await SumDecimalAsync(workOrders.Select(workOrder => workOrder.ActualCost), cancellationToken));

        return ServiceResult<OperationsReportResponse>.Success(response);
    }

    public async Task<ServiceResult<IReadOnlyCollection<ChartPointResponse>>> GetPaymentsTrendAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateDateRange(query)
            ?? await ValidateCompoundAccessAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<IReadOnlyCollection<ChartPointResponse>>(validation);
        }

        var groups = await ApplyDateRange(dbContext.Payments.AsNoTracking(), query)
            .GroupBy(payment => new
            {
                payment.CreatedAt.Year,
                payment.CreatedAt.Month,
                payment.CreatedAt.Day
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                Count = group.Count(),
                Amount = group.Sum(payment => payment.Amount)
            })
            .OrderBy(group => group.Year)
            .ThenBy(group => group.Month)
            .ThenBy(group => group.Day)
            .ToArrayAsync(cancellationToken);

        return ServiceResult<IReadOnlyCollection<ChartPointResponse>>.Success(
            ToDailyTrendPoints(groups.Select(group => new DailyTrendGroup(
                group.Year,
                group.Month,
                group.Day,
                group.Count,
                group.Amount))));
    }

    public async Task<ServiceResult<IReadOnlyCollection<ChartPointResponse>>> GetMaintenanceTrendAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateDateRange(query)
            ?? await ValidateCompoundAccessAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<IReadOnlyCollection<ChartPointResponse>>(validation);
        }

        var groups = await ApplyDateRange(dbContext.MaintenanceRequests.AsNoTracking(), query)
            .GroupBy(request => new
            {
                request.CreatedAt.Year,
                request.CreatedAt.Month,
                request.CreatedAt.Day
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                Count = group.Count(),
                Amount = (decimal?)null
            })
            .OrderBy(group => group.Year)
            .ThenBy(group => group.Month)
            .ThenBy(group => group.Day)
            .ToArrayAsync(cancellationToken);

        return ServiceResult<IReadOnlyCollection<ChartPointResponse>>.Success(
            ToDailyTrendPoints(groups.Select(group => new DailyTrendGroup(
                group.Year,
                group.Month,
                group.Day,
                group.Count,
                group.Amount))));
    }

    public async Task<ServiceResult<IReadOnlyCollection<ChartPointResponse>>> GetWorkOrdersTrendAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateDateRange(query)
            ?? await ValidateCompoundAccessAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<IReadOnlyCollection<ChartPointResponse>>(validation);
        }

        var groups = await ApplyDateRange(dbContext.WorkOrders.AsNoTracking(), query)
            .GroupBy(workOrder => new
            {
                workOrder.CreatedAtUtc.Year,
                workOrder.CreatedAtUtc.Month,
                workOrder.CreatedAtUtc.Day
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                Count = group.Count(),
                Amount = group.Sum(workOrder => workOrder.ActualCost)
            })
            .OrderBy(group => group.Year)
            .ThenBy(group => group.Month)
            .ThenBy(group => group.Day)
            .ToArrayAsync(cancellationToken);

        return ServiceResult<IReadOnlyCollection<ChartPointResponse>>.Success(
            ToDailyTrendPoints(groups.Select(group => new DailyTrendGroup(
                group.Year,
                group.Month,
                group.Day,
                group.Count,
                group.Amount))));
    }

    private static ValidationFailure? ValidateDateRange(DateRangeQueryRequest query)
    {
        if (!query.CompoundId.HasValue || query.CompoundId.Value == Guid.Empty)
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "CompoundId is required for analytics endpoints.");
        }

        return query.FromUtc.HasValue
            && query.ToUtc.HasValue
            && query.FromUtc.Value > query.ToUtc.Value
            ? new ValidationFailure(ServiceResultStatus.BadRequest, "FromUtc cannot be later than ToUtc.")
            : null;
    }

    private async Task<ValidationFailure?> ValidateCompoundAccessAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return null;
        }

        return await compoundAccessService.CanCurrentUserAccessCompoundAsync(
            query.CompoundId!.Value,
            cancellationToken)
            ? null
            : new ValidationFailure(ServiceResultStatus.Forbidden, "Current user cannot access this compound.");
    }

    private static IQueryable<ApplicationUser> ApplyDateRange(
        IQueryable<ApplicationUser> queryable,
        DateRangeQueryRequest query)
    {
        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAtUtc <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<ResidentProfile> ApplyDateRange(
        IQueryable<ResidentProfile> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.CompoundId == query.CompoundId!.Value);

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<PropertyUnit> ApplyDateRange(
        IQueryable<PropertyUnit> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.CompoundId == query.CompoundId!.Value);

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<UtilityBill> ApplyDateRange(
        IQueryable<UtilityBill> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.CompoundId == query.CompoundId!.Value);

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<Payment> ApplyDateRange(
        IQueryable<Payment> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.CompoundId == query.CompoundId!.Value);

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<MaintenanceRequest> ApplyDateRange(
        IQueryable<MaintenanceRequest> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.CompoundId == query.CompoundId!.Value);

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<Complaint> ApplyDateRange(
        IQueryable<Complaint> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.CompoundId == query.CompoundId!.Value);

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<Violation> ApplyDateRange(
        IQueryable<Violation> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.CompoundId == query.CompoundId!.Value);

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<VisitorPass> ApplyDateRange(
        IQueryable<VisitorPass> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.CompoundId == query.CompoundId!.Value);

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt <= query.ToUtc.Value);
        }

        return queryable;
    }


    private IQueryable<ResidentNotification> ApplyDateRange(
        IQueryable<ResidentNotification> queryable,
        DateRangeQueryRequest query)
    {
        var compoundId = query.CompoundId!.Value;
        queryable = queryable.Where(notification => dbContext.ResidentProfiles
            .Any(profile => profile.UserId == notification.UserId && profile.CompoundId == compoundId));

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<Announcement> ApplyDateRange(
        IQueryable<Announcement> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.CompoundId == query.CompoundId!.Value);

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<CommunityPoll> ApplyDateRange(
        IQueryable<CommunityPoll> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.CompoundId == query.CompoundId!.Value);

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<CommunityPollVote> ApplyDateRange(
        IQueryable<CommunityPollVote> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.Poll.CompoundId == query.CompoundId!.Value);

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAt <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<DocumentFile> ApplyDateRange(
        IQueryable<DocumentFile> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.PropertyUnit != null && item.PropertyUnit.CompoundId == query.CompoundId!.Value);

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAtUtc <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<StaffMember> ApplyDateRange(
        IQueryable<StaffMember> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.AssignedWorkOrders.Any(workOrder => workOrder.PropertyUnit != null && workOrder.PropertyUnit.CompoundId == query.CompoundId!.Value));

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAtUtc <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<ServiceVendor> ApplyDateRange(
        IQueryable<ServiceVendor> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.AssignedWorkOrders.Any(workOrder => workOrder.PropertyUnit != null && workOrder.PropertyUnit.CompoundId == query.CompoundId!.Value));

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAtUtc <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static IQueryable<WorkOrder> ApplyDateRange(
        IQueryable<WorkOrder> queryable,
        DateRangeQueryRequest query)
    {
        queryable = queryable.Where(item => item.PropertyUnit != null && item.PropertyUnit.CompoundId == query.CompoundId!.Value);

        if (query.FromUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            queryable = queryable.Where(item => item.CreatedAtUtc <= query.ToUtc.Value);
        }

        return queryable;
    }

    private static System.Linq.Expressions.Expression<Func<MaintenanceRequest, bool>> IsOpenMaintenanceRequest()
    {
        return request =>
            request.Status == MaintenanceStatus.Open
            || request.Status == MaintenanceStatus.Assigned
            || request.Status == MaintenanceStatus.InProgress;
    }

    private static System.Linq.Expressions.Expression<Func<Complaint, bool>> IsOpenComplaint()
    {
        return complaint =>
            complaint.Status == ComplaintStatus.Open
            || complaint.Status == ComplaintStatus.UnderReview;
    }

    private static System.Linq.Expressions.Expression<Func<WorkOrder, bool>> IsOpenWorkOrder()
    {
        return workOrder =>
            workOrder.Status == WorkOrderStatus.New
            || workOrder.Status == WorkOrderStatus.Assigned
            || workOrder.Status == WorkOrderStatus.Scheduled
            || workOrder.Status == WorkOrderStatus.InProgress;
    }

    private static Task<int> CountOverdueWorkOrdersAsync(
        IQueryable<WorkOrder> workOrders,
        DateTime now,
        CancellationToken cancellationToken)
    {
        return workOrders.CountAsync(workOrder =>
            workOrder.DueAtUtc.HasValue
            && workOrder.DueAtUtc.Value < now
            && workOrder.Status != WorkOrderStatus.Completed
            && workOrder.Status != WorkOrderStatus.Cancelled,
            cancellationToken);
    }

    private static Task<int> CountOpenViolationsAsync(
        IQueryable<Violation> violations,
        CancellationToken cancellationToken)
    {
        return violations.CountAsync(violation =>
            !violation.Fines.Any()
            || violation.Fines.Any(fine =>
                fine.Status == ViolationFineStatus.Unpaid
                || fine.Status == ViolationFineStatus.PartiallyPaid),
            cancellationToken);
    }

    private static async Task<decimal> SumDecimalAsync(
        IQueryable<decimal?> values,
        CancellationToken cancellationToken)
    {
        return await values.SumAsync(cancellationToken) ?? 0m;
    }

    private static async Task<long> SumLongAsync(
        IQueryable<long?> values,
        CancellationToken cancellationToken)
    {
        return await values.SumAsync(cancellationToken) ?? 0L;
    }

    private static IReadOnlyCollection<ChartPointResponse> ToDailyTrendPoints(
        IEnumerable<DailyTrendGroup> groups)
    {
        return groups
            .Select(group => new ChartPointResponse(
                new DateTime(group.Year, group.Month, group.Day).ToString("yyyy-MM-dd"),
                group.Count,
                group.Amount))
            .ToArray();
    }

    private static ServiceResult<T> ToResult<T>(ValidationFailure validationFailure)
    {
        return validationFailure.Status switch
        {
            ServiceResultStatus.NotFound => ServiceResult<T>.NotFound(validationFailure.Message),
            ServiceResultStatus.Conflict => ServiceResult<T>.Conflict(validationFailure.Message),
            ServiceResultStatus.Forbidden => ServiceResult<T>.Forbidden(validationFailure.Message),
            _ => ServiceResult<T>.BadRequest(validationFailure.Message)
        };
    }

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);

    private sealed record DailyTrendGroup(
        int Year,
        int Month,
        int Day,
        int Count,
        decimal? Amount);
}
