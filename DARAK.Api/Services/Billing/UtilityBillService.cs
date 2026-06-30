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

public sealed class UtilityBillService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IUtilityBillService
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

    public async Task<PagedResult<UtilityBillResponse>> SearchUtilityBillsAsync(
        UtilityBillSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var utilityBills = await ApplyCurrentUtilityBillScopeAsync(
            ApplyUtilityBillFilters(
                GetUtilityBillDetailsQuery(asNoTracking: true),
                query),
            cancellationToken);

        return await ToPagedUtilityBillResultAsync(utilityBills, query, cancellationToken);
    }

    public async Task<ServiceResult<UtilityBillResponse>> GetUtilityBillAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var utilityBills = await ApplyCurrentUtilityBillScopeAsync(
            GetUtilityBillDetailsQuery(asNoTracking: true),
            cancellationToken);

        var utilityBill = await utilityBills
            .FirstOrDefaultAsync(bill => bill.Id == id, cancellationToken);

        return utilityBill is null
            ? ServiceResult<UtilityBillResponse>.NotFound("Utility bill was not found.")
            : ServiceResult<UtilityBillResponse>.Success(ToUtilityBillResponse(utilityBill));
    }

    public async Task<ServiceResult<UtilityBillResponse>> GenerateUtilityBillAsync(
        GenerateUtilityBillRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = await ValidateActiveCompoundAsync(request.CompoundId, cancellationToken);
        if (validationFailure is not null)
        {
            return ToResult<UtilityBillResponse>(validationFailure);
        }

        var unitResult = await GetValidatedPropertyUnitAsync(
            request.CompoundId,
            request.PropertyUnitId,
            cancellationToken);
        if (unitResult.Failure is not null)
        {
            return ToResult<UtilityBillResponse>(unitResult.Failure);
        }

        var cycleResult = await GetBillingCycleForGenerationAsync(
            request.CompoundId,
            request.BillingCycleId,
            cancellationToken);
        if (cycleResult.Failure is not null)
        {
            return ToResult<UtilityBillResponse>(cycleResult.Failure);
        }

        var amountFailure = ValidateNonNegativeAmounts(
            request.PreviousBalanceAmount ?? 0m,
            request.LateFeeAmount ?? 0m,
            request.DiscountAmount ?? 0m,
            0m);
        if (amountFailure is not null)
        {
            return ToResult<UtilityBillResponse>(amountFailure);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var duplicateExists = await dbContext.UtilityBills.AnyAsync(
            bill => bill.PropertyUnitId == request.PropertyUnitId
                && bill.BillingCycleId == request.BillingCycleId,
            cancellationToken);

        if (duplicateExists)
        {
            return ServiceResult<UtilityBillResponse>.Conflict(
                "Utility bill already exists for this property unit and billing cycle.");
        }

        var linesResult = await BuildUtilityBillLinesAsync(
            request.CompoundId,
            request.Lines,
            cancellationToken);
        if (!linesResult.IsSuccess)
        {
            return ToResult<UtilityBillResponse>(
                new ValidationFailure(linesResult.Status, linesResult.Message ?? "Invalid bill lines."));
        }

        var cycle = cycleResult.Cycle!;
        var unit = unitResult.Unit!;
        var previousBalance = request.PreviousBalanceAmount
            ?? await CalculatePreviousBalanceAsync(unit.Id, cycle.Year, cycle.Month, cancellationToken);
        var lateFeeAmount = request.LateFeeAmount ?? 0m;
        var discountAmount = request.DiscountAmount ?? 0m;
        var subtotalAmount = linesResult.Value!.Sum(line => line.LineTotal);
        var totalAmount = subtotalAmount + previousBalance + lateFeeAmount - discountAmount;

        if (totalAmount < 0)
        {
            return ServiceResult<UtilityBillResponse>.BadRequest("Total amount cannot be negative.");
        }

        var residentProfileId = await GetActiveResidentProfileIdForUnitAsync(
            request.CompoundId,
            unit.Id,
            cancellationToken);

        var utilityBill = new UtilityBill
        {
            CompoundId = request.CompoundId,
            PropertyUnitId = unit.Id,
            ResidentProfileId = residentProfileId,
            BillingCycleId = cycle.Id,
            BillNumber = GenerateBillNumber(cycle.Year, cycle.Month, unit.UnitNumber),
            BillStatus = DetermineBillStatus(0m, totalAmount, cycle.DueDate, isCancelled: false),
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = cycle.DueDate,
            SubtotalAmount = subtotalAmount,
            PreviousBalanceAmount = previousBalance,
            LateFeeAmount = lateFeeAmount,
            DiscountAmount = discountAmount,
            TotalAmount = totalAmount,
            PaidAmount = 0m,
            Notes = TrimOrNull(request.Notes),
            Lines = linesResult.Value!.ToList()
        };

        dbContext.UtilityBills.Add(utilityBill);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetUtilityBillAsync(utilityBill.Id, cancellationToken);
    }

    public async Task<ServiceResult<GenerateMonthlyUtilityBillsResponse>> GenerateMonthlyUtilityBillsAsync(
        GenerateMonthlyUtilityBillsRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = await ValidateActiveCompoundAsync(request.CompoundId, cancellationToken);
        if (validationFailure is not null)
        {
            return ToResult<GenerateMonthlyUtilityBillsResponse>(validationFailure);
        }

        var cycleResult = await GetBillingCycleForGenerationAsync(
            request.CompoundId,
            request.BillingCycleId,
            cancellationToken);
        if (cycleResult.Failure is not null)
        {
            return ToResult<GenerateMonthlyUtilityBillsResponse>(cycleResult.Failure);
        }

        var services = await dbContext.CompoundServices
            .AsNoTracking()
            .Where(service => service.CompoundId == request.CompoundId
                && service.IsActive
                && !service.IsMeterBased)
            .OrderBy(service => service.Name)
            .ToArrayAsync(cancellationToken);

        if (services.Length == 0)
        {
            return ServiceResult<GenerateMonthlyUtilityBillsResponse>.BadRequest(
                "Compound has no active non-meter-based services for monthly bill generation.");
        }

        var eligibleStatuses = request.IncludeOnlyOccupiedUnits
            ? OccupiedBillingStatuses
            : OptionalAvailableBillingStatuses;

        var units = await dbContext.PropertyUnits
            .AsNoTracking()
            .Where(unit => unit.CompoundId == request.CompoundId
                && unit.IsActive
                && eligibleStatuses.Contains(unit.UnitStatus))
            .OrderBy(unit => unit.UnitNumber)
            .ToArrayAsync(cancellationToken);

        var existingBillUnitIds = await dbContext.UtilityBills
            .AsNoTracking()
            .Where(bill => bill.BillingCycleId == request.BillingCycleId)
            .Select(bill => bill.PropertyUnitId)
            .ToArrayAsync(cancellationToken);

        var activeResidentByUnitId = await dbContext.OccupancyRecords
            .AsNoTracking()
            .Where(record => record.CompoundId == request.CompoundId
                && record.OccupancyStatus == OccupancyStatus.Active)
            .Select(record => new { record.PropertyUnitId, record.ResidentProfileId })
            .ToDictionaryAsync(record => record.PropertyUnitId, record => record.ResidentProfileId, cancellationToken);

        var existingBillUnitIdSet = existingBillUnitIds.ToHashSet();
        var skippedReasons = new List<string>();
        var createdCount = 0;
        var cycle = cycleResult.Cycle!;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var unit in units)
        {
            if (existingBillUnitIdSet.Contains(unit.Id))
            {
                skippedReasons.Add($"Unit {unit.UnitNumber} already has a utility bill for this billing cycle.");
                continue;
            }

            var lines = services
                .Select(service => new UtilityBillLine
                {
                    CompoundServiceId = service.Id,
                    Description = service.Name,
                    Quantity = 1m,
                    UnitPrice = service.DefaultMonthlyFee,
                    LineTotal = service.DefaultMonthlyFee
                })
                .ToList();

            var subtotalAmount = lines.Sum(line => line.LineTotal);
            var previousBalance = request.IncludePreviousBalance
                ? await CalculatePreviousBalanceAsync(unit.Id, cycle.Year, cycle.Month, cancellationToken)
                : 0m;
            var totalAmount = subtotalAmount + previousBalance;

            var utilityBill = new UtilityBill
            {
                CompoundId = request.CompoundId,
                PropertyUnitId = unit.Id,
                ResidentProfileId = activeResidentByUnitId.TryGetValue(unit.Id, out var residentProfileId)
                    ? residentProfileId
                    : null,
                BillingCycleId = cycle.Id,
                BillNumber = GenerateBillNumber(cycle.Year, cycle.Month, unit.UnitNumber),
                BillStatus = DetermineBillStatus(0m, totalAmount, cycle.DueDate, isCancelled: false),
                IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
                DueDate = cycle.DueDate,
                SubtotalAmount = subtotalAmount,
                PreviousBalanceAmount = previousBalance,
                LateFeeAmount = 0m,
                DiscountAmount = 0m,
                TotalAmount = totalAmount,
                PaidAmount = 0m,
                Notes = TrimOrNull(request.Notes),
                Lines = lines
            };

            dbContext.UtilityBills.Add(utilityBill);
            createdCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<GenerateMonthlyUtilityBillsResponse>.Success(
            new GenerateMonthlyUtilityBillsResponse(
                createdCount,
                skippedReasons.Count,
                skippedReasons));
    }

    public async Task<ServiceResult<UtilityBillResponse>> UpdateUtilityBillAsync(
        Guid id,
        UpdateUtilityBillRequest request,
        CancellationToken cancellationToken = default)
    {
        var utilityBills = await ApplyCurrentUtilityBillScopeAsync(
            GetUtilityBillDetailsQuery(asNoTracking: false),
            cancellationToken);

        var utilityBill = await utilityBills
            .FirstOrDefaultAsync(bill => bill.Id == id, cancellationToken);

        if (utilityBill is null)
        {
            return ServiceResult<UtilityBillResponse>.NotFound("Utility bill was not found.");
        }

        if (utilityBill.BillStatus == BillStatus.Cancelled)
        {
            return ServiceResult<UtilityBillResponse>.BadRequest("Cancelled utility bill cannot be updated.");
        }

        if (request.DueDate == default)
        {
            return ServiceResult<UtilityBillResponse>.BadRequest("Due date is required.");
        }

        var amountFailure = ValidateNonNegativeAmounts(
            utilityBill.PreviousBalanceAmount,
            request.LateFeeAmount,
            request.DiscountAmount,
            0m);
        if (amountFailure is not null)
        {
            return ToResult<UtilityBillResponse>(amountFailure);
        }

        var totalAmount = utilityBill.SubtotalAmount
            + utilityBill.PreviousBalanceAmount
            + request.LateFeeAmount
            - request.DiscountAmount;

        if (totalAmount < 0)
        {
            return ServiceResult<UtilityBillResponse>.BadRequest("Total amount cannot be negative.");
        }

        if (utilityBill.PaidAmount > totalAmount)
        {
            return ServiceResult<UtilityBillResponse>.BadRequest(
                "Total amount cannot be less than the already paid amount.");
        }

        utilityBill.DueDate = request.DueDate;
        utilityBill.LateFeeAmount = request.LateFeeAmount;
        utilityBill.DiscountAmount = request.DiscountAmount;
        utilityBill.TotalAmount = totalAmount;
        utilityBill.BillStatus = DetermineBillStatus(
            utilityBill.PaidAmount,
            utilityBill.TotalAmount,
            utilityBill.DueDate,
            isCancelled: false);
        utilityBill.Notes = TrimOrNull(request.Notes);
        utilityBill.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<UtilityBillResponse>.Success(ToUtilityBillResponse(utilityBill));
    }

    public async Task<ServiceResult<UtilityBillResponse>> CancelUtilityBillAsync(
        Guid id,
        CancelUtilityBillRequest request,
        CancellationToken cancellationToken = default)
    {
        var utilityBills = await ApplyCurrentUtilityBillScopeAsync(
            GetUtilityBillDetailsQuery(asNoTracking: false),
            cancellationToken);

        var utilityBill = await utilityBills
            .FirstOrDefaultAsync(bill => bill.Id == id, cancellationToken);

        if (utilityBill is null)
        {
            return ServiceResult<UtilityBillResponse>.NotFound("Utility bill was not found.");
        }

        if (utilityBill.PaidAmount > 0)
        {
            return ServiceResult<UtilityBillResponse>.BadRequest(
                "Utility bill cannot be cancelled after a paid amount is recorded.");
        }

        utilityBill.BillStatus = BillStatus.Cancelled;
        utilityBill.CancelledAt ??= DateTime.UtcNow;
        utilityBill.CancellationReason = TrimOrNull(request.CancellationReason);
        utilityBill.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<UtilityBillResponse>.Success(ToUtilityBillResponse(utilityBill));
    }

    public async Task<ServiceResult<UtilityBillResponse>> RecalculateUtilityBillStatusAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var utilityBills = await ApplyCurrentUtilityBillScopeAsync(
            GetUtilityBillDetailsQuery(asNoTracking: false),
            cancellationToken);

        var utilityBill = await utilityBills
            .FirstOrDefaultAsync(bill => bill.Id == id, cancellationToken);

        if (utilityBill is null)
        {
            return ServiceResult<UtilityBillResponse>.NotFound("Utility bill was not found.");
        }

        utilityBill.BillStatus = DetermineBillStatus(
            utilityBill.PaidAmount,
            utilityBill.TotalAmount,
            utilityBill.DueDate,
            utilityBill.BillStatus == BillStatus.Cancelled);
        utilityBill.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<UtilityBillResponse>.Success(ToUtilityBillResponse(utilityBill));
    }

    public async Task<PagedResult<UtilityBillResponse>> SearchResidentBillsAsync(
        Guid userId,
        UtilityBillSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetResidentBillingScopeAsync(userId, cancellationToken);
        if (scope.ProfileIds.Length == 0)
        {
            return new PagedResult<UtilityBillResponse>(
                [],
                query.PageNumber,
                query.PageSize,
                0);
        }

        var profileIds = scope.ProfileIds;

        var utilityBills = GetUtilityBillDetailsQuery(asNoTracking: true)
            .Where(bill => bill.ResidentProfileId.HasValue
                && profileIds.Contains(bill.ResidentProfileId.Value));

        utilityBills = ApplyUtilityBillFilters(utilityBills, query);

        return await ToPagedUtilityBillResultAsync(utilityBills, query, cancellationToken);
    }

    public async Task<ServiceResult<UtilityBillResponse>> GetResidentBillAsync(
        Guid userId,
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetResidentBillingScopeAsync(userId, cancellationToken);
        if (scope.ProfileIds.Length == 0)
        {
            return ServiceResult<UtilityBillResponse>.NotFound("Utility bill was not found.");
        }

        var profileIds = scope.ProfileIds;

        var utilityBill = await GetUtilityBillDetailsQuery(asNoTracking: true)
            .Where(bill => bill.Id == billId)
            .Where(bill => bill.ResidentProfileId.HasValue
                && profileIds.Contains(bill.ResidentProfileId.Value))
            .FirstOrDefaultAsync(cancellationToken);

        return utilityBill is null
            ? ServiceResult<UtilityBillResponse>.NotFound("Utility bill was not found.")
            : ServiceResult<UtilityBillResponse>.Success(ToUtilityBillResponse(utilityBill));
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

        return asNoTracking ? query.AsNoTracking().AsSplitQuery() : query;
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
            .Select(bill => new { bill.TotalAmount, bill.PaidAmount, bill.PreviousBalanceAmount })
            .ToArrayAsync(cancellationToken);

        var openBalance = previousBalances.Sum(bill => Math.Max(0m, bill.TotalAmount - bill.PaidAmount));
        var alreadyCarried = previousBalances.Sum(bill => bill.PreviousBalanceAmount);
        return Math.Max(0m, openBalance - alreadyCarried);
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
