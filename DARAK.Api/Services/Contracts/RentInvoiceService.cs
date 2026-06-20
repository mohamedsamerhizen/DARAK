using System.Data;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.PropertySales;
using DARAK.Api.DTOs.Rents;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class RentInvoiceService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IRentInvoiceService
{
    public async Task<ServiceResult<RentInvoiceResponse>> GenerateRentInvoiceAsync(
        GenerateRentInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var validation = await ValidateRentInvoiceRequestAsync(
            request.RentContractId,
            request.Year,
            request.Month,
            request.IssueDate,
            request.DueDate,
            request.PreviousBalanceAmount,
            request.LateFeeAmount,
            request.DiscountAmount,
            cancellationToken);
        if (validation.Failure is not null)
        {
            return ToResult<RentInvoiceResponse>(validation.Failure);
        }

        var contract = validation.Contract!;
        var total = contract.MonthlyRentAmount
            + request.PreviousBalanceAmount
            + request.LateFeeAmount
            - request.DiscountAmount;
        if (total < 0)
        {
            return ServiceResult<RentInvoiceResponse>.BadRequest("Rent invoice total cannot be negative.");
        }

        var invoice = new RentInvoice
        {
            RentContractId = contract.Id,
            CompoundId = contract.CompoundId,
            PropertyUnitId = contract.PropertyUnitId,
            ResidentProfileId = contract.ResidentProfileId,
            InvoiceNumber = GenerateReference("RINV"),
            Year = request.Year,
            Month = request.Month,
            IssueDate = request.IssueDate,
            DueDate = request.DueDate,
            RentAmount = contract.MonthlyRentAmount,
            PreviousBalanceAmount = request.PreviousBalanceAmount,
            LateFeeAmount = request.LateFeeAmount,
            DiscountAmount = request.DiscountAmount,
            TotalAmount = total,
            RentInvoiceStatus = DetermineRentInvoiceStatus(0, total, request.DueDate),
            Notes = TrimOrNull(request.Notes)
        };

        dbContext.RentInvoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<RentInvoiceResponse>.Success(
            ToRentInvoiceResponse(await LoadRentInvoiceAsync(invoice.Id, cancellationToken) ?? invoice));
    }

    public async Task<ServiceResult<GenerateMonthlyRentInvoicesResponse>> GenerateMonthlyRentInvoicesAsync(
        GenerateMonthlyRentInvoicesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Month is < 1 or > 12)
        {
            return ServiceResult<GenerateMonthlyRentInvoicesResponse>.BadRequest("Month must be between 1 and 12.");
        }

        if (request.DueDate < request.IssueDate)
        {
            return ServiceResult<GenerateMonthlyRentInvoicesResponse>.BadRequest(
                "Due date cannot be before issue date.");
        }

        var compoundExists = await dbContext.Compounds.AnyAsync(
            compound => compound.Id == request.CompoundId && compound.IsActive,
            cancellationToken);
        if (!compoundExists)
        {
            return ServiceResult<GenerateMonthlyRentInvoicesResponse>.NotFound("Active compound was not found.");
        }

        if (!await CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<GenerateMonthlyRentInvoicesResponse>.Forbidden(
                "Current user cannot access this compound.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var contractsQuery = await ApplyCurrentRentContractScopeAsync(
            GetRentContractsQuery(asNoTracking: false),
            cancellationToken);

        var contracts = await contractsQuery
            .Where(contract => contract.CompoundId == request.CompoundId
                && contract.ContractStatus == RentContractStatus.Active)
            .ToArrayAsync(cancellationToken);

        var createdCount = 0;
        var skippedReasons = new List<string>();
        var periodDate = new DateOnly(request.Year, request.Month, 1);

        foreach (var contract in contracts)
        {
            if (periodDate < new DateOnly(contract.StartDate.Year, contract.StartDate.Month, 1)
                || periodDate > new DateOnly(contract.EndDate.Year, contract.EndDate.Month, 1))
            {
                skippedReasons.Add($"{contract.ContractNumber}: outside contract period.");
                continue;
            }

            var exists = await dbContext.RentInvoices.AnyAsync(item =>
                item.RentContractId == contract.Id
                && item.Year == request.Year
                && item.Month == request.Month,
                cancellationToken);
            if (exists)
            {
                skippedReasons.Add($"{contract.ContractNumber}: invoice already exists.");
                continue;
            }

            var previousBalance = request.IncludePreviousBalance
                ? await CalculatePreviousRentBalanceAsync(contract.Id, request.Year, request.Month, cancellationToken)
                : 0m;
            var total = contract.MonthlyRentAmount + previousBalance;

            dbContext.RentInvoices.Add(new RentInvoice
            {
                RentContractId = contract.Id,
                CompoundId = contract.CompoundId,
                PropertyUnitId = contract.PropertyUnitId,
                ResidentProfileId = contract.ResidentProfileId,
                InvoiceNumber = GenerateReference("RINV"),
                Year = request.Year,
                Month = request.Month,
                IssueDate = request.IssueDate,
                DueDate = request.DueDate,
                RentAmount = contract.MonthlyRentAmount,
                PreviousBalanceAmount = previousBalance,
                TotalAmount = total,
                RentInvoiceStatus = DetermineRentInvoiceStatus(0, total, request.DueDate),
                Notes = TrimOrNull(request.Notes)
            });
            createdCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<GenerateMonthlyRentInvoicesResponse>.Success(
            new GenerateMonthlyRentInvoicesResponse(createdCount, skippedReasons.Count, skippedReasons));
    }

    public async Task<PagedResult<RentInvoiceResponse>> SearchRentInvoicesAsync(
        RentInvoiceSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var invoices = await ApplyCurrentRentInvoiceScopeAsync(
            ApplyRentInvoiceFilters(GetRentInvoicesQuery(asNoTracking: true), query),
            cancellationToken);

        return await ToPagedRentInvoiceResultAsync(
            invoices,
            query,
            cancellationToken);
    }

    public async Task<ServiceResult<RentInvoiceResponse>> GetRentInvoiceAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var invoices = await ApplyCurrentRentInvoiceScopeAsync(
            GetRentInvoicesQuery(asNoTracking: true),
            cancellationToken);

        var invoice = await invoices
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return invoice is null
            ? ServiceResult<RentInvoiceResponse>.NotFound("Rent invoice was not found.")
            : ServiceResult<RentInvoiceResponse>.Success(ToRentInvoiceResponse(invoice));
    }

    public async Task<ServiceResult<RentInvoiceResponse>> CancelRentInvoiceAsync(
        Guid id,
        CancelRentInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = TrimOrNull(request.Reason);
        if (reason is null)
        {
            return ServiceResult<RentInvoiceResponse>.BadRequest("Cancellation reason is required.");
        }

        var invoices = await ApplyCurrentRentInvoiceScopeAsync(
            GetRentInvoicesQuery(asNoTracking: false),
            cancellationToken);

        var invoice = await invoices
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return ServiceResult<RentInvoiceResponse>.NotFound("Rent invoice was not found.");
        }

        if (invoice.RentInvoiceStatus == RentInvoiceStatus.Cancelled)
        {
            return ServiceResult<RentInvoiceResponse>.Conflict("Rent invoice is already cancelled.");
        }

        if (invoice.PaidAmount > 0)
        {
            return ServiceResult<RentInvoiceResponse>.BadRequest("Cannot cancel a rent invoice with payments.");
        }

        invoice.RentInvoiceStatus = RentInvoiceStatus.Cancelled;
        invoice.CancelledAt = DateTime.UtcNow;
        invoice.CancellationReason = reason;
        invoice.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<RentInvoiceResponse>.Success(ToRentInvoiceResponse(invoice));
    }

    public async Task<ServiceResult<RentInvoiceResponse>> RecalculateRentInvoiceStatusAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var invoices = await ApplyCurrentRentInvoiceScopeAsync(
            GetRentInvoicesQuery(asNoTracking: false),
            cancellationToken);

        var invoice = await invoices
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return ServiceResult<RentInvoiceResponse>.NotFound("Rent invoice was not found.");
        }

        if (invoice.RentInvoiceStatus == RentInvoiceStatus.Cancelled)
        {
            return ServiceResult<RentInvoiceResponse>.BadRequest("Cancelled rent invoice cannot be recalculated.");
        }

        invoice.RentInvoiceStatus = DetermineRentInvoiceStatus(
            invoice.PaidAmount,
            invoice.TotalAmount,
            invoice.DueDate);
        invoice.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<RentInvoiceResponse>.Success(ToRentInvoiceResponse(invoice));
    }

    public async Task<PagedResult<RentInvoiceResponse>> SearchResidentRentInvoicesAsync(
        Guid userId,
        RentInvoiceSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var profileIds = await GetResidentProfileIdsAsync(userId, cancellationToken);
        if (profileIds.Length == 0)
        {
            return new PagedResult<RentInvoiceResponse>([], query.PageNumber, query.PageSize, 0);
        }

        return await ToPagedRentInvoiceResultAsync(
            ApplyRentInvoiceFilters(GetRentInvoicesQuery(asNoTracking: true), query)
                .Where(invoice => profileIds.Contains(invoice.ResidentProfileId)),
            query,
            cancellationToken);
    }

    public async Task<ServiceResult<RentInvoiceResponse>> GetResidentRentInvoiceAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var profileIds = await GetResidentProfileIdsAsync(userId, cancellationToken);
        var invoice = await GetRentInvoicesQuery(asNoTracking: true)
            .FirstOrDefaultAsync(item => item.Id == id && profileIds.Contains(item.ResidentProfileId), cancellationToken);

        return invoice is null
            ? ServiceResult<RentInvoiceResponse>.NotFound("Rent invoice was not found.")
            : ServiceResult<RentInvoiceResponse>.Success(ToRentInvoiceResponse(invoice));
    }

    private IQueryable<PropertySaleContract> GetSaleContractsQuery(bool asNoTracking)
    {
        var query = dbContext.PropertySaleContracts
            .Include(contract => contract.Compound)
            .Include(contract => contract.PropertyUnit)
            .Include(contract => contract.ResidentProfile)
            .Include(contract => contract.Installments)
                .ThenInclude(installment => installment.Compound)
            .Include(contract => contract.Installments)
                .ThenInclude(installment => installment.PropertyUnit)
            .Include(contract => contract.Installments)
                .ThenInclude(installment => installment.ResidentProfile)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private IQueryable<InstallmentScheduleItem> GetInstallmentsQuery(bool asNoTracking)
    {
        var query = dbContext.InstallmentScheduleItems
            .Include(installment => installment.PropertySaleContract)
            .Include(installment => installment.Compound)
            .Include(installment => installment.PropertyUnit)
            .Include(installment => installment.ResidentProfile)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private IQueryable<RentContract> GetRentContractsQuery(bool asNoTracking)
    {
        var query = dbContext.RentContracts
            .Include(contract => contract.Compound)
            .Include(contract => contract.PropertyUnit)
            .Include(contract => contract.ResidentProfile)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private IQueryable<RentInvoice> GetRentInvoicesQuery(bool asNoTracking)
    {
        var query = dbContext.RentInvoices
            .Include(invoice => invoice.RentContract)
            .Include(invoice => invoice.Compound)
            .Include(invoice => invoice.PropertyUnit)
            .Include(invoice => invoice.ResidentProfile)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private async Task<PropertySaleContract?> LoadSaleContractAsync(Guid id, CancellationToken cancellationToken)
    {
        return await GetSaleContractsQuery(asNoTracking: true)
            .FirstOrDefaultAsync(contract => contract.Id == id, cancellationToken);
    }

    private async Task<RentContract?> LoadRentContractAsync(Guid id, CancellationToken cancellationToken)
    {
        return await GetRentContractsQuery(asNoTracking: true)
            .FirstOrDefaultAsync(contract => contract.Id == id, cancellationToken);
    }

    private async Task<RentInvoice?> LoadRentInvoiceAsync(Guid id, CancellationToken cancellationToken)
    {
        return await GetRentInvoicesQuery(asNoTracking: true)
            .FirstOrDefaultAsync(invoice => invoice.Id == id, cancellationToken);
    }

    private static IQueryable<PropertySaleContract> ApplySaleContractFilters(
        IQueryable<PropertySaleContract> query,
        PropertySaleContractSearchQuery filters)
    {
        if (filters.CompoundId.HasValue)
        {
            query = query.Where(item => item.CompoundId == filters.CompoundId.Value);
        }

        if (filters.PropertyUnitId.HasValue)
        {
            query = query.Where(item => item.PropertyUnitId == filters.PropertyUnitId.Value);
        }

        if (filters.ResidentProfileId.HasValue)
        {
            query = query.Where(item => item.ResidentProfileId == filters.ResidentProfileId.Value);
        }

        if (filters.SaleType.HasValue)
        {
            query = query.Where(item => item.SaleType == filters.SaleType.Value);
        }

        if (filters.ContractStatus.HasValue)
        {
            query = query.Where(item => item.ContractStatus == filters.ContractStatus.Value);
        }

        var searchTerm = TrimOrNull(filters.SearchTerm);
        if (searchTerm is not null)
        {
            query = query.Where(item =>
                item.ContractNumber.Contains(searchTerm)
                || item.PropertyUnit.UnitNumber.Contains(searchTerm)
                || item.ResidentProfile.FullName.Contains(searchTerm));
        }

        return query;
    }

    private static IQueryable<InstallmentScheduleItem> ApplyInstallmentFilters(
        IQueryable<InstallmentScheduleItem> query,
        InstallmentSearchQuery filters)
    {
        if (filters.CompoundId.HasValue)
        {
            query = query.Where(item => item.CompoundId == filters.CompoundId.Value);
        }

        if (filters.PropertySaleContractId.HasValue)
        {
            query = query.Where(item => item.PropertySaleContractId == filters.PropertySaleContractId.Value);
        }

        if (filters.PropertyUnitId.HasValue)
        {
            query = query.Where(item => item.PropertyUnitId == filters.PropertyUnitId.Value);
        }

        if (filters.ResidentProfileId.HasValue)
        {
            query = query.Where(item => item.ResidentProfileId == filters.ResidentProfileId.Value);
        }

        if (filters.InstallmentStatus.HasValue)
        {
            query = query.Where(item => item.InstallmentStatus == filters.InstallmentStatus.Value);
        }

        if (filters.DueFrom.HasValue)
        {
            query = query.Where(item => item.DueDate >= filters.DueFrom.Value);
        }

        if (filters.DueTo.HasValue)
        {
            query = query.Where(item => item.DueDate <= filters.DueTo.Value);
        }

        return query;
    }

    private static IQueryable<RentContract> ApplyRentContractFilters(
        IQueryable<RentContract> query,
        RentContractSearchQuery filters)
    {
        if (filters.CompoundId.HasValue)
        {
            query = query.Where(item => item.CompoundId == filters.CompoundId.Value);
        }

        if (filters.PropertyUnitId.HasValue)
        {
            query = query.Where(item => item.PropertyUnitId == filters.PropertyUnitId.Value);
        }

        if (filters.ResidentProfileId.HasValue)
        {
            query = query.Where(item => item.ResidentProfileId == filters.ResidentProfileId.Value);
        }

        if (filters.ContractStatus.HasValue)
        {
            query = query.Where(item => item.ContractStatus == filters.ContractStatus.Value);
        }

        var searchTerm = TrimOrNull(filters.SearchTerm);
        if (searchTerm is not null)
        {
            query = query.Where(item =>
                item.ContractNumber.Contains(searchTerm)
                || item.PropertyUnit.UnitNumber.Contains(searchTerm)
                || item.ResidentProfile.FullName.Contains(searchTerm));
        }

        return query;
    }

    private static IQueryable<RentInvoice> ApplyRentInvoiceFilters(
        IQueryable<RentInvoice> query,
        RentInvoiceSearchQuery filters)
    {
        if (filters.CompoundId.HasValue)
        {
            query = query.Where(item => item.CompoundId == filters.CompoundId.Value);
        }

        if (filters.RentContractId.HasValue)
        {
            query = query.Where(item => item.RentContractId == filters.RentContractId.Value);
        }

        if (filters.PropertyUnitId.HasValue)
        {
            query = query.Where(item => item.PropertyUnitId == filters.PropertyUnitId.Value);
        }

        if (filters.ResidentProfileId.HasValue)
        {
            query = query.Where(item => item.ResidentProfileId == filters.ResidentProfileId.Value);
        }

        if (filters.Year.HasValue)
        {
            query = query.Where(item => item.Year == filters.Year.Value);
        }

        if (filters.Month.HasValue)
        {
            query = query.Where(item => item.Month == filters.Month.Value);
        }

        if (filters.RentInvoiceStatus.HasValue)
        {
            query = query.Where(item => item.RentInvoiceStatus == filters.RentInvoiceStatus.Value);
        }

        if (filters.DueBefore.HasValue)
        {
            query = query.Where(item => item.DueDate <= filters.DueBefore.Value);
        }

        if (filters.DueAfter.HasValue)
        {
            query = query.Where(item => item.DueDate >= filters.DueAfter.Value);
        }

        var searchTerm = TrimOrNull(filters.SearchTerm);
        if (searchTerm is not null)
        {
            query = query.Where(item =>
                item.InvoiceNumber.Contains(searchTerm)
                || item.PropertyUnit.UnitNumber.Contains(searchTerm)
                || item.ResidentProfile.FullName.Contains(searchTerm));
        }

        return query;
    }

    private async Task<PagedResult<PropertySaleContractResponse>> ToPagedSaleContractResultAsync(
        IQueryable<PropertySaleContract> query,
        PaginationQuery pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var contracts = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<PropertySaleContractResponse>(
            contracts.Select(ToSaleContractResponse).ToArray(),
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private async Task<PagedResult<InstallmentScheduleItemResponse>> ToPagedInstallmentResultAsync(
        IQueryable<InstallmentScheduleItem> query,
        PaginationQuery pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var installments = await query
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.InstallmentNumber)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<InstallmentScheduleItemResponse>(
            installments.Select(ToInstallmentResponse).ToArray(),
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private async Task<PagedResult<RentContractResponse>> ToPagedRentContractResultAsync(
        IQueryable<RentContract> query,
        PaginationQuery pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var contracts = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<RentContractResponse>(
            contracts.Select(ToRentContractResponse).ToArray(),
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private async Task<PagedResult<RentInvoiceResponse>> ToPagedRentInvoiceResultAsync(
        IQueryable<RentInvoice> query,
        PaginationQuery pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var invoices = await query
            .OrderByDescending(item => item.Year)
            .ThenByDescending(item => item.Month)
            .ThenBy(item => item.InvoiceNumber)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<RentInvoiceResponse>(
            invoices.Select(ToRentInvoiceResponse).ToArray(),
            pagination.PageNumber,
            pagination.PageSize,
            totalCount);
    }

    private async Task<(PropertyUnit? PropertyUnit, ValidationFailure? Failure)> ValidateContractFoundationAsync(
        Guid compoundId,
        Guid propertyUnitId,
        Guid residentProfileId,
        CancellationToken cancellationToken)
    {
        var compoundExists = await dbContext.Compounds.AnyAsync(
            compound => compound.Id == compoundId && compound.IsActive,
            cancellationToken);
        if (!compoundExists)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Active compound was not found."));
        }

        if (!await CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken))
        {
            return (null, new ValidationFailure(
                ServiceResultStatus.Forbidden,
                "Current user cannot access this compound."));
        }

        var propertyUnit = await dbContext.PropertyUnits
            .FirstOrDefaultAsync(unit => unit.Id == propertyUnitId && unit.IsActive, cancellationToken);
        if (propertyUnit is null)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Active property unit was not found."));
        }

        if (propertyUnit.CompoundId != compoundId)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Property unit does not belong to compound."));
        }

        if (propertyUnit.UnitStatus != UnitStatus.Available)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Property unit must be Available."));
        }

        var residentExists = await dbContext.ResidentProfiles.AnyAsync(profile =>
            profile.Id == residentProfileId
            && profile.CompoundId == compoundId
            && profile.IsActive,
            cancellationToken);
        if (!residentExists)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Active resident profile was not found."));
        }

        return (propertyUnit, null);
    }

    private async Task<(PropertyUnit? PropertyUnit, ValidationFailure? Failure)> ValidateSaleContractFoundationAsync(
        Guid compoundId,
        Guid propertyUnitId,
        Guid residentProfileId,
        string contractNumber,
        CancellationToken cancellationToken)
    {
        var foundation = await ValidateContractFoundationAsync(
            compoundId,
            propertyUnitId,
            residentProfileId,
            cancellationToken);
        if (foundation.Failure is not null)
        {
            return foundation;
        }

        if (TrimOrNull(contractNumber) is null)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Contract number is required."));
        }

        var duplicateContractNumber = await dbContext.PropertySaleContracts
            .AnyAsync(item => item.ContractNumber == contractNumber.Trim(), cancellationToken);
        if (duplicateContractNumber)
        {
            return (null, new ValidationFailure(ServiceResultStatus.Conflict, "Sale contract number already exists."));
        }

        var activeSaleContractExists = await dbContext.PropertySaleContracts.AnyAsync(item =>
            item.PropertyUnitId == propertyUnitId
            && item.ContractStatus == SaleContractStatus.Active,
            cancellationToken);
        if (activeSaleContractExists)
        {
            return (null, new ValidationFailure(ServiceResultStatus.Conflict, "Unit already has an active sale contract."));
        }

        return foundation;
    }

    private async Task<(RentContract? Contract, ValidationFailure? Failure)> ValidateRentInvoiceRequestAsync(
        Guid rentContractId,
        int year,
        int month,
        DateOnly issueDate,
        DateOnly dueDate,
        decimal previousBalance,
        decimal lateFee,
        decimal discount,
        CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Month must be between 1 and 12."));
        }

        if (dueDate < issueDate)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Due date cannot be before issue date."));
        }

        if (previousBalance < 0 || lateFee < 0 || discount < 0)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Amounts cannot be negative."));
        }

        var contract = await GetRentContractsQuery(asNoTracking: true)
            .FirstOrDefaultAsync(item => item.Id == rentContractId, cancellationToken);
        if (contract is null)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Rent contract was not found."));
        }

        if (!await CanCurrentUserAccessCompoundAsync(contract.CompoundId, cancellationToken))
        {
            return (null, new ValidationFailure(
                ServiceResultStatus.Forbidden,
                "Current user cannot access this compound."));
        }

        if (contract.ContractStatus != RentContractStatus.Active)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Rent contract must be active."));
        }

        var periodDate = new DateOnly(year, month, 1);
        if (periodDate < new DateOnly(contract.StartDate.Year, contract.StartDate.Month, 1)
            || periodDate > new DateOnly(contract.EndDate.Year, contract.EndDate.Month, 1))
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Rent invoice month is outside contract period."));
        }

        var duplicate = await dbContext.RentInvoices.AnyAsync(item =>
            item.RentContractId == rentContractId
            && item.Year == year
            && item.Month == month,
            cancellationToken);
        if (duplicate)
        {
            return (null, new ValidationFailure(ServiceResultStatus.Conflict, "Rent invoice already exists for this contract and month."));
        }

        return (contract, null);
    }

    private async Task<decimal> CalculatePreviousRentBalanceAsync(
        Guid rentContractId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var previousBalances = await dbContext.RentInvoices
            .AsNoTracking()
            .Where(invoice => invoice.RentContractId == rentContractId
                && invoice.RentInvoiceStatus != RentInvoiceStatus.Cancelled
                && invoice.RentInvoiceStatus != RentInvoiceStatus.Paid
                && (invoice.Year < year || (invoice.Year == year && invoice.Month < month)))
            .Select(invoice => new { invoice.TotalAmount, invoice.PaidAmount })
            .ToArrayAsync(cancellationToken);

        return previousBalances.Sum(invoice => Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount));
    }

    private async Task<Guid[]> GetResidentProfileIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId && profile.IsActive)
            .Select(profile => profile.Id)
            .ToArrayAsync(cancellationToken);
    }

    private static PropertySaleContractResponse ToSaleContractResponse(PropertySaleContract contract)
    {
        var installments = contract.Installments
            .OrderBy(item => item.InstallmentNumber)
            .Select(ToInstallmentResponse)
            .ToArray();

        return new PropertySaleContractResponse(
            contract.Id,
            contract.CompoundId,
            contract.Compound.Name,
            contract.PropertyUnitId,
            contract.PropertyUnit.UnitNumber,
            contract.ResidentProfileId,
            contract.ResidentProfile.FullName,
            contract.SaleType,
            contract.ContractStatus,
            contract.ContractNumber,
            contract.ContractDate,
            contract.PropertyPrice,
            contract.DownPaymentAmount,
            contract.InstallmentCount,
            installments.Sum(item => item.Amount),
            installments.Sum(item => item.PaidAmount),
            installments.Sum(item => item.RemainingAmount),
            contract.FirstInstallmentDueDate,
            contract.Notes,
            contract.CreatedAt,
            contract.UpdatedAt,
            contract.CancelledAt,
            contract.CancellationReason,
            installments);
    }

    private static InstallmentScheduleItemResponse ToInstallmentResponse(InstallmentScheduleItem installment)
    {
        return new InstallmentScheduleItemResponse(
            installment.Id,
            installment.PropertySaleContractId,
            installment.CompoundId,
            installment.Compound.Name,
            installment.PropertyUnitId,
            installment.PropertyUnit.UnitNumber,
            installment.ResidentProfileId,
            installment.ResidentProfile.FullName,
            installment.InstallmentNumber,
            installment.DueDate,
            installment.Amount,
            installment.PaidAmount,
            Math.Max(0m, installment.Amount - installment.PaidAmount),
            installment.InstallmentStatus,
            installment.CreatedAt,
            installment.UpdatedAt,
            installment.PaidAt,
            installment.CancelledAt,
            installment.CancellationReason);
    }

    private static RentContractResponse ToRentContractResponse(RentContract contract)
    {
        return new RentContractResponse(
            contract.Id,
            contract.CompoundId,
            contract.Compound.Name,
            contract.PropertyUnitId,
            contract.PropertyUnit.UnitNumber,
            contract.ResidentProfileId,
            contract.ResidentProfile.FullName,
            contract.ContractNumber,
            contract.ContractStatus,
            contract.StartDate,
            contract.EndDate,
            contract.MonthlyRentAmount,
            contract.DepositAmount,
            contract.Notes,
            contract.CreatedAt,
            contract.UpdatedAt,
            contract.TerminatedAt,
            contract.TerminationReason,
            contract.CancelledAt,
            contract.CancellationReason);
    }

    private static RentInvoiceResponse ToRentInvoiceResponse(RentInvoice invoice)
    {
        return new RentInvoiceResponse(
            invoice.Id,
            invoice.RentContractId,
            invoice.CompoundId,
            invoice.Compound.Name,
            invoice.PropertyUnitId,
            invoice.PropertyUnit.UnitNumber,
            invoice.ResidentProfileId,
            invoice.ResidentProfile.FullName,
            invoice.InvoiceNumber,
            invoice.Year,
            invoice.Month,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.RentAmount,
            invoice.PreviousBalanceAmount,
            invoice.LateFeeAmount,
            invoice.DiscountAmount,
            invoice.TotalAmount,
            invoice.PaidAmount,
            Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount),
            invoice.RentInvoiceStatus,
            invoice.Notes,
            invoice.CreatedAt,
            invoice.UpdatedAt,
            invoice.CancelledAt,
            invoice.CancellationReason);
    }

    private static InstallmentStatus DetermineInstallmentStatus(decimal paidAmount, decimal amount, DateOnly dueDate)
    {
        if (paidAmount >= amount)
        {
            return InstallmentStatus.Paid;
        }

        if (paidAmount > 0)
        {
            return InstallmentStatus.PartiallyPaid;
        }

        return dueDate < DateOnly.FromDateTime(DateTime.UtcNow)
            ? InstallmentStatus.Overdue
            : InstallmentStatus.Pending;
    }

    private static RentInvoiceStatus DetermineRentInvoiceStatus(decimal paidAmount, decimal totalAmount, DateOnly dueDate)
    {
        if (paidAmount >= totalAmount)
        {
            return RentInvoiceStatus.Paid;
        }

        if (paidAmount > 0)
        {
            return RentInvoiceStatus.PartiallyPaid;
        }

        return dueDate < DateOnly.FromDateTime(DateTime.UtcNow)
            ? RentInvoiceStatus.Overdue
            : RentInvoiceStatus.Unpaid;
    }

    private static string GenerateReference(string prefix)
    {
        return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..16].ToUpperInvariant()}";
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

    private async Task<IQueryable<PropertySaleContract>> ApplyCurrentSaleContractScopeAsync(
        IQueryable<PropertySaleContract> contracts,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return contracts;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return contracts.ApplyCompoundAccess(scope, contract => contract.CompoundId);
    }

    private async Task<IQueryable<InstallmentScheduleItem>> ApplyCurrentInstallmentScopeAsync(
        IQueryable<InstallmentScheduleItem> installments,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return installments;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return installments.ApplyCompoundAccess(scope, installment => installment.CompoundId);
    }

    private async Task<IQueryable<RentContract>> ApplyCurrentRentContractScopeAsync(
        IQueryable<RentContract> contracts,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return contracts;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return contracts.ApplyCompoundAccess(scope, contract => contract.CompoundId);
    }

    private async Task<IQueryable<RentInvoice>> ApplyCurrentRentInvoiceScopeAsync(
        IQueryable<RentInvoice> invoices,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return invoices;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return invoices.ApplyCompoundAccess(scope, invoice => invoice.CompoundId);
    }

    private async Task<bool> CanCurrentUserAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);
}
