using DARAK.Api.Data;
using DARAK.Api.DTOs.AdminPortal;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class AdminPortalService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IAdminPortalService
{
    private const int MinTopCount = 1;
    private const int MaxTopCount = 50;

    public async Task<ServiceResult<AdminDashboardResponse>> GetDashboardAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateQueryAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<AdminDashboardResponse>(validation);
        }

        var units = await GetUnitsOverviewCoreAsync(query, cancellationToken);
        var debt = await GetDebtOverviewCoreAsync(query, cancellationToken);
        var monthStart = GetCurrentMonthStartUtc();

        var response = new AdminDashboardResponse(
            await GetCompoundsQuery(query).CountAsync(cancellationToken),
            await GetCompoundsQuery(query).CountAsync(compound => compound.IsActive, cancellationToken),
            units.TotalUnits,
            units.Available,
            units.Occupied,
            units.Rented,
            units.SoldCash,
            units.SoldInstallment,
            units.UnderMaintenance,
            units.Blocked,
            await GetResidentProfilesQuery(query).CountAsync(profile => profile.IsActive, cancellationToken),
            await GetOccupancyRecordsQuery(query).CountAsync(
                record => record.OccupancyStatus == OccupancyStatus.Active,
                cancellationToken),
            debt.TotalOutstanding,
            debt.UtilityBillsOutstanding,
            debt.RentOutstanding,
            debt.InstallmentsOutstanding,
            await SumSucceededPaymentsAsync(query, monthStart, null, null, cancellationToken),
            await SumSucceededPaymentsAsync(query, monthStart, null, PaymentTargetType.UtilityBill, cancellationToken),
            await SumSucceededPaymentsAsync(query, monthStart, null, PaymentTargetType.RentInvoice, cancellationToken),
            await SumSucceededPaymentsAsync(query, monthStart, null, PaymentTargetType.PropertyInstallment, cancellationToken),
            await GetPaymentsForSummaryQuery(query).CountAsync(
                payment => payment.PaymentStatus == PaymentStatus.Pending,
                cancellationToken),
            await GetPaymentsForSummaryQuery(query).CountAsync(
                payment => payment.PaymentStatus == PaymentStatus.Failed,
                cancellationToken),
            debt.OverdueUtilityBills,
            debt.OverdueRentInvoices,
            debt.OverdueInstallments,
            debt.TopDebtors,
            await GetRecentPaymentsAsync(query, cancellationToken));

        return ServiceResult<AdminDashboardResponse>.Success(response);
    }

    public async Task<ServiceResult<AdminUnitsOverviewResponse>> GetUnitsOverviewAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateQueryAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<AdminUnitsOverviewResponse>(validation);
        }

        return ServiceResult<AdminUnitsOverviewResponse>.Success(
            await GetUnitsOverviewCoreAsync(query, cancellationToken));
    }

    public async Task<ServiceResult<AdminDebtOverviewResponse>> GetDebtOverviewAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateQueryAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<AdminDebtOverviewResponse>(validation);
        }

        return ServiceResult<AdminDebtOverviewResponse>.Success(
            await GetDebtOverviewCoreAsync(query, cancellationToken));
    }

    public async Task<ServiceResult<AdminRevenueOverviewResponse>> GetRevenueOverviewAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateQueryAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<AdminRevenueOverviewResponse>(validation);
        }

        var monthStart = GetCurrentMonthStartUtc();
        var todayStart = DateTime.UtcNow.Date;
        var tomorrowStart = todayStart.AddDays(1);

        var byPaymentMethod = await GetSucceededPaymentsQuery(query)
            .GroupBy(payment => payment.PaymentMethod)
            .Select(group => new
            {
                PaymentMethod = group.Key,
                TotalAmount = group.Sum(payment => payment.Amount),
                Count = group.Count()
            })
            .OrderByDescending(item => item.TotalAmount)
            .ThenBy(item => item.PaymentMethod)
            .ToArrayAsync(cancellationToken);

        var response = new AdminRevenueOverviewResponse(
            await SumSucceededPaymentsAsync(query, null, null, null, cancellationToken),
            await SumSucceededPaymentsAsync(query, monthStart, null, null, cancellationToken),
            await SumSucceededPaymentsAsync(query, todayStart, tomorrowStart, null, cancellationToken),
            await SumSucceededPaymentsAsync(query, null, null, PaymentTargetType.UtilityBill, cancellationToken),
            await SumSucceededPaymentsAsync(query, null, null, PaymentTargetType.RentInvoice, cancellationToken),
            await SumSucceededPaymentsAsync(query, null, null, PaymentTargetType.PropertyInstallment, cancellationToken),
            byPaymentMethod
                .Select(item => new AdminRevenueByPaymentMethodResponse(
                    item.PaymentMethod.ToString(),
                    item.TotalAmount,
                    item.Count))
                .ToList());

        return ServiceResult<AdminRevenueOverviewResponse>.Success(response);
    }

    public async Task<ServiceResult<AdminOccupancyOverviewResponse>> GetOccupancyOverviewAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateQueryAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<AdminOccupancyOverviewResponse>(validation);
        }

        var occupancies = GetOccupancyRecordsQuery(query);

        var response = new AdminOccupancyOverviewResponse(
            await occupancies.CountAsync(record => record.OccupancyStatus == OccupancyStatus.Active, cancellationToken),
            await occupancies.CountAsync(
                record => record.OccupancyStatus == OccupancyStatus.Active
                    && record.OccupancyType == OccupancyType.Tenant,
                cancellationToken),
            await occupancies.CountAsync(
                record => record.OccupancyStatus == OccupancyStatus.Active
                    && record.OccupancyType == OccupancyType.OwnerCash,
                cancellationToken),
            await occupancies.CountAsync(
                record => record.OccupancyStatus == OccupancyStatus.Active
                    && record.OccupancyType == OccupancyType.OwnerInstallment,
                cancellationToken),
            await occupancies.CountAsync(record => record.OccupancyStatus == OccupancyStatus.Ended, cancellationToken),
            await occupancies.CountAsync(record => record.OccupancyStatus == OccupancyStatus.Cancelled, cancellationToken));

        return ServiceResult<AdminOccupancyOverviewResponse>.Success(response);
    }

    public async Task<ServiceResult<AdminBillingOverviewResponse>> GetBillingOverviewAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateQueryAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<AdminBillingOverviewResponse>(validation);
        }

        var response = await GetBillingOverviewCoreAsync(query, cancellationToken);
        return ServiceResult<AdminBillingOverviewResponse>.Success(response);
    }

    public async Task<ServiceResult<AdminPaymentsOverviewResponse>> GetPaymentsOverviewAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateQueryAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<AdminPaymentsOverviewResponse>(validation);
        }

        var payments = GetPaymentsForSummaryQuery(query);

        var response = new AdminPaymentsOverviewResponse(
            await payments.CountAsync(cancellationToken),
            await payments.CountAsync(payment => payment.PaymentStatus == PaymentStatus.Pending, cancellationToken),
            await payments.CountAsync(payment => payment.PaymentStatus == PaymentStatus.Succeeded, cancellationToken),
            await payments.CountAsync(payment => payment.PaymentStatus == PaymentStatus.Failed, cancellationToken),
            await payments.CountAsync(payment => payment.PaymentStatus == PaymentStatus.Cancelled, cancellationToken),
            await payments.CountAsync(payment => payment.PaymentStatus == PaymentStatus.Refunded, cancellationToken),
            await payments
                .Where(payment => payment.PaymentStatus == PaymentStatus.Succeeded)
                .SumAsync(payment => payment.Amount, cancellationToken));

        return ServiceResult<AdminPaymentsOverviewResponse>.Success(response);
    }

    public async Task<ServiceResult<AdminContractsOverviewResponse>> GetContractsOverviewAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateQueryAsync(query, cancellationToken);
        if (validation is not null)
        {
            return ToResult<AdminContractsOverviewResponse>(validation);
        }

        var saleContracts = GetSaleContractsQuery(query);
        var rentContracts = GetRentContractsQuery(query);
        var installments = GetInstallmentsQuery(query);
        var rentInvoices = GetRentInvoicesQuery(query);

        var response = new AdminContractsOverviewResponse(
            await saleContracts.CountAsync(cancellationToken),
            await saleContracts.CountAsync(
                contract => contract.ContractStatus == SaleContractStatus.Active,
                cancellationToken),
            await rentContracts.CountAsync(cancellationToken),
            await rentContracts.CountAsync(
                contract => contract.ContractStatus == RentContractStatus.Active,
                cancellationToken),
            await installments.CountAsync(
                installment => installment.InstallmentStatus == InstallmentStatus.Pending,
                cancellationToken),
            await installments.CountAsync(
                installment => installment.InstallmentStatus == InstallmentStatus.Overdue,
                cancellationToken),
            await rentInvoices.CountAsync(
                invoice => invoice.RentInvoiceStatus == RentInvoiceStatus.Unpaid,
                cancellationToken),
            await rentInvoices.CountAsync(
                invoice => invoice.RentInvoiceStatus == RentInvoiceStatus.Overdue,
                cancellationToken));

        return ServiceResult<AdminContractsOverviewResponse>.Success(response);
    }

    private async Task<AdminUnitsOverviewResponse> GetUnitsOverviewCoreAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        var units = GetPropertyUnitsQuery(query);

        var byCompound = await units
            .GroupBy(unit => new { unit.CompoundId, CompoundName = unit.Compound.Name })
            .Select(group => new AdminUnitsByCompoundResponse(
                group.Key.CompoundId,
                group.Key.CompoundName,
                group.Count(),
                group.Count(unit => unit.UnitStatus == UnitStatus.Available),
                group.Count(unit => unit.UnitStatus == UnitStatus.Occupied),
                group.Count(unit => unit.UnitStatus == UnitStatus.Rented),
                group.Count(unit => unit.UnitStatus == UnitStatus.SoldCash),
                group.Count(unit => unit.UnitStatus == UnitStatus.SoldInstallment),
                group.Count(unit => unit.UnitStatus == UnitStatus.UnderMaintenance),
                group.Count(unit => unit.UnitStatus == UnitStatus.Blocked)))
            .OrderBy(item => item.CompoundName)
            .ToListAsync(cancellationToken);

        return new AdminUnitsOverviewResponse(
            await units.CountAsync(cancellationToken),
            await units.CountAsync(unit => unit.UnitStatus == UnitStatus.Available, cancellationToken),
            await units.CountAsync(unit => unit.UnitStatus == UnitStatus.Occupied, cancellationToken),
            await units.CountAsync(unit => unit.UnitStatus == UnitStatus.Rented, cancellationToken),
            await units.CountAsync(unit => unit.UnitStatus == UnitStatus.SoldCash, cancellationToken),
            await units.CountAsync(unit => unit.UnitStatus == UnitStatus.SoldInstallment, cancellationToken),
            await units.CountAsync(unit => unit.UnitStatus == UnitStatus.UnderMaintenance, cancellationToken),
            await units.CountAsync(unit => unit.UnitStatus == UnitStatus.Blocked, cancellationToken),
            byCompound);
    }

    private async Task<AdminDebtOverviewResponse> GetDebtOverviewCoreAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        var utilityOutstanding = await SumUtilityOutstandingAsync(query, cancellationToken);
        var rentOutstanding = await SumRentOutstandingAsync(query, cancellationToken);
        var installmentOutstanding = await SumInstallmentsOutstandingAsync(query, cancellationToken);

        return new AdminDebtOverviewResponse(
            utilityOutstanding + rentOutstanding + installmentOutstanding,
            utilityOutstanding,
            rentOutstanding,
            installmentOutstanding,
            await GetUtilityBillsQuery(query).CountAsync(
                bill => bill.BillStatus == BillStatus.Overdue,
                cancellationToken),
            await GetRentInvoicesQuery(query).CountAsync(
                invoice => invoice.RentInvoiceStatus == RentInvoiceStatus.Overdue,
                cancellationToken),
            await GetInstallmentsQuery(query).CountAsync(
                installment => installment.InstallmentStatus == InstallmentStatus.Overdue,
                cancellationToken),
            await GetTopDebtorsAsync(query, cancellationToken));
    }

    private async Task<AdminBillingOverviewResponse> GetBillingOverviewCoreAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        var bills = GetUtilityBillsQuery(query);
        var billable = bills.Where(bill => bill.BillStatus != BillStatus.Cancelled);

        return new AdminBillingOverviewResponse(
            await bills.CountAsync(cancellationToken),
            await bills.CountAsync(bill => bill.BillStatus == BillStatus.Unpaid, cancellationToken),
            await bills.CountAsync(bill => bill.BillStatus == BillStatus.PartiallyPaid, cancellationToken),
            await bills.CountAsync(bill => bill.BillStatus == BillStatus.Paid, cancellationToken),
            await bills.CountAsync(bill => bill.BillStatus == BillStatus.Overdue, cancellationToken),
            await bills.CountAsync(bill => bill.BillStatus == BillStatus.Cancelled, cancellationToken),
            await billable.SumAsync(bill => bill.TotalAmount, cancellationToken),
            await billable.SumAsync(bill => bill.PaidAmount, cancellationToken),
            await billable.SumAsync(
                bill => bill.TotalAmount > bill.PaidAmount ? bill.TotalAmount - bill.PaidAmount : 0m,
                cancellationToken));
    }

    private async Task<List<AdminTopDebtorResponse>> GetTopDebtorsAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        var debtors = new Dictionary<(Guid ResidentProfileId, Guid CompoundId), DebtorAccumulator>();

        var utilityDebts = await GetUtilityBillsQuery(query)
            .Where(bill => bill.ResidentProfileId.HasValue && bill.BillStatus != BillStatus.Cancelled)
            .GroupBy(bill => new
            {
                ResidentProfileId = bill.ResidentProfileId!.Value,
                ResidentName = bill.ResidentProfile!.FullName,
                bill.CompoundId,
                CompoundName = bill.Compound.Name
            })
            .Select(group => new
            {
                group.Key.ResidentProfileId,
                group.Key.ResidentName,
                group.Key.CompoundId,
                group.Key.CompoundName,
                Debt = group.Sum(bill =>
                    bill.TotalAmount > bill.PaidAmount ? bill.TotalAmount - bill.PaidAmount : 0m)
            })
            .Where(item => item.Debt > 0m)
            .ToArrayAsync(cancellationToken);

        foreach (var item in utilityDebts)
        {
            GetOrAddDebtor(
                    debtors,
                    item.ResidentProfileId,
                    item.ResidentName,
                    item.CompoundId,
                    item.CompoundName)
                .UtilityDebt += item.Debt;
        }

        var rentDebts = await GetRentInvoicesQuery(query)
            .Where(invoice => invoice.RentInvoiceStatus != RentInvoiceStatus.Cancelled)
            .GroupBy(invoice => new
            {
                invoice.ResidentProfileId,
                ResidentName = invoice.ResidentProfile.FullName,
                invoice.CompoundId,
                CompoundName = invoice.Compound.Name
            })
            .Select(group => new
            {
                group.Key.ResidentProfileId,
                group.Key.ResidentName,
                group.Key.CompoundId,
                group.Key.CompoundName,
                Debt = group.Sum(invoice =>
                    invoice.TotalAmount > invoice.PaidAmount ? invoice.TotalAmount - invoice.PaidAmount : 0m)
            })
            .Where(item => item.Debt > 0m)
            .ToArrayAsync(cancellationToken);

        foreach (var item in rentDebts)
        {
            GetOrAddDebtor(
                    debtors,
                    item.ResidentProfileId,
                    item.ResidentName,
                    item.CompoundId,
                    item.CompoundName)
                .RentDebt += item.Debt;
        }

        var installmentDebts = await GetInstallmentsQuery(query)
            .Where(installment => installment.InstallmentStatus != InstallmentStatus.Cancelled)
            .GroupBy(installment => new
            {
                installment.ResidentProfileId,
                ResidentName = installment.ResidentProfile.FullName,
                installment.CompoundId,
                CompoundName = installment.Compound.Name
            })
            .Select(group => new
            {
                group.Key.ResidentProfileId,
                group.Key.ResidentName,
                group.Key.CompoundId,
                group.Key.CompoundName,
                Debt = group.Sum(installment =>
                    installment.Amount > installment.PaidAmount ? installment.Amount - installment.PaidAmount : 0m)
            })
            .Where(item => item.Debt > 0m)
            .ToArrayAsync(cancellationToken);

        foreach (var item in installmentDebts)
        {
            GetOrAddDebtor(
                    debtors,
                    item.ResidentProfileId,
                    item.ResidentName,
                    item.CompoundId,
                    item.CompoundName)
                .InstallmentDebt += item.Debt;
        }

        return debtors.Values
            .Where(debtor => debtor.TotalDebt > 0m)
            .OrderByDescending(debtor => debtor.TotalDebt)
            .ThenBy(debtor => debtor.ResidentName)
            .Take(query.TopCount)
            .Select(debtor => new AdminTopDebtorResponse(
                debtor.ResidentProfileId,
                debtor.ResidentName,
                debtor.CompoundId,
                debtor.CompoundName,
                debtor.TotalDebt,
                debtor.UtilityDebt,
                debtor.RentDebt,
                debtor.InstallmentDebt))
            .ToList();
    }

    private async Task<List<AdminRecentPaymentResponse>> GetRecentPaymentsAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        var payments = await GetPaymentsForSummaryQuery(query)
            .Where(payment => payment.PaymentStatus == PaymentStatus.Pending
                || payment.PaymentStatus == PaymentStatus.Succeeded
                || payment.PaymentStatus == PaymentStatus.Failed)
            .OrderByDescending(payment => payment.CreatedAt)
            .ThenBy(payment => payment.PaymentReference)
            .Take(query.TopCount)
            .Select(payment => new
            {
                payment.Id,
                payment.PaymentReference,
                ResidentName = payment.ResidentProfile == null ? null : payment.ResidentProfile.FullName,
                payment.PaymentMethod,
                payment.PaymentStatus,
                payment.TargetType,
                payment.Amount,
                payment.CreatedAt,
                payment.CompletedAt
            })
            .ToArrayAsync(cancellationToken);

        return payments
            .Select(payment => new AdminRecentPaymentResponse(
                payment.Id,
                payment.PaymentReference,
                payment.ResidentName,
                payment.PaymentMethod.ToString(),
                payment.PaymentStatus.ToString(),
                payment.TargetType.ToString(),
                payment.Amount,
                payment.CreatedAt,
                payment.CompletedAt))
            .ToList();
    }

    private async Task<decimal> SumUtilityOutstandingAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        return await GetUtilityBillsQuery(query)
            .Where(bill => bill.BillStatus != BillStatus.Cancelled)
            .SumAsync(
                bill => bill.TotalAmount > bill.PaidAmount ? bill.TotalAmount - bill.PaidAmount : 0m,
                cancellationToken);
    }

    private async Task<decimal> SumRentOutstandingAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        return await GetRentInvoicesQuery(query)
            .Where(invoice => invoice.RentInvoiceStatus != RentInvoiceStatus.Cancelled)
            .SumAsync(
                invoice => invoice.TotalAmount > invoice.PaidAmount ? invoice.TotalAmount - invoice.PaidAmount : 0m,
                cancellationToken);
    }

    private async Task<decimal> SumInstallmentsOutstandingAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        return await GetInstallmentsQuery(query)
            .Where(installment => installment.InstallmentStatus != InstallmentStatus.Cancelled)
            .SumAsync(
                installment => installment.Amount > installment.PaidAmount
                    ? installment.Amount - installment.PaidAmount
                    : 0m,
                cancellationToken);
    }

    private async Task<decimal> SumSucceededPaymentsAsync(
        AdminOverviewQuery query,
        DateTime? from,
        DateTime? toExclusive,
        PaymentTargetType? targetType,
        CancellationToken cancellationToken)
    {
        var payments = GetSucceededPaymentsQuery(query);

        if (from.HasValue)
        {
            payments = payments.Where(payment => (payment.CompletedAt ?? payment.CreatedAt) >= from.Value);
        }

        if (toExclusive.HasValue)
        {
            payments = payments.Where(payment => (payment.CompletedAt ?? payment.CreatedAt) < toExclusive.Value);
        }

        if (targetType.HasValue)
        {
            payments = payments.Where(payment => payment.TargetType == targetType.Value);
        }

        return await payments.SumAsync(payment => payment.Amount, cancellationToken);
    }

    private IQueryable<Compound> GetCompoundsQuery(AdminOverviewQuery query)
    {
        var compounds = dbContext.Compounds.AsNoTracking();

        return query.CompoundId.HasValue
            ? compounds.Where(compound => compound.Id == query.CompoundId.Value)
            : compounds;
    }

    private IQueryable<PropertyUnit> GetPropertyUnitsQuery(AdminOverviewQuery query)
    {
        var units = dbContext.PropertyUnits.AsNoTracking();

        return query.CompoundId.HasValue
            ? units.Where(unit => unit.CompoundId == query.CompoundId.Value)
            : units;
    }

    private IQueryable<ResidentProfile> GetResidentProfilesQuery(AdminOverviewQuery query)
    {
        var profiles = dbContext.ResidentProfiles.AsNoTracking();

        return query.CompoundId.HasValue
            ? profiles.Where(profile => profile.CompoundId == query.CompoundId.Value)
            : profiles;
    }

    private IQueryable<OccupancyRecord> GetOccupancyRecordsQuery(AdminOverviewQuery query)
    {
        var occupancies = dbContext.OccupancyRecords.AsNoTracking();

        return query.CompoundId.HasValue
            ? occupancies.Where(record => record.CompoundId == query.CompoundId.Value)
            : occupancies;
    }

    private IQueryable<UtilityBill> GetUtilityBillsQuery(AdminOverviewQuery query)
    {
        var bills = dbContext.UtilityBills.AsNoTracking();

        return query.CompoundId.HasValue
            ? bills.Where(bill => bill.CompoundId == query.CompoundId.Value)
            : bills;
    }

    private IQueryable<Payment> GetPaymentsQuery(AdminOverviewQuery query)
    {
        var payments = dbContext.Payments.AsNoTracking();

        return query.CompoundId.HasValue
            ? payments.Where(payment => payment.CompoundId == query.CompoundId.Value)
            : payments;
    }

    private IQueryable<Payment> GetPaymentsForSummaryQuery(AdminOverviewQuery query)
    {
        return ApplyPaymentDateRange(GetPaymentsQuery(query), query);
    }

    private IQueryable<Payment> GetSucceededPaymentsQuery(AdminOverviewQuery query)
    {
        return GetPaymentsForSummaryQuery(query)
            .Where(payment => payment.PaymentStatus == PaymentStatus.Succeeded);
    }

    private IQueryable<PropertySaleContract> GetSaleContractsQuery(AdminOverviewQuery query)
    {
        var contracts = dbContext.PropertySaleContracts.AsNoTracking();

        return query.CompoundId.HasValue
            ? contracts.Where(contract => contract.CompoundId == query.CompoundId.Value)
            : contracts;
    }

    private IQueryable<InstallmentScheduleItem> GetInstallmentsQuery(AdminOverviewQuery query)
    {
        var installments = dbContext.InstallmentScheduleItems.AsNoTracking();

        return query.CompoundId.HasValue
            ? installments.Where(installment => installment.CompoundId == query.CompoundId.Value)
            : installments;
    }

    private IQueryable<RentContract> GetRentContractsQuery(AdminOverviewQuery query)
    {
        var contracts = dbContext.RentContracts.AsNoTracking();

        return query.CompoundId.HasValue
            ? contracts.Where(contract => contract.CompoundId == query.CompoundId.Value)
            : contracts;
    }

    private IQueryable<RentInvoice> GetRentInvoicesQuery(AdminOverviewQuery query)
    {
        var invoices = dbContext.RentInvoices.AsNoTracking();

        return query.CompoundId.HasValue
            ? invoices.Where(invoice => invoice.CompoundId == query.CompoundId.Value)
            : invoices;
    }

    private static IQueryable<Payment> ApplyPaymentDateRange(
        IQueryable<Payment> payments,
        AdminOverviewQuery query)
    {
        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value.ToDateTime(TimeOnly.MinValue);
            payments = payments.Where(payment => (payment.CompletedAt ?? payment.CreatedAt) >= from);
        }

        if (query.ToDate.HasValue)
        {
            var toExclusive = query.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            payments = payments.Where(payment => (payment.CompletedAt ?? payment.CreatedAt) < toExclusive);
        }

        return payments;
    }

    private async Task<ValidationFailure?> ValidateQueryAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        if (query.TopCount is < MinTopCount or > MaxTopCount)
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                $"TopCount must be between {MinTopCount} and {MaxTopCount}.");
        }

        if (query.FromDate.HasValue
            && query.ToDate.HasValue
            && query.FromDate.Value > query.ToDate.Value)
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "FromDate cannot be after ToDate.");
        }

        if (!query.CompoundId.HasValue || query.CompoundId.Value == Guid.Empty)
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "CompoundId is required for admin dashboard/reporting endpoints.");
        }

        var compoundExists = await dbContext.Compounds
            .AsNoTracking()
            .AnyAsync(compound => compound.Id == query.CompoundId.Value, cancellationToken);

        if (!compoundExists)
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Compound was not found.");
        }

        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(
                query.CompoundId.Value,
                cancellationToken)
            ? null
            : new ValidationFailure(ServiceResultStatus.Forbidden, "Current user cannot access this compound.");
    }

    private static DebtorAccumulator GetOrAddDebtor(
        Dictionary<(Guid ResidentProfileId, Guid CompoundId), DebtorAccumulator> debtors,
        Guid residentProfileId,
        string residentName,
        Guid compoundId,
        string compoundName)
    {
        var key = (residentProfileId, compoundId);
        if (debtors.TryGetValue(key, out var debtor))
        {
            return debtor;
        }

        debtor = new DebtorAccumulator(residentProfileId, residentName, compoundId, compoundName);
        debtors[key] = debtor;
        return debtor;
    }

    private static DateTime GetCurrentMonthStartUtc()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
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

    private sealed class DebtorAccumulator(
        Guid residentProfileId,
        string residentName,
        Guid compoundId,
        string compoundName)
    {
        public Guid ResidentProfileId { get; } = residentProfileId;

        public string ResidentName { get; } = residentName;

        public Guid CompoundId { get; } = compoundId;

        public string CompoundName { get; } = compoundName;

        public decimal UtilityDebt { get; set; }

        public decimal RentDebt { get; set; }

        public decimal InstallmentDebt { get; set; }

        public decimal TotalDebt => UtilityDebt + RentDebt + InstallmentDebt;
    }
}
