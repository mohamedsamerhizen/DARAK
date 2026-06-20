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

public sealed class CompoundServiceCatalogService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : ICompoundServiceCatalogService
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

    public async Task<PagedResult<CompoundServiceResponse>> SearchCompoundServicesAsync(
        CompoundServiceSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var compoundServices = await ApplyCurrentCompoundServiceScopeAsync(
            ApplyCompoundServiceFilters(
                dbContext.CompoundServices.AsNoTracking(),
                query),
            cancellationToken);

        return await ToPagedResultAsync(
            compoundServices
                .OrderBy(service => service.Compound.Name)
                .ThenBy(service => service.Name),
            query,
            service => new CompoundServiceResponse(
                service.Id,
                service.CompoundId,
                service.Compound.Name,
                service.ServiceType,
                service.Name,
                service.Description,
                service.DefaultMonthlyFee,
                service.IsMeterBased,
                service.IsActive,
                service.CreatedAt,
                service.UpdatedAt),
            cancellationToken);
    }

    public async Task<ServiceResult<CompoundServiceResponse>> GetCompoundServiceAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var compoundServices = await ApplyCurrentCompoundServiceScopeAsync(
            dbContext.CompoundServices
                .AsNoTracking()
                .Include(service => service.Compound),
            cancellationToken);

        var compoundService = await compoundServices
            .AsNoTracking()
            .FirstOrDefaultAsync(service => service.Id == id, cancellationToken);

        return compoundService is null
            ? ServiceResult<CompoundServiceResponse>.NotFound("Compound service was not found.")
            : ServiceResult<CompoundServiceResponse>.Success(ToCompoundServiceResponse(compoundService));
    }

    public async Task<ServiceResult<CompoundServiceResponse>> CreateCompoundServiceAsync(
        CreateCompoundServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = await ValidateActiveCompoundAsync(request.CompoundId, cancellationToken);
        if (validationFailure is not null)
        {
            return ToResult<CompoundServiceResponse>(validationFailure);
        }

        var name = TrimOrNull(request.Name);
        if (name is null)
        {
            return ServiceResult<CompoundServiceResponse>.BadRequest("Compound service name is required.");
        }

        if (request.DefaultMonthlyFee < 0)
        {
            return ServiceResult<CompoundServiceResponse>.BadRequest(
                "Default monthly fee cannot be negative.");
        }

        var duplicateNameExists = await dbContext.CompoundServices.AnyAsync(
            service => service.CompoundId == request.CompoundId && service.Name == name,
            cancellationToken);

        if (duplicateNameExists)
        {
            return ServiceResult<CompoundServiceResponse>.Conflict(
                "Compound service name already exists in this compound.");
        }

        var duplicateActiveTypeExists = await dbContext.CompoundServices.AnyAsync(
            service => service.CompoundId == request.CompoundId
                && service.ServiceType == request.ServiceType
                && service.IsActive,
            cancellationToken);

        if (duplicateActiveTypeExists)
        {
            return ServiceResult<CompoundServiceResponse>.Conflict(
                "An active compound service already exists for this service type.");
        }

        var compoundService = new CompoundService
        {
            CompoundId = request.CompoundId,
            ServiceType = request.ServiceType,
            Name = name,
            Description = TrimOrNull(request.Description),
            DefaultMonthlyFee = request.DefaultMonthlyFee,
            IsMeterBased = request.IsMeterBased
        };

        dbContext.CompoundServices.Add(compoundService);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetCompoundServiceAsync(compoundService.Id, cancellationToken);
    }

    public async Task<ServiceResult<CompoundServiceResponse>> UpdateCompoundServiceAsync(
        Guid id,
        UpdateCompoundServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var compoundService = await dbContext.CompoundServices
            .Include(service => service.Compound)
            .FirstOrDefaultAsync(service => service.Id == id, cancellationToken);

        if (compoundService is null)
        {
            return ServiceResult<CompoundServiceResponse>.NotFound("Compound service was not found.");
        }

        if (!await CanCurrentUserAccessCompoundAsync(compoundService.CompoundId, cancellationToken))
        {
            return ServiceResult<CompoundServiceResponse>.NotFound("Compound service was not found.");
        }

        if (!compoundService.Compound.IsActive)
        {
            return ServiceResult<CompoundServiceResponse>.BadRequest("Compound is inactive.");
        }

        var name = TrimOrNull(request.Name);
        if (name is null)
        {
            return ServiceResult<CompoundServiceResponse>.BadRequest("Compound service name is required.");
        }

        if (request.DefaultMonthlyFee < 0)
        {
            return ServiceResult<CompoundServiceResponse>.BadRequest(
                "Default monthly fee cannot be negative.");
        }

        var duplicateNameExists = await dbContext.CompoundServices.AnyAsync(
            service => service.Id != id
                && service.CompoundId == compoundService.CompoundId
                && service.Name == name,
            cancellationToken);

        if (duplicateNameExists)
        {
            return ServiceResult<CompoundServiceResponse>.Conflict(
                "Compound service name already exists in this compound.");
        }

        if (request.IsActive)
        {
            var duplicateActiveTypeExists = await dbContext.CompoundServices.AnyAsync(
                service => service.Id != id
                    && service.CompoundId == compoundService.CompoundId
                    && service.ServiceType == request.ServiceType
                    && service.IsActive,
                cancellationToken);

            if (duplicateActiveTypeExists)
            {
                return ServiceResult<CompoundServiceResponse>.Conflict(
                    "An active compound service already exists for this service type.");
            }
        }

        compoundService.ServiceType = request.ServiceType;
        compoundService.Name = name;
        compoundService.Description = TrimOrNull(request.Description);
        compoundService.DefaultMonthlyFee = request.DefaultMonthlyFee;
        compoundService.IsMeterBased = request.IsMeterBased;
        compoundService.IsActive = request.IsActive;
        compoundService.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<CompoundServiceResponse>.Success(ToCompoundServiceResponse(compoundService));
    }

    public async Task<ServiceResult<object?>> DeactivateCompoundServiceAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var compoundService = await dbContext.CompoundServices
            .FirstOrDefaultAsync(service => service.Id == id, cancellationToken);

        if (compoundService is null)
        {
            return ServiceResult<object?>.NotFound("Compound service was not found.");
        }

        if (!await CanCurrentUserAccessCompoundAsync(compoundService.CompoundId, cancellationToken))
        {
            return ServiceResult<object?>.NotFound("Compound service was not found.");
        }

        compoundService.IsActive = false;
        compoundService.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object?>.Success(null);
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
