using System.Linq.Expressions;
using DARAK.Api.Data;
using DARAK.Api.DTOs.BillingCycles;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.CompoundServices;
using DARAK.Api.DTOs.UtilityBills;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class BillingCycleService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IBillingCycleService
{
    private static readonly UnitStatus[] OccupiedBillingStatuses =
    [
        UnitStatus.Rented,
        UnitStatus.SoldCash,
        UnitStatus.SoldInstallment,
        UnitStatus.Occupied
    ];

    private static readonly UnitStatus[] OptionalAvailableBillingStatuses =
    [
        UnitStatus.Rented,
        UnitStatus.SoldCash,
        UnitStatus.SoldInstallment,
        UnitStatus.Occupied,
        UnitStatus.Available
    ];

    public async Task<PagedResult<BillingCycleResponse>> SearchBillingCyclesAsync(
        BillingCycleSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var billingCycles = await ApplyCurrentBillingCycleScopeAsync(
            ApplyBillingCycleFilters(
                dbContext.BillingCycles.AsNoTracking(),
                query),
            cancellationToken);

        return await ToPagedResultAsync(
            billingCycles
                .OrderByDescending(cycle => cycle.Year)
                .ThenByDescending(cycle => cycle.Month)
                .ThenBy(cycle => cycle.Compound.Name),
            query,
            cycle => new BillingCycleResponse(
                cycle.Id,
                cycle.CompoundId,
                cycle.Compound.Name,
                cycle.Year,
                cycle.Month,
                cycle.PeriodStart,
                cycle.PeriodEnd,
                cycle.DueDate,
                cycle.IsClosed,
                cycle.CreatedAt,
                cycle.UpdatedAt),
            cancellationToken);
    }

    public async Task<ServiceResult<BillingCycleResponse>> GetBillingCycleAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var billingCycles = await ApplyCurrentBillingCycleScopeAsync(
            dbContext.BillingCycles
                .AsNoTracking()
                .Include(cycle => cycle.Compound),
            cancellationToken);

        var billingCycle = await billingCycles
            .FirstOrDefaultAsync(cycle => cycle.Id == id, cancellationToken);

        return billingCycle is null
            ? ServiceResult<BillingCycleResponse>.NotFound("Billing cycle was not found.")
            : ServiceResult<BillingCycleResponse>.Success(ToBillingCycleResponse(billingCycle));
    }

    public async Task<ServiceResult<BillingCycleResponse>> CreateBillingCycleAsync(
        CreateBillingCycleRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = await ValidateActiveCompoundAsync(request.CompoundId, cancellationToken)
            ?? ValidateBillingCycleDates(
                request.Year,
                request.Month,
                request.PeriodStart,
                request.PeriodEnd,
                request.DueDate);

        if (validationFailure is not null)
        {
            return ToResult<BillingCycleResponse>(validationFailure);
        }

        var duplicateExists = await dbContext.BillingCycles.AnyAsync(
            cycle => cycle.CompoundId == request.CompoundId
                && cycle.Year == request.Year
                && cycle.Month == request.Month,
            cancellationToken);

        if (duplicateExists)
        {
            return ServiceResult<BillingCycleResponse>.Conflict(
                "Billing cycle already exists for this compound, year, and month.");
        }

        var billingCycle = new BillingCycle
        {
            CompoundId = request.CompoundId,
            Year = request.Year,
            Month = request.Month,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            DueDate = request.DueDate
        };

        dbContext.BillingCycles.Add(billingCycle);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetBillingCycleAsync(billingCycle.Id, cancellationToken);
    }

    public async Task<ServiceResult<BillingCycleResponse>> UpdateBillingCycleAsync(
        Guid id,
        UpdateBillingCycleRequest request,
        CancellationToken cancellationToken = default)
    {
        var billingCycle = await dbContext.BillingCycles
            .Include(cycle => cycle.Compound)
            .FirstOrDefaultAsync(cycle => cycle.Id == id, cancellationToken);

        if (billingCycle is null)
        {
            return ServiceResult<BillingCycleResponse>.NotFound("Billing cycle was not found.");
        }

        if (!await CanCurrentUserAccessCompoundAsync(billingCycle.CompoundId, cancellationToken))
        {
            return ServiceResult<BillingCycleResponse>.NotFound("Billing cycle was not found.");
        }

        if (!billingCycle.Compound.IsActive)
        {
            return ServiceResult<BillingCycleResponse>.BadRequest("Compound is inactive.");
        }

        if (billingCycle.IsClosed)
        {
            return ServiceResult<BillingCycleResponse>.BadRequest("Closed billing cycle cannot be updated.");
        }

        var validationFailure = ValidateBillingCycleDates(
            request.Year,
            request.Month,
            request.PeriodStart,
            request.PeriodEnd,
            request.DueDate);

        if (validationFailure is not null)
        {
            return ToResult<BillingCycleResponse>(validationFailure);
        }

        var duplicateExists = await dbContext.BillingCycles.AnyAsync(
            cycle => cycle.Id != id
                && cycle.CompoundId == billingCycle.CompoundId
                && cycle.Year == request.Year
                && cycle.Month == request.Month,
            cancellationToken);

        if (duplicateExists)
        {
            return ServiceResult<BillingCycleResponse>.Conflict(
                "Billing cycle already exists for this compound, year, and month.");
        }

        billingCycle.Year = request.Year;
        billingCycle.Month = request.Month;
        billingCycle.PeriodStart = request.PeriodStart;
        billingCycle.PeriodEnd = request.PeriodEnd;
        billingCycle.DueDate = request.DueDate;
        billingCycle.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<BillingCycleResponse>.Success(ToBillingCycleResponse(billingCycle));
    }

    public async Task<ServiceResult<BillingCycleResponse>> CloseBillingCycleAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var billingCycle = await dbContext.BillingCycles
            .Include(cycle => cycle.Compound)
            .FirstOrDefaultAsync(cycle => cycle.Id == id, cancellationToken);

        if (billingCycle is null)
        {
            return ServiceResult<BillingCycleResponse>.NotFound("Billing cycle was not found.");
        }

        if (!await CanCurrentUserAccessCompoundAsync(billingCycle.CompoundId, cancellationToken))
        {
            return ServiceResult<BillingCycleResponse>.NotFound("Billing cycle was not found.");
        }

        billingCycle.IsClosed = true;
        billingCycle.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<BillingCycleResponse>.Success(ToBillingCycleResponse(billingCycle));
    }

    private static IQueryable<CompoundService> ApplyCompoundServiceFilters(
        IQueryable<CompoundService> compoundServices,
        CompoundServiceSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            compoundServices = compoundServices.Where(service => service.CompoundId == query.CompoundId.Value);
        }

        if (query.ServiceType.HasValue)
        {
            compoundServices = compoundServices.Where(service => service.ServiceType == query.ServiceType.Value);
        }

        if (query.IsActive.HasValue)
        {
            compoundServices = compoundServices.Where(service => service.IsActive == query.IsActive.Value);
        }

        if (HasText(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm!.Trim();
            compoundServices = compoundServices.Where(service =>
                service.Name.Contains(searchTerm)
                || (service.Description != null && service.Description.Contains(searchTerm)));
        }

        return compoundServices;
    }

    private static IQueryable<BillingCycle> ApplyBillingCycleFilters(
        IQueryable<BillingCycle> billingCycles,
        BillingCycleSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            billingCycles = billingCycles.Where(cycle => cycle.CompoundId == query.CompoundId.Value);
        }

        if (query.Year.HasValue)
        {
            billingCycles = billingCycles.Where(cycle => cycle.Year == query.Year.Value);
        }

        if (query.Month.HasValue)
        {
            billingCycles = billingCycles.Where(cycle => cycle.Month == query.Month.Value);
        }

        if (query.IsClosed.HasValue)
        {
            billingCycles = billingCycles.Where(cycle => cycle.IsClosed == query.IsClosed.Value);
        }

        return billingCycles;
    }

    private static IQueryable<UtilityBill> ApplyUtilityBillFilters(
        IQueryable<UtilityBill> utilityBills,
        UtilityBillSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            utilityBills = utilityBills.Where(bill => bill.CompoundId == query.CompoundId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            utilityBills = utilityBills.Where(bill => bill.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            utilityBills = utilityBills.Where(bill => bill.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.BillingCycleId.HasValue)
        {
            utilityBills = utilityBills.Where(bill => bill.BillingCycleId == query.BillingCycleId.Value);
        }

        if (query.BillStatus.HasValue)
        {
            utilityBills = utilityBills.Where(bill => bill.BillStatus == query.BillStatus.Value);
        }

        if (query.Year.HasValue)
        {
            utilityBills = utilityBills.Where(bill => bill.BillingCycle.Year == query.Year.Value);
        }

        if (query.Month.HasValue)
        {
            utilityBills = utilityBills.Where(bill => bill.BillingCycle.Month == query.Month.Value);
        }

        if (query.DueBefore.HasValue)
        {
            utilityBills = utilityBills.Where(bill => bill.DueDate <= query.DueBefore.Value);
        }

        if (query.DueAfter.HasValue)
        {
            utilityBills = utilityBills.Where(bill => bill.DueDate >= query.DueAfter.Value);
        }

        if (HasText(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm!.Trim();
            utilityBills = utilityBills.Where(bill =>
                bill.BillNumber.Contains(searchTerm)
                || bill.PropertyUnit.UnitNumber.Contains(searchTerm));
        }

        return utilityBills;
    }

    private IQueryable<UtilityBill> GetUtilityBillDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.UtilityBills
            .Include(bill => bill.Compound)
            .Include(bill => bill.PropertyUnit)
            .Include(bill => bill.ResidentProfile)
            .Include(bill => bill.BillingCycle)
            .Include(bill => bill.Lines)
            .ThenInclude(line => line.CompoundService)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private async Task<ServiceResult<IReadOnlyCollection<UtilityBillLine>>> BuildUtilityBillLinesAsync(
        Guid compoundId,
        IReadOnlyCollection<AddUtilityBillLineRequest> lineRequests,
        CancellationToken cancellationToken)
    {
        if (lineRequests.Count == 0)
        {
            return ServiceResult<IReadOnlyCollection<UtilityBillLine>>.BadRequest(
                "Utility bill must contain at least one line.");
        }

        var serviceIds = lineRequests
            .Select(line => line.CompoundServiceId)
            .Distinct()
            .ToArray();

        if (serviceIds.Any(serviceId => serviceId == Guid.Empty))
        {
            return ServiceResult<IReadOnlyCollection<UtilityBillLine>>.BadRequest(
                "Compound service id is required for every bill line.");
        }

        var services = await dbContext.CompoundServices
            .AsNoTracking()
            .Where(service => serviceIds.Contains(service.Id))
            .ToDictionaryAsync(service => service.Id, cancellationToken);

        if (services.Count != serviceIds.Length)
        {
            return ServiceResult<IReadOnlyCollection<UtilityBillLine>>.NotFound(
                "One or more compound services were not found.");
        }

        var lines = new List<UtilityBillLine>();
        foreach (var lineRequest in lineRequests)
        {
            var service = services[lineRequest.CompoundServiceId];
            if (service.CompoundId != compoundId)
            {
                return ServiceResult<IReadOnlyCollection<UtilityBillLine>>.BadRequest(
                    "All bill line services must belong to the bill compound.");
            }

            if (!service.IsActive)
            {
                return ServiceResult<IReadOnlyCollection<UtilityBillLine>>.BadRequest(
                    "Compound service is inactive.");
            }

            if (lineRequest.Quantity <= 0)
            {
                return ServiceResult<IReadOnlyCollection<UtilityBillLine>>.BadRequest(
                    "Bill line quantity must be greater than zero.");
            }

            var unitPrice = lineRequest.UnitPrice ?? service.DefaultMonthlyFee;
            if (unitPrice < 0)
            {
                return ServiceResult<IReadOnlyCollection<UtilityBillLine>>.BadRequest(
                    "Bill line unit price cannot be negative.");
            }

            var description = TrimOrNull(lineRequest.Description) ?? service.Name;
            var lineTotal = Math.Round(
                lineRequest.Quantity * unitPrice,
                2,
                MidpointRounding.AwayFromZero);

            lines.Add(new UtilityBillLine
            {
                CompoundServiceId = service.Id,
                Description = description,
                Quantity = lineRequest.Quantity,
                UnitPrice = unitPrice,
                LineTotal = lineTotal
            });
        }

        return ServiceResult<IReadOnlyCollection<UtilityBillLine>>.Success(lines);
    }

    private async Task<decimal> CalculatePreviousBalanceAsync(
        Guid propertyUnitId,
        int cycleYear,
        int cycleMonth,
        CancellationToken cancellationToken)
    {
        var previousBalances = await dbContext.UtilityBills
            .AsNoTracking()
            .Where(bill => bill.PropertyUnitId == propertyUnitId
                && bill.BillStatus != BillStatus.Cancelled
                && bill.BillStatus != BillStatus.Paid
                && (bill.BillingCycle.Year < cycleYear
                    || (bill.BillingCycle.Year == cycleYear && bill.BillingCycle.Month < cycleMonth)))
            .Select(bill => new { bill.TotalAmount, bill.PaidAmount })
            .ToArrayAsync(cancellationToken);

        return previousBalances.Sum(bill => Math.Max(0m, bill.TotalAmount - bill.PaidAmount));
    }

    private async Task<Guid?> GetActiveResidentProfileIdForUnitAsync(
        Guid compoundId,
        Guid propertyUnitId,
        CancellationToken cancellationToken)
    {
        return await dbContext.OccupancyRecords
            .AsNoTracking()
            .Where(record => record.CompoundId == compoundId
                && record.PropertyUnitId == propertyUnitId
                && record.OccupancyStatus == OccupancyStatus.Active
                && record.ResidentProfile.IsActive)
            .Select(record => (Guid?)record.ResidentProfileId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<ResidentBillingScope> GetResidentBillingScopeAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var profileIds = await dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId && profile.IsActive)
            .Select(profile => profile.Id)
            .ToArrayAsync(cancellationToken);

        return new ResidentBillingScope(profileIds);
    }

    private async Task<ValidationFailure?> ValidateActiveCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        if (compoundId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Compound id is required.");
        }

        var compound = await dbContext.Compounds
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == compoundId, cancellationToken);

        if (compound is null)
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Compound was not found.");
        }

        if (!compound.IsActive)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Compound is inactive.");
        }

        return await CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken)
            ? null
            : new ValidationFailure(ServiceResultStatus.Forbidden, "Current user cannot access this compound.");
    }

    private async Task<(PropertyUnit? Unit, ValidationFailure? Failure)> GetValidatedPropertyUnitAsync(
        Guid compoundId,
        Guid propertyUnitId,
        CancellationToken cancellationToken)
    {
        if (propertyUnitId == Guid.Empty)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Property unit id is required."));
        }

        var propertyUnit = await dbContext.PropertyUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(unit => unit.Id == propertyUnitId, cancellationToken);

        if (propertyUnit is null)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Property unit was not found."));
        }

        if (!propertyUnit.IsActive)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Property unit is inactive."));
        }

        if (propertyUnit.CompoundId != compoundId)
        {
            return (null, new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "Property unit must belong to the bill compound."));
        }

        return (propertyUnit, null);
    }

    private async Task<(BillingCycle? Cycle, ValidationFailure? Failure)> GetBillingCycleForGenerationAsync(
        Guid compoundId,
        Guid billingCycleId,
        CancellationToken cancellationToken)
    {
        if (billingCycleId == Guid.Empty)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Billing cycle id is required."));
        }

        var billingCycle = await dbContext.BillingCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(cycle => cycle.Id == billingCycleId, cancellationToken);

        if (billingCycle is null)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Billing cycle was not found."));
        }

        if (billingCycle.CompoundId != compoundId)
        {
            return (null, new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "Billing cycle must belong to the bill compound."));
        }

        if (billingCycle.IsClosed)
        {
            return (null, new ValidationFailure(
                ServiceResultStatus.Conflict,
                "Closed billing cycle cannot generate new bills."));
        }

        return (billingCycle, null);
    }

    private static ValidationFailure? ValidateBillingCycleDates(
        int year,
        int month,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly dueDate)
    {
        if (year is < 2000 or > 2100)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Billing cycle year is invalid.");
        }

        if (month is < 1 or > 12)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Billing cycle month must be from 1 to 12.");
        }

        if (periodStart == default || periodEnd == default || dueDate == default)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Billing cycle dates are required.");
        }

        if (periodEnd <= periodStart)
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "Billing cycle period end must be after period start.");
        }

        if (dueDate < periodStart)
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "Billing cycle due date must be on or after period start.");
        }

        return null;
    }

    private static ValidationFailure? ValidateNonNegativeAmounts(
        decimal previousBalanceAmount,
        decimal lateFeeAmount,
        decimal discountAmount,
        decimal paidAmount)
    {
        if (previousBalanceAmount < 0)
        {
            return new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "Previous balance amount cannot be negative.");
        }

        if (lateFeeAmount < 0)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Late fee amount cannot be negative.");
        }

        if (discountAmount < 0)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Discount amount cannot be negative.");
        }

        if (paidAmount < 0)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Paid amount cannot be negative.");
        }

        return null;
    }

    private async Task<PagedResult<UtilityBillResponse>> ToPagedUtilityBillResultAsync(
        IQueryable<UtilityBill> query,
        PaginationQuery pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var bills = await query
            .OrderByDescending(bill => bill.IssueDate)
            .ThenByDescending(bill => bill.CreatedAt)
            .ThenBy(bill => bill.BillNumber)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<UtilityBillResponse>(
            bills.Select(ToUtilityBillResponse).ToArray(),
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private static async Task<PagedResult<TResponse>> ToPagedResultAsync<TSource, TResponse>(
        IQueryable<TSource> query,
        PaginationQuery pagination,
        Expression<Func<TSource, TResponse>> selector,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(selector)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<TResponse>(
            items,
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
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

    private async Task<IQueryable<CompoundService>> ApplyCurrentCompoundServiceScopeAsync(
        IQueryable<CompoundService> compoundServices,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return compoundServices;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return compoundServices.ApplyCompoundAccess(scope, service => service.CompoundId);
    }

    private async Task<IQueryable<BillingCycle>> ApplyCurrentBillingCycleScopeAsync(
        IQueryable<BillingCycle> billingCycles,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return billingCycles;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return billingCycles.ApplyCompoundAccess(scope, cycle => cycle.CompoundId);
    }

    private async Task<IQueryable<UtilityBill>> ApplyCurrentUtilityBillScopeAsync(
        IQueryable<UtilityBill> utilityBills,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return utilityBills;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return utilityBills.ApplyCompoundAccess(scope, bill => bill.CompoundId);
    }

    private async Task<bool> CanCurrentUserAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private static BillStatus DetermineBillStatus(
        decimal paidAmount,
        decimal totalAmount,
        DateOnly dueDate,
        bool isCancelled)
    {
        if (isCancelled)
        {
            return BillStatus.Cancelled;
        }

        if (paidAmount >= totalAmount)
        {
            return BillStatus.Paid;
        }

        if (paidAmount > 0)
        {
            return BillStatus.PartiallyPaid;
        }

        return dueDate < DateOnly.FromDateTime(DateTime.UtcNow)
            ? BillStatus.Overdue
            : BillStatus.Unpaid;
    }

    private static string GenerateBillNumber(int year, int month, string unitNumber)
    {
        var sanitizedUnitNumber = new string(unitNumber
            .Where(char.IsLetterOrDigit)
            .Take(12)
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitizedUnitNumber))
        {
            sanitizedUnitNumber = "UNIT";
        }

        return $"UB-{year}{month:00}-{sanitizedUnitNumber}-{Guid.NewGuid().ToString("N")[..8]}";
    }

    private static CompoundServiceResponse ToCompoundServiceResponse(CompoundService compoundService)
    {
        return new CompoundServiceResponse(
            compoundService.Id,
            compoundService.CompoundId,
            compoundService.Compound.Name,
            compoundService.ServiceType,
            compoundService.Name,
            compoundService.Description,
            compoundService.DefaultMonthlyFee,
            compoundService.IsMeterBased,
            compoundService.IsActive,
            compoundService.CreatedAt,
            compoundService.UpdatedAt);
    }

    private static BillingCycleResponse ToBillingCycleResponse(BillingCycle billingCycle)
    {
        return new BillingCycleResponse(
            billingCycle.Id,
            billingCycle.CompoundId,
            billingCycle.Compound.Name,
            billingCycle.Year,
            billingCycle.Month,
            billingCycle.PeriodStart,
            billingCycle.PeriodEnd,
            billingCycle.DueDate,
            billingCycle.IsClosed,
            billingCycle.CreatedAt,
            billingCycle.UpdatedAt);
    }

    private static UtilityBillResponse ToUtilityBillResponse(UtilityBill utilityBill)
    {
        var lines = utilityBill.Lines
            .OrderBy(line => line.Description)
            .ThenBy(line => line.CreatedAt)
            .Select(line => new UtilityBillLineResponse(
                line.Id,
                line.UtilityBillId,
                line.CompoundServiceId,
                line.CompoundService.Name,
                line.Description,
                line.Quantity,
                line.UnitPrice,
                line.LineTotal,
                line.CreatedAt))
            .ToArray();

        return new UtilityBillResponse(
            utilityBill.Id,
            utilityBill.CompoundId,
            utilityBill.Compound.Name,
            utilityBill.PropertyUnitId,
            utilityBill.PropertyUnit.UnitNumber,
            utilityBill.ResidentProfileId,
            utilityBill.ResidentProfile?.FullName,
            utilityBill.BillingCycleId,
            utilityBill.BillingCycle.Year,
            utilityBill.BillingCycle.Month,
            utilityBill.BillNumber,
            utilityBill.BillStatus,
            utilityBill.IssueDate,
            utilityBill.DueDate,
            utilityBill.SubtotalAmount,
            utilityBill.PreviousBalanceAmount,
            utilityBill.LateFeeAmount,
            utilityBill.DiscountAmount,
            utilityBill.TotalAmount,
            utilityBill.PaidAmount,
            CalculateRemainingAmount(utilityBill),
            utilityBill.Notes,
            utilityBill.CreatedAt,
            utilityBill.UpdatedAt,
            utilityBill.CancelledAt,
            utilityBill.CancellationReason,
            lines);
    }

    private static decimal CalculateRemainingAmount(UtilityBill utilityBill)
    {
        return utilityBill.BillStatus == BillStatus.Cancelled
            ? 0m
            : Math.Max(0m, utilityBill.TotalAmount - utilityBill.PaidAmount);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool HasText(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);

    private sealed record ResidentBillingScope(Guid[] ProfileIds);
}
