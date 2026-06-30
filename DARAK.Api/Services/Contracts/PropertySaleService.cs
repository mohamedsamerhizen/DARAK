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

public sealed class PropertySaleService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IPropertySaleService
{
    public async Task<PagedResult<PropertySaleContractResponse>> SearchSaleContractsAsync(
        PropertySaleContractSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var contracts = await ApplyCurrentSaleContractScopeAsync(
            ApplySaleContractFilters(GetSaleContractsQuery(asNoTracking: true), query),
            cancellationToken);

        return await ToPagedSaleContractResultAsync(
            contracts,
            query,
            cancellationToken);
    }

    public async Task<ServiceResult<PropertySaleContractResponse>> GetSaleContractAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var contracts = await ApplyCurrentSaleContractScopeAsync(
            GetSaleContractsQuery(asNoTracking: true),
            cancellationToken);

        var contract = await contracts
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return contract is null
            ? ServiceResult<PropertySaleContractResponse>.NotFound("Property sale contract was not found.")
            : ServiceResult<PropertySaleContractResponse>.Success(ToSaleContractResponse(contract));
    }

    public async Task<ServiceResult<PropertySaleContractResponse>> CreateCashSaleContractAsync(
        CreateCashSaleContractRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PropertyPrice <= 0)
        {
            return ServiceResult<PropertySaleContractResponse>.BadRequest("Property price must be greater than zero.");
        }

        var validation = await ValidateSaleContractFoundationAsync(
            request.CompoundId,
            request.PropertyUnitId,
            request.ResidentProfileId,
            request.ContractNumber,
            cancellationToken);
        if (validation.Failure is not null)
        {
            return ToResult<PropertySaleContractResponse>(validation.Failure);
        }

        var idempotencyKey = TrimOrNull(request.IdempotencyKey);
        if (idempotencyKey is not null
            && await dbContext.Payments.AsNoTracking().AnyAsync(payment => payment.IdempotencyKey == idempotencyKey, cancellationToken))
        {
            return ServiceResult<PropertySaleContractResponse>.Conflict("Idempotency key is already used by another payment.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        validation.PropertyUnit!.UnitStatus = UnitStatus.SoldCash;
        validation.PropertyUnit.UpdatedAt = DateTime.UtcNow;

        var contract = new PropertySaleContract
        {
            CompoundId = request.CompoundId,
            PropertyUnitId = request.PropertyUnitId,
            ResidentProfileId = request.ResidentProfileId,
            SaleType = SaleType.Cash,
            ContractStatus = SaleContractStatus.Completed,
            ContractNumber = request.ContractNumber.Trim(),
            ContractDate = request.ContractDate,
            PropertyPrice = request.PropertyPrice,
            DownPaymentAmount = request.PropertyPrice,
            InstallmentCount = 0,
            Notes = TrimOrNull(request.Notes)
        };

        dbContext.PropertySaleContracts.Add(contract);
        await AddSalePaymentRecordsAsync(
            contract,
            request.PropertyPrice,
            idempotencyKey,
            "Cash sale payment received",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PropertySaleContractResponse>.Success(
            ToSaleContractResponse(await LoadSaleContractAsync(contract.Id, cancellationToken) ?? contract));
    }

    public async Task<ServiceResult<PropertySaleContractResponse>> CreateInstallmentSaleContractAsync(
        CreateInstallmentSaleContractRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PropertyPrice <= 0)
        {
            return ServiceResult<PropertySaleContractResponse>.BadRequest("Property price must be greater than zero.");
        }

        if (request.DownPaymentAmount < 0 || request.DownPaymentAmount >= request.PropertyPrice)
        {
            return ServiceResult<PropertySaleContractResponse>.BadRequest(
                "Down payment must be non-negative and less than property price.");
        }

        if (request.InstallmentCount <= 0)
        {
            return ServiceResult<PropertySaleContractResponse>.BadRequest("Installment count must be greater than zero.");
        }

        if (request.FirstInstallmentDueDate < request.ContractDate)
        {
            return ServiceResult<PropertySaleContractResponse>.BadRequest(
                "First installment due date cannot be before contract date.");
        }

        var validation = await ValidateSaleContractFoundationAsync(
            request.CompoundId,
            request.PropertyUnitId,
            request.ResidentProfileId,
            request.ContractNumber,
            cancellationToken);
        if (validation.Failure is not null)
        {
            return ToResult<PropertySaleContractResponse>(validation.Failure);
        }

        var downPaymentIdempotencyKey = TrimOrNull(request.DownPaymentIdempotencyKey);
        if (downPaymentIdempotencyKey is not null
            && await dbContext.Payments.AsNoTracking().AnyAsync(payment => payment.IdempotencyKey == downPaymentIdempotencyKey, cancellationToken))
        {
            return ServiceResult<PropertySaleContractResponse>.Conflict("Down-payment idempotency key is already used by another payment.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        validation.PropertyUnit!.UnitStatus = UnitStatus.SoldInstallment;
        validation.PropertyUnit.UpdatedAt = DateTime.UtcNow;

        var remaining = request.PropertyPrice - request.DownPaymentAmount;
        var baseInstallmentAmount = Math.Round(remaining / request.InstallmentCount, 2, MidpointRounding.AwayFromZero);
        var installments = new List<InstallmentScheduleItem>();
        var assignedTotal = 0m;

        var contract = new PropertySaleContract
        {
            CompoundId = request.CompoundId,
            PropertyUnitId = request.PropertyUnitId,
            ResidentProfileId = request.ResidentProfileId,
            SaleType = SaleType.Installment,
            ContractStatus = SaleContractStatus.Active,
            ContractNumber = request.ContractNumber.Trim(),
            ContractDate = request.ContractDate,
            PropertyPrice = request.PropertyPrice,
            DownPaymentAmount = request.DownPaymentAmount,
            InstallmentCount = request.InstallmentCount,
            FirstInstallmentDueDate = request.FirstInstallmentDueDate,
            Notes = TrimOrNull(request.Notes)
        };

        for (var i = 1; i <= request.InstallmentCount; i++)
        {
            var amount = i == request.InstallmentCount
                ? remaining - assignedTotal
                : baseInstallmentAmount;
            assignedTotal += amount;

            installments.Add(new InstallmentScheduleItem
            {
                PropertySaleContract = contract,
                CompoundId = request.CompoundId,
                PropertyUnitId = request.PropertyUnitId,
                ResidentProfileId = request.ResidentProfileId,
                InstallmentNumber = i,
                DueDate = request.FirstInstallmentDueDate.AddMonths(i - 1),
                Amount = amount,
                InstallmentStatus = DetermineInstallmentStatus(0, amount, request.FirstInstallmentDueDate.AddMonths(i - 1))
            });
        }

        contract.Installments = installments;
        dbContext.PropertySaleContracts.Add(contract);
        if (request.DownPaymentAmount > 0)
        {
            await AddSalePaymentRecordsAsync(
                contract,
                request.DownPaymentAmount,
                downPaymentIdempotencyKey,
                "Property sale down payment received",
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PropertySaleContractResponse>.Success(
            ToSaleContractResponse(await LoadSaleContractAsync(contract.Id, cancellationToken) ?? contract));
    }

    public async Task<ServiceResult<PropertySaleContractResponse>> CancelSaleContractAsync(
        Guid id,
        CancelSaleContractRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = TrimOrNull(request.Reason);
        if (reason is null)
        {
            return ServiceResult<PropertySaleContractResponse>.BadRequest("Cancellation reason is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var contracts = await ApplyCurrentSaleContractScopeAsync(
            GetSaleContractsQuery(asNoTracking: false),
            cancellationToken);

        var contract = await contracts
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (contract is null)
        {
            return ServiceResult<PropertySaleContractResponse>.NotFound("Property sale contract was not found.");
        }

        if (contract.ContractStatus == SaleContractStatus.Cancelled)
        {
            return ServiceResult<PropertySaleContractResponse>.Conflict("Property sale contract is already cancelled.");
        }

        if (contract.Installments.Any(item => item.PaidAmount > 0))
        {
            return ServiceResult<PropertySaleContractResponse>.BadRequest(
                "Cannot cancel a sale contract after installment payments exist.");
        }

        if (contract.DownPaymentAmount > 0)
        {
            return ServiceResult<PropertySaleContractResponse>.BadRequest(
                "Cannot cancel a sale contract with a recorded down payment or cash sale amount without a payment/refund workflow.");
        }

        contract.ContractStatus = SaleContractStatus.Cancelled;
        contract.CancelledAt = DateTime.UtcNow;
        contract.CancellationReason = reason;
        contract.UpdatedAt = DateTime.UtcNow;

        foreach (var installment in contract.Installments.Where(item => item.InstallmentStatus != InstallmentStatus.Cancelled))
        {
            installment.InstallmentStatus = InstallmentStatus.Cancelled;
            installment.CancelledAt = DateTime.UtcNow;
            installment.CancellationReason = reason;
            installment.UpdatedAt = DateTime.UtcNow;
        }

        contract.PropertyUnit.UnitStatus = UnitStatus.Available;
        contract.PropertyUnit.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PropertySaleContractResponse>.Success(ToSaleContractResponse(contract));
    }

    public async Task<PagedResult<InstallmentScheduleItemResponse>> SearchInstallmentsAsync(
        InstallmentSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var installments = await ApplyCurrentInstallmentScopeAsync(
            ApplyInstallmentFilters(GetInstallmentsQuery(asNoTracking: true), query),
            cancellationToken);

        return await ToPagedInstallmentResultAsync(
            installments,
            query,
            cancellationToken);
    }

    public async Task<ServiceResult<InstallmentScheduleItemResponse>> GetInstallmentAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var installments = await ApplyCurrentInstallmentScopeAsync(
            GetInstallmentsQuery(asNoTracking: true),
            cancellationToken);

        var installment = await installments
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return installment is null
            ? ServiceResult<InstallmentScheduleItemResponse>.NotFound("Installment was not found.")
            : ServiceResult<InstallmentScheduleItemResponse>.Success(ToInstallmentResponse(installment));
    }

    public async Task<ServiceResult<InstallmentScheduleItemResponse>> RecalculateInstallmentStatusAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var installments = await ApplyCurrentInstallmentScopeAsync(
            GetInstallmentsQuery(asNoTracking: false),
            cancellationToken);

        var installment = await installments
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (installment is null)
        {
            return ServiceResult<InstallmentScheduleItemResponse>.NotFound("Installment was not found.");
        }

        if (installment.InstallmentStatus == InstallmentStatus.Cancelled)
        {
            return ServiceResult<InstallmentScheduleItemResponse>.BadRequest("Cancelled installment cannot be recalculated.");
        }

        installment.InstallmentStatus = DetermineInstallmentStatus(
            installment.PaidAmount,
            installment.Amount,
            installment.DueDate);
        installment.PaidAt = installment.InstallmentStatus == InstallmentStatus.Paid
            ? installment.PaidAt ?? DateTime.UtcNow
            : null;
        installment.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<InstallmentScheduleItemResponse>.Success(ToInstallmentResponse(installment));
    }

    public async Task<PagedResult<PropertySaleContractResponse>> SearchResidentSaleContractsAsync(
        Guid userId,
        PropertySaleContractSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var profileIds = await GetResidentProfileIdsAsync(userId, cancellationToken);
        if (profileIds.Length == 0)
        {
            return new PagedResult<PropertySaleContractResponse>([], query.PageNumber, query.PageSize, 0);
        }

        return await ToPagedSaleContractResultAsync(
            ApplySaleContractFilters(GetSaleContractsQuery(asNoTracking: true), query)
                .Where(contract => profileIds.Contains(contract.ResidentProfileId)),
            query,
            cancellationToken);
    }

    public async Task<ServiceResult<PropertySaleContractResponse>> GetResidentSaleContractAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var profileIds = await GetResidentProfileIdsAsync(userId, cancellationToken);
        var contract = await GetSaleContractsQuery(asNoTracking: true)
            .FirstOrDefaultAsync(item => item.Id == id && profileIds.Contains(item.ResidentProfileId), cancellationToken);

        return contract is null
            ? ServiceResult<PropertySaleContractResponse>.NotFound("Property sale contract was not found.")
            : ServiceResult<PropertySaleContractResponse>.Success(ToSaleContractResponse(contract));
    }

    public async Task<PagedResult<InstallmentScheduleItemResponse>> SearchResidentInstallmentsAsync(
        Guid userId,
        InstallmentSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var profileIds = await GetResidentProfileIdsAsync(userId, cancellationToken);
        if (profileIds.Length == 0)
        {
            return new PagedResult<InstallmentScheduleItemResponse>([], query.PageNumber, query.PageSize, 0);
        }

        return await ToPagedInstallmentResultAsync(
            ApplyInstallmentFilters(GetInstallmentsQuery(asNoTracking: true), query)
                .Where(installment => profileIds.Contains(installment.ResidentProfileId)),
            query,
            cancellationToken);
    }

    public async Task<ServiceResult<InstallmentScheduleItemResponse>> GetResidentInstallmentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var profileIds = await GetResidentProfileIdsAsync(userId, cancellationToken);
        var installment = await GetInstallmentsQuery(asNoTracking: true)
            .FirstOrDefaultAsync(item => item.Id == id && profileIds.Contains(item.ResidentProfileId), cancellationToken);

        return installment is null
            ? ServiceResult<InstallmentScheduleItemResponse>.NotFound("Installment was not found.")
            : ServiceResult<InstallmentScheduleItemResponse>.Success(ToInstallmentResponse(installment));
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

    private async Task AddSalePaymentRecordsAsync(
        PropertySaleContract contract,
        decimal amount,
        string? idempotencyKey,
        string description,
        CancellationToken cancellationToken)
    {
        var occurredAtUtc = contract.ContractDate.ToDateTime(TimeOnly.MinValue);
        var payment = new Payment
        {
            CompoundId = contract.CompoundId,
            ResidentProfileId = contract.ResidentProfileId,
            TargetType = PaymentTargetType.PropertySaleContract,
            TargetId = contract.Id,
            PaymentMethod = PaymentMethod.ManualAdminPayment,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = amount,
            Currency = "IQD",
            IdempotencyKey = idempotencyKey,
            PaymentReference = await GenerateUniquePaymentReferenceAsync(cancellationToken),
            CompletedAt = occurredAtUtc,
            Attempts =
            [
                new PaymentAttempt
                {
                    AttemptStatus = PaymentStatus.Succeeded,
                    Provider = PaymentMethod.ManualAdminPayment.ToString(),
                    Message = description
                }
            ],
            Receipt = new Receipt
            {
                ReceiptNumber = await GenerateUniqueReceiptNumberAsync(cancellationToken),
                Amount = amount,
                IssuedAt = occurredAtUtc
            }
        };
        payment.Receipt.PaymentId = payment.Id;

        dbContext.Payments.Add(payment);
        dbContext.ResidentLedgerEntries.Add(new ResidentLedgerEntry
        {
            CompoundId = contract.CompoundId,
            ResidentProfileId = contract.ResidentProfileId,
            Direction = FinancialLedgerEntryDirection.Debit,
            SourceType = FinancialLedgerSourceType.PropertySaleContract,
            SourceId = contract.Id,
            Amount = amount,
            Currency = "IQD",
            Reference = contract.ContractNumber,
            Description = contract.SaleType == SaleType.Cash
                ? "Cash property sale amount due"
                : "Property sale down payment due",
            OccurredAtUtc = occurredAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        });
        dbContext.ResidentLedgerEntries.Add(new ResidentLedgerEntry
        {
            CompoundId = contract.CompoundId,
            ResidentProfileId = contract.ResidentProfileId,
            Direction = FinancialLedgerEntryDirection.Credit,
            SourceType = FinancialLedgerSourceType.Payment,
            SourceId = payment.Id,
            Amount = amount,
            Currency = "IQD",
            Reference = payment.PaymentReference,
            Description = description,
            OccurredAtUtc = occurredAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private async Task<string> GenerateUniquePaymentReferenceAsync(CancellationToken cancellationToken)
    {
        string reference;
        do
        {
            reference = $"PAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        }
        while (await dbContext.Payments.AsNoTracking().AnyAsync(payment => payment.PaymentReference == reference, cancellationToken));

        return reference;
    }

    private async Task<string> GenerateUniqueReceiptNumberAsync(CancellationToken cancellationToken)
    {
        string receiptNumber;
        do
        {
            receiptNumber = $"RCT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        }
        while (await dbContext.Receipts.AsNoTracking().AnyAsync(receipt => receipt.ReceiptNumber == receiptNumber, cancellationToken));

        return receiptNumber;
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
