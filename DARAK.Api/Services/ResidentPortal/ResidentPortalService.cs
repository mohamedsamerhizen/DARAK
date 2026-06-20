using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Meters;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.DTOs.PropertySales;
using DARAK.Api.DTOs.ResidentPortal;
using DARAK.Api.DTOs.Rents;
using DARAK.Api.DTOs.UtilityBills;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class ResidentPortalService(
    ApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IUtilityBillingService utilityBillingService,
    IPropertyContractsService propertyContractsService,
    IPaymentService paymentService,
    IMeterService meterService)
    : IResidentPortalService
{
    private const int DashboardItemLimit = 10;

    public async Task<ServiceResult<ResidentDashboardResponse>> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await GetResidentPortalScopeAsync(cancellationToken);
        if (scopeResult.Failure is not null)
        {
            return ToResult<ResidentDashboardResponse>(scopeResult.Failure);
        }

        var scope = scopeResult.Scope!;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var profileIds = scope.ProfileIds;

        var properties = await GetPropertySummariesAsync(profileIds, cancellationToken);
        var utilityBills = await GetResidentUtilityBillsQuery(profileIds)
            .ToArrayAsync(cancellationToken);
        var rentInvoices = await dbContext.RentInvoices
            .AsNoTracking()
            .Include(invoice => invoice.PropertyUnit)
            .Where(invoice => profileIds.Contains(invoice.ResidentProfileId)
                && invoice.RentInvoiceStatus != RentInvoiceStatus.Cancelled)
            .ToArrayAsync(cancellationToken);
        var installments = await dbContext.InstallmentScheduleItems
            .AsNoTracking()
            .Include(installment => installment.PropertyUnit)
            .Where(installment => profileIds.Contains(installment.ResidentProfileId)
                && installment.InstallmentStatus != InstallmentStatus.Cancelled)
            .ToArrayAsync(cancellationToken);
        var violationFines = await dbContext.ViolationFines
            .AsNoTracking()
            .Where(fine => fine.ResidentProfileId.HasValue
                && profileIds.Contains(fine.ResidentProfileId.Value)
                && fine.Status != ViolationFineStatus.Cancelled)
            .ToArrayAsync(cancellationToken);
        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.ResidentProfileId.HasValue
                && profileIds.Contains(payment.ResidentProfileId.Value))
            .ToArrayAsync(cancellationToken);

        var utilityOutstanding = utilityBills.Sum(GetUtilityBillOutstanding);
        var rentOutstanding = rentInvoices.Sum(GetRentInvoiceOutstanding);
        var installmentsOutstanding = installments.Sum(GetInstallmentOutstanding);
        var violationFineOutstanding = violationFines.Sum(GetViolationFineOutstanding);
        var utilityOverdue = utilityBills
            .Where(bill => IsUtilityBillOverdue(bill, today))
            .Sum(GetUtilityBillOutstanding);
        var rentOverdue = rentInvoices
            .Where(invoice => IsRentInvoiceOverdue(invoice, today))
            .Sum(GetRentInvoiceOutstanding);
        var installmentOverdue = installments
            .Where(installment => IsInstallmentOverdue(installment, today))
            .Sum(GetInstallmentOutstanding);
        var violationFineOverdue = violationFines
            .Where(fine => IsViolationFineOverdue(fine, today))
            .Sum(GetViolationFineOutstanding);

        var upcomingDueItems = BuildUpcomingDueItems(
            utilityBills,
            rentInvoices,
            installments,
            violationFines,
            today);

        var recentPayments = payments
            .OrderByDescending(payment => payment.CreatedAt)
            .Take(DashboardItemLimit)
            .Select(payment => new ResidentRecentPaymentResponse(
                payment.Id,
                payment.PaymentReference,
                payment.TargetType.ToString(),
                payment.Amount,
                payment.PaymentStatus.ToString(),
                payment.CreatedAt,
                payment.CompletedAt))
            .ToList();

        var openMeterReadingsCount = await CountVisibleOpenMeterReadingsAsync(
            profileIds,
            cancellationToken);

        return ServiceResult<ResidentDashboardResponse>.Success(
            new ResidentDashboardResponse(
                scope.PrimaryProfile.Id,
                scope.PrimaryProfile.FullName,
                properties.Count,
                utilityOutstanding + rentOutstanding + installmentsOutstanding + violationFineOutstanding,
                utilityOverdue + rentOverdue + installmentOverdue + violationFineOverdue,
                utilityBills.Count(IsUtilityBillOutstanding),
                utilityBills.Count(bill => IsUtilityBillOverdue(bill, today)),
                installments.Count(IsInstallmentOutstanding),
                installments.Count(installment => IsInstallmentOverdue(installment, today)),
                rentInvoices.Count(IsRentInvoiceOutstanding),
                rentInvoices.Count(invoice => IsRentInvoiceOverdue(invoice, today)),
                payments.Count(payment => payment.PaymentStatus == PaymentStatus.Pending),
                openMeterReadingsCount,
                upcomingDueItems,
                recentPayments,
                properties));
    }

    public async Task<ServiceResult<IReadOnlyCollection<ResidentPropertySummaryResponse>>> GetMyPropertiesAsync(
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await GetResidentPortalScopeAsync(cancellationToken);
        if (scopeResult.Failure is not null)
        {
            return ToResult<IReadOnlyCollection<ResidentPropertySummaryResponse>>(scopeResult.Failure);
        }

        var properties = await GetPropertySummariesAsync(
            scopeResult.Scope!.ProfileIds,
            cancellationToken);

        return ServiceResult<IReadOnlyCollection<ResidentPropertySummaryResponse>>.Success(properties);
    }

    public async Task<ServiceResult<PagedResult<UtilityBillResponse>>> GetMyBillsAsync(
        UtilityBillSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await GetResidentPortalScopeAsync(cancellationToken);
        if (scopeResult.Failure is not null)
        {
            return ToResult<PagedResult<UtilityBillResponse>>(scopeResult.Failure);
        }

        if (query.ResidentProfileId.HasValue)
        {
            return ServiceResult<PagedResult<UtilityBillResponse>>.BadRequest(
                "Resident profile filters are not allowed on resident portal endpoints.");
        }

        return ServiceResult<PagedResult<UtilityBillResponse>>.Success(
            await utilityBillingService.SearchResidentBillsAsync(
                scopeResult.Scope!.UserId,
                query,
                cancellationToken));
    }

    public async Task<ServiceResult<PagedResult<InstallmentScheduleItemResponse>>> GetMyInstallmentsAsync(
        InstallmentSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await GetResidentPortalScopeAsync(cancellationToken);
        if (scopeResult.Failure is not null)
        {
            return ToResult<PagedResult<InstallmentScheduleItemResponse>>(scopeResult.Failure);
        }

        if (query.ResidentProfileId.HasValue)
        {
            return ServiceResult<PagedResult<InstallmentScheduleItemResponse>>.BadRequest(
                "Resident profile filters are not allowed on resident portal endpoints.");
        }

        return ServiceResult<PagedResult<InstallmentScheduleItemResponse>>.Success(
            await propertyContractsService.SearchResidentInstallmentsAsync(
                scopeResult.Scope!.UserId,
                query,
                cancellationToken));
    }

    public async Task<ServiceResult<PagedResult<RentInvoiceResponse>>> GetMyRentAsync(
        RentInvoiceSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await GetResidentPortalScopeAsync(cancellationToken);
        if (scopeResult.Failure is not null)
        {
            return ToResult<PagedResult<RentInvoiceResponse>>(scopeResult.Failure);
        }

        if (query.ResidentProfileId.HasValue)
        {
            return ServiceResult<PagedResult<RentInvoiceResponse>>.BadRequest(
                "Resident profile filters are not allowed on resident portal endpoints.");
        }

        return ServiceResult<PagedResult<RentInvoiceResponse>>.Success(
            await propertyContractsService.SearchResidentRentInvoicesAsync(
                scopeResult.Scope!.UserId,
                query,
                cancellationToken));
    }

    public async Task<ServiceResult<PagedResult<PaymentResponse>>> GetMyPaymentsAsync(
        PaymentSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await GetResidentPortalScopeAsync(cancellationToken);
        if (scopeResult.Failure is not null)
        {
            return ToResult<PagedResult<PaymentResponse>>(scopeResult.Failure);
        }

        if (query.ResidentProfileId.HasValue)
        {
            return ServiceResult<PagedResult<PaymentResponse>>.BadRequest(
                "Resident profile filters are not allowed on resident portal endpoints.");
        }

        return ServiceResult<PagedResult<PaymentResponse>>.Success(
            await paymentService.SearchResidentPaymentsAsync(
                scopeResult.Scope!.UserId,
                query,
                cancellationToken));
    }

    public async Task<ServiceResult<PagedResult<MeterReadingResponse>>> GetMyMeterReadingsAsync(
        MeterReadingSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await GetResidentPortalScopeAsync(cancellationToken);
        if (scopeResult.Failure is not null)
        {
            return ToResult<PagedResult<MeterReadingResponse>>(scopeResult.Failure);
        }

        return ServiceResult<PagedResult<MeterReadingResponse>>.Success(
            await meterService.SearchResidentMeterReadingsAsync(
                scopeResult.Scope!.UserId,
                query,
                cancellationToken));
    }

    private async Task<(ResidentPortalScope? Scope, ValidationFailure? Failure)> GetResidentPortalScopeAsync(
        CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Current user is invalid."));
        }

        var profiles = await dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == currentUserId.Value && profile.IsActive)
            .OrderBy(profile => profile.CreatedAt)
            .ToArrayAsync(cancellationToken);

        if (profiles.Length == 0)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Resident profile was not found."));
        }

        var profileIds = profiles
            .Select(profile => profile.Id)
            .ToArray();

        return (new ResidentPortalScope(
            currentUserId.Value,
            profiles[0],
            profileIds), null);
    }

    private async Task<List<ResidentPropertySummaryResponse>> GetPropertySummariesAsync(
        Guid[] profileIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.OccupancyRecords
            .AsNoTracking()
            .Where(record => profileIds.Contains(record.ResidentProfileId)
                && record.OccupancyStatus == OccupancyStatus.Active
                && record.PropertyUnit.IsActive)
            .OrderBy(record => record.PropertyUnit.UnitNumber)
            .Select(record => new ResidentPropertySummaryResponse(
                record.PropertyUnitId,
                record.PropertyUnit.UnitNumber,
                record.PropertyUnit.PropertyType.ToString(),
                record.PropertyUnit.UnitStatus.ToString(),
                record.OccupancyType.ToString(),
                record.StartDate,
                record.PropertyUnit.Building != null ? record.PropertyUnit.Building.Name : null,
                record.PropertyUnit.Floor != null ? record.PropertyUnit.Floor.FloorNumber : null))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<UtilityBill> GetResidentUtilityBillsQuery(Guid[] profileIds)
    {
        return dbContext.UtilityBills
            .AsNoTracking()
            .Include(bill => bill.PropertyUnit)
            .Where(bill => bill.BillStatus != BillStatus.Cancelled)
            .Where(bill => bill.ResidentProfileId.HasValue
                && profileIds.Contains(bill.ResidentProfileId.Value));
    }

    private async Task<int> CountVisibleOpenMeterReadingsAsync(
        Guid[] profileIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.MeterReadings
            .AsNoTracking()
            .CountAsync(reading =>
                !reading.IsBilled
                && dbContext.OccupancyRecords
                    .AsNoTracking()
                    .Any(record =>
                        profileIds.Contains(record.ResidentProfileId)
                        && record.OccupancyStatus == OccupancyStatus.Active
                        && record.ResidentProfile.IsActive
                        && record.PropertyUnitId == reading.PropertyUnitId
                        && (reading.Year > record.StartDate.Year
                            || (reading.Year == record.StartDate.Year
                                && reading.Month >= record.StartDate.Month))),
                cancellationToken);
    }

    private static List<ResidentUpcomingDueItemResponse> BuildUpcomingDueItems(
        IReadOnlyCollection<UtilityBill> utilityBills,
        IReadOnlyCollection<RentInvoice> rentInvoices,
        IReadOnlyCollection<InstallmentScheduleItem> installments,
        IReadOnlyCollection<ViolationFine> violationFines,
        DateOnly today)
    {
        var utilityItems = utilityBills
            .Where(IsUtilityBillOutstanding)
            .Select(bill => new ResidentUpcomingDueItemResponse(
                "UtilityBill",
                bill.Id,
                $"Utility bill {bill.BillNumber}",
                bill.DueDate,
                GetUtilityBillOutstanding(bill),
                GetUtilityBillDashboardStatus(bill, today)));

        var rentItems = rentInvoices
            .Where(IsRentInvoiceOutstanding)
            .Select(invoice => new ResidentUpcomingDueItemResponse(
                "RentInvoice",
                invoice.Id,
                $"Rent invoice {invoice.InvoiceNumber}",
                invoice.DueDate,
                GetRentInvoiceOutstanding(invoice),
                GetRentInvoiceDashboardStatus(invoice, today)));

        var installmentItems = installments
            .Where(IsInstallmentOutstanding)
            .Select(installment => new ResidentUpcomingDueItemResponse(
                "PropertyInstallment",
                installment.Id,
                $"Installment {installment.InstallmentNumber} for unit {installment.PropertyUnit.UnitNumber}",
                installment.DueDate,
                GetInstallmentOutstanding(installment),
                GetInstallmentDashboardStatus(installment, today)));

        var violationFineItems = violationFines
            .Where(IsViolationFineOutstanding)
            .Select(fine => new ResidentUpcomingDueItemResponse(
                "ViolationFine",
                fine.Id,
                $"Violation fine: {fine.Reason}",
                fine.DueDate,
                GetViolationFineOutstanding(fine),
                GetViolationFineDashboardStatus(fine, today)));

        return utilityItems
            .Concat(rentItems)
            .Concat(installmentItems)
            .Concat(violationFineItems)
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.Type)
            .Take(DashboardItemLimit)
            .ToList();
    }

    private static bool IsUtilityBillOutstanding(UtilityBill bill)
    {
        return bill.BillStatus != BillStatus.Cancelled && GetUtilityBillOutstanding(bill) > 0;
    }

    private static bool IsRentInvoiceOutstanding(RentInvoice invoice)
    {
        return invoice.RentInvoiceStatus != RentInvoiceStatus.Cancelled
            && GetRentInvoiceOutstanding(invoice) > 0;
    }

    private static bool IsInstallmentOutstanding(InstallmentScheduleItem installment)
    {
        return installment.InstallmentStatus != InstallmentStatus.Cancelled
            && GetInstallmentOutstanding(installment) > 0;
    }

    private static bool IsViolationFineOutstanding(ViolationFine fine)
    {
        return fine.Status != ViolationFineStatus.Cancelled
            && GetViolationFineOutstanding(fine) > 0;
    }

    private static bool IsUtilityBillOverdue(UtilityBill bill, DateOnly today)
    {
        return IsUtilityBillOutstanding(bill)
            && (bill.BillStatus == BillStatus.Overdue || bill.DueDate < today);
    }

    private static bool IsRentInvoiceOverdue(RentInvoice invoice, DateOnly today)
    {
        return IsRentInvoiceOutstanding(invoice)
            && (invoice.RentInvoiceStatus == RentInvoiceStatus.Overdue || invoice.DueDate < today);
    }

    private static bool IsInstallmentOverdue(InstallmentScheduleItem installment, DateOnly today)
    {
        return IsInstallmentOutstanding(installment)
            && (installment.InstallmentStatus == InstallmentStatus.Overdue || installment.DueDate < today);
    }

    private static bool IsViolationFineOverdue(ViolationFine fine, DateOnly today)
    {
        return IsViolationFineOutstanding(fine)
            && fine.DueDate < today;
    }

    private static decimal GetUtilityBillOutstanding(UtilityBill bill)
    {
        return Math.Max(0m, bill.TotalAmount - bill.PaidAmount);
    }

    private static decimal GetRentInvoiceOutstanding(RentInvoice invoice)
    {
        return Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount);
    }

    private static decimal GetInstallmentOutstanding(InstallmentScheduleItem installment)
    {
        return Math.Max(0m, installment.Amount - installment.PaidAmount);
    }

    private static decimal GetViolationFineOutstanding(ViolationFine fine)
    {
        return Math.Max(0m, fine.Amount - fine.PaidAmount);
    }

    private static string GetUtilityBillDashboardStatus(UtilityBill bill, DateOnly today)
    {
        return IsUtilityBillOverdue(bill, today)
            ? BillStatus.Overdue.ToString()
            : bill.BillStatus.ToString();
    }

    private static string GetRentInvoiceDashboardStatus(RentInvoice invoice, DateOnly today)
    {
        return IsRentInvoiceOverdue(invoice, today)
            ? RentInvoiceStatus.Overdue.ToString()
            : invoice.RentInvoiceStatus.ToString();
    }

    private static string GetInstallmentDashboardStatus(
        InstallmentScheduleItem installment,
        DateOnly today)
    {
        return IsInstallmentOverdue(installment, today)
            ? InstallmentStatus.Overdue.ToString()
            : installment.InstallmentStatus.ToString();
    }

    private static string GetViolationFineDashboardStatus(ViolationFine fine, DateOnly today)
    {
        return IsViolationFineOverdue(fine, today)
            ? "Overdue"
            : fine.Status.ToString();
    }

    private static ServiceResult<T> ToResult<T>(ValidationFailure validationFailure)
    {
        return validationFailure.Status switch
        {
            ServiceResultStatus.NotFound => ServiceResult<T>.NotFound(validationFailure.Message),
            ServiceResultStatus.Conflict => ServiceResult<T>.Conflict(validationFailure.Message),
            _ => ServiceResult<T>.BadRequest(validationFailure.Message)
        };
    }

    private sealed record ResidentPortalScope(
        Guid UserId,
        ResidentProfile PrimaryProfile,
        Guid[] ProfileIds);

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);
}
