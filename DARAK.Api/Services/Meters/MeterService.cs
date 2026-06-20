using System.Data;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Meters;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class MeterService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IMeterService
{
    public async Task<PagedResult<MeterResponse>> SearchMetersAsync(
        MeterSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var meters = (await ApplyCurrentMeterScopeAsync(
                ApplyMeterFilters(GetMeterDetailsQuery(asNoTracking: true), query),
                cancellationToken))
            .OrderBy(meter => meter.Compound.Name)
            .ThenBy(meter => meter.PropertyUnit.UnitNumber)
            .ThenBy(meter => meter.MeterType)
            .ThenBy(meter => meter.MeterNumber);

        var totalCount = await meters.CountAsync(cancellationToken);
        var items = await meters
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<MeterResponse>(
            items.Select(ToMeterResponse).ToArray(),
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    public async Task<ServiceResult<MeterResponse>> GetMeterAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var meters = await ApplyCurrentMeterScopeAsync(
            GetMeterDetailsQuery(asNoTracking: true),
            cancellationToken);

        var meter = await meters
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return meter is null
            ? ServiceResult<MeterResponse>.NotFound("Meter was not found.")
            : ServiceResult<MeterResponse>.Success(ToMeterResponse(meter));
    }

    public async Task<ServiceResult<MeterResponse>> CreateMeterAsync(
        CreateMeterRequest request,
        CancellationToken cancellationToken = default)
    {
        var compoundValidation = await ValidateActiveCompoundAsync(request.CompoundId, cancellationToken);
        if (compoundValidation is not null)
        {
            return ToResult<MeterResponse>(compoundValidation);
        }

        var unitResult = await GetValidatedPropertyUnitAsync(
            request.CompoundId,
            request.PropertyUnitId,
            cancellationToken);
        if (unitResult.Failure is not null)
        {
            return ToResult<MeterResponse>(unitResult.Failure);
        }

        var meterNumber = TrimOrNull(request.MeterNumber);
        if (meterNumber is null)
        {
            return ServiceResult<MeterResponse>.BadRequest("Meter number is required.");
        }

        if (request.RatePerUnit < 0)
        {
            return ServiceResult<MeterResponse>.BadRequest("Rate per unit cannot be negative.");
        }

        var duplicateMeterNumberExists = await dbContext.Meters.AnyAsync(
            meter => meter.CompoundId == request.CompoundId && meter.MeterNumber == meterNumber,
            cancellationToken);
        if (duplicateMeterNumberExists)
        {
            return ServiceResult<MeterResponse>.Conflict("Meter number already exists in this compound.");
        }

        var activeMeterExists = await dbContext.Meters.AnyAsync(
            meter => meter.PropertyUnitId == request.PropertyUnitId
                && meter.MeterType == request.MeterType
                && meter.IsActive,
            cancellationToken);
        if (activeMeterExists)
        {
            return ServiceResult<MeterResponse>.Conflict(
                "An active meter already exists for this property unit and meter type.");
        }

        var meter = new Meter
        {
            CompoundId = request.CompoundId,
            PropertyUnitId = request.PropertyUnitId,
            MeterType = request.MeterType,
            MeterNumber = meterNumber,
            RatePerUnit = request.RatePerUnit
        };

        dbContext.Meters.Add(meter);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetMeterAsync(meter.Id, cancellationToken);
    }

    public async Task<ServiceResult<MeterResponse>> UpdateMeterAsync(
        Guid id,
        UpdateMeterRequest request,
        CancellationToken cancellationToken = default)
    {
        var meters = await ApplyCurrentMeterScopeAsync(
            GetMeterDetailsQuery(asNoTracking: false),
            cancellationToken);

        var meter = await meters
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (meter is null)
        {
            return ServiceResult<MeterResponse>.NotFound("Meter was not found.");
        }

        if (!meter.Compound.IsActive)
        {
            return ServiceResult<MeterResponse>.BadRequest("Compound is inactive.");
        }

        if (!meter.PropertyUnit.IsActive)
        {
            return ServiceResult<MeterResponse>.BadRequest("Property unit is inactive.");
        }

        var meterNumber = TrimOrNull(request.MeterNumber);
        if (meterNumber is null)
        {
            return ServiceResult<MeterResponse>.BadRequest("Meter number is required.");
        }

        if (request.RatePerUnit < 0)
        {
            return ServiceResult<MeterResponse>.BadRequest("Rate per unit cannot be negative.");
        }

        var duplicateMeterNumberExists = await dbContext.Meters.AnyAsync(
            item => item.Id != id
                && item.CompoundId == meter.CompoundId
                && item.MeterNumber == meterNumber,
            cancellationToken);
        if (duplicateMeterNumberExists)
        {
            return ServiceResult<MeterResponse>.Conflict("Meter number already exists in this compound.");
        }

        if (request.IsActive)
        {
            var activeMeterExists = await dbContext.Meters.AnyAsync(
                item => item.Id != id
                    && item.PropertyUnitId == meter.PropertyUnitId
                    && item.MeterType == meter.MeterType
                    && item.IsActive,
                cancellationToken);
            if (activeMeterExists)
            {
                return ServiceResult<MeterResponse>.Conflict(
                    "An active meter already exists for this property unit and meter type.");
            }
        }

        meter.MeterNumber = meterNumber;
        meter.RatePerUnit = request.RatePerUnit;
        meter.IsActive = request.IsActive;
        meter.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<MeterResponse>.Success(ToMeterResponse(meter));
    }

    public async Task<ServiceResult<object?>> DeactivateMeterAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var meter = await dbContext.Meters
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (meter is null)
        {
            return ServiceResult<object?>.NotFound("Meter was not found.");
        }

        if (!await CanCurrentUserAccessCompoundAsync(meter.CompoundId, cancellationToken))
        {
            return ServiceResult<object?>.NotFound("Meter was not found.");
        }

        meter.IsActive = false;
        meter.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object?>.Success(null);
    }

    public async Task<PagedResult<MeterReadingResponse>> SearchMeterReadingsAsync(
        MeterReadingSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        return await SearchMeterReadingsCoreAsync(
            await ApplyCurrentMeterReadingScopeAsync(
                ApplyMeterReadingFilters(GetMeterReadingDetailsQuery(asNoTracking: true), query),
                cancellationToken),
            query,
            cancellationToken);
    }

    public async Task<ServiceResult<MeterReadingResponse>> GetMeterReadingAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var meterReadings = await ApplyCurrentMeterReadingScopeAsync(
            GetMeterReadingDetailsQuery(asNoTracking: true),
            cancellationToken);

        var meterReading = await meterReadings
            .FirstOrDefaultAsync(reading => reading.Id == id, cancellationToken);

        return meterReading is null
            ? ServiceResult<MeterReadingResponse>.NotFound("Meter reading was not found.")
            : ServiceResult<MeterReadingResponse>.Success(ToMeterReadingResponse(meterReading));
    }

    public async Task<ServiceResult<MeterReadingResponse>> CreateMeterReadingAsync(
        CreateMeterReadingRequest request,
        CancellationToken cancellationToken = default)
    {
        var meterResult = await GetValidatedMeterForReadingAsync(request.MeterId, cancellationToken);
        if (meterResult.Failure is not null)
        {
            return ToResult<MeterReadingResponse>(meterResult.Failure);
        }

        var dateValidation = ValidateReadingPeriod(request.Year, request.Month);
        if (dateValidation is not null)
        {
            return ToResult<MeterReadingResponse>(dateValidation);
        }

        var duplicateExists = await dbContext.MeterReadings.AnyAsync(
            reading => reading.MeterId == request.MeterId
                && reading.Year == request.Year
                && reading.Month == request.Month,
            cancellationToken);
        if (duplicateExists)
        {
            return ServiceResult<MeterReadingResponse>.Conflict(
                "Meter reading already exists for this meter, year, and month.");
        }

        var previousReading = await GetPreviousReadingValueAsync(
            request.MeterId,
            request.Year,
            request.Month,
            cancellationToken);

        if (request.CurrentReading < previousReading)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest(
                "Current reading cannot be lower than previous reading.");
        }

        var meter = meterResult.Meter!;
        var consumption = request.CurrentReading - previousReading;
        var amount = CalculateAmount(consumption, meter.RatePerUnit);
        var meterReading = new MeterReading
        {
            CompoundId = meter.CompoundId,
            MeterId = meter.Id,
            PropertyUnitId = meter.PropertyUnitId,
            Year = request.Year,
            Month = request.Month,
            PreviousReading = previousReading,
            CurrentReading = request.CurrentReading,
            Consumption = consumption,
            RatePerUnit = meter.RatePerUnit,
            Amount = amount,
            ReadingDate = request.ReadingDate ?? DateTime.UtcNow,
            Notes = TrimOrNull(request.Notes)
        };

        dbContext.MeterReadings.Add(meterReading);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetMeterReadingAsync(meterReading.Id, cancellationToken);
    }

    public async Task<ServiceResult<MeterReadingResponse>> UpdateMeterReadingAsync(
        Guid id,
        UpdateMeterReadingRequest request,
        CancellationToken cancellationToken = default)
    {
        var meterReadings = await ApplyCurrentMeterReadingScopeAsync(
            GetMeterReadingDetailsQuery(asNoTracking: false),
            cancellationToken);

        var meterReading = await meterReadings
            .FirstOrDefaultAsync(reading => reading.Id == id, cancellationToken);

        if (meterReading is null)
        {
            return ServiceResult<MeterReadingResponse>.NotFound("Meter reading was not found.");
        }

        if (!await CanCurrentUserAccessCompoundAsync(meterReading.CompoundId, cancellationToken))
        {
            return ServiceResult<MeterReadingResponse>.NotFound("Meter reading was not found.");
        }

        if (meterReading.IsBilled)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest("Billed meter reading cannot be modified.");
        }

        var previousReadingValue = await GetPreviousReadingValueAsync(
            meterReading.MeterId,
            meterReading.Year,
            meterReading.Month,
            cancellationToken);

        if (request.CurrentReading < previousReadingValue)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest(
                "Current reading cannot be lower than previous reading.");
        }

        var nextReading = await dbContext.MeterReadings
            .Where(reading => reading.MeterId == meterReading.MeterId
                && (reading.Year > meterReading.Year
                    || (reading.Year == meterReading.Year && reading.Month > meterReading.Month)))
            .OrderBy(reading => reading.Year)
            .ThenBy(reading => reading.Month)
            .FirstOrDefaultAsync(cancellationToken);
        if (nextReading is not null && request.CurrentReading > nextReading.CurrentReading)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest(
                "Current reading cannot be greater than the next recorded meter reading.");
        }

        if (nextReading is { IsBilled: true } && request.CurrentReading != nextReading.PreviousReading)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest(
                "Historical meter reading cannot be changed because the next reading is already billed.");
        }

        if (nextReading is { UtilityBillLineId: not null } && request.CurrentReading != nextReading.PreviousReading)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest(
                "Historical meter reading cannot be changed because the next reading has generated a bill line.");
        }

        meterReading.PreviousReading = previousReadingValue;
        meterReading.CurrentReading = request.CurrentReading;
        meterReading.Consumption = request.CurrentReading - previousReadingValue;
        meterReading.Amount = CalculateAmount(meterReading.Consumption, meterReading.RatePerUnit);
        meterReading.Notes = TrimOrNull(request.Notes);
        meterReading.UpdatedAt = DateTime.UtcNow;

        if (nextReading is { IsBilled: false, UtilityBillLineId: null })
        {
            nextReading.PreviousReading = request.CurrentReading;
            nextReading.Consumption = nextReading.CurrentReading - request.CurrentReading;
            nextReading.Amount = CalculateAmount(nextReading.Consumption, nextReading.RatePerUnit);
            nextReading.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<MeterReadingResponse>.Success(ToMeterReadingResponse(meterReading));
    }

    public async Task<PagedResult<MeterReadingResponse>> SearchResidentMeterReadingsAsync(
        Guid userId,
        MeterReadingSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var readings = ApplyResidentMeterReadingScope(
            GetMeterReadingDetailsQuery(asNoTracking: true),
            userId);

        readings = ApplyMeterReadingFilters(readings, query);

        return await SearchMeterReadingsCoreAsync(readings, query, cancellationToken);
    }

    public async Task<ServiceResult<MeterReadingResponse>> GetResidentMeterReadingAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var meterReading = await ApplyResidentMeterReadingScope(
                GetMeterReadingDetailsQuery(asNoTracking: true),
                userId)
            .Where(reading => reading.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        return meterReading is null
            ? ServiceResult<MeterReadingResponse>.NotFound("Meter reading was not found.")
            : ServiceResult<MeterReadingResponse>.Success(ToMeterReadingResponse(meterReading));
    }

    public async Task<ServiceResult<MeterReadingResponse>> GenerateBillLineFromReadingAsync(
        Guid id,
        GenerateBillLineFromReadingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.MeterReadingId == Guid.Empty)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest("Meter reading id is required.");
        }

        if (request.MeterReadingId != id)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest(
                "Meter reading id must match the route id.");
        }

        if (request.UtilityBillId == Guid.Empty)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest("Utility bill id is required.");
        }

        if (request.CompoundServiceId == Guid.Empty)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest("Compound service id is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var meterReading = await GetMeterReadingDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(reading => reading.Id == id, cancellationToken);
        if (meterReading is null)
        {
            return ServiceResult<MeterReadingResponse>.NotFound("Meter reading was not found.");
        }

        if (meterReading.IsBilled || meterReading.UtilityBillLineId.HasValue)
        {
            return ServiceResult<MeterReadingResponse>.Conflict("Meter reading is already billed.");
        }

        var utilityBill = await dbContext.UtilityBills
            .Include(bill => bill.Lines)
            .FirstOrDefaultAsync(bill => bill.Id == request.UtilityBillId, cancellationToken);
        if (utilityBill is null)
        {
            return ServiceResult<MeterReadingResponse>.NotFound("Utility bill was not found.");
        }

        if (utilityBill.BillStatus != BillStatus.Unpaid)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest(
                "Only unpaid utility bills can receive new meter bill lines.");
        }

        if (utilityBill.PaidAmount > 0)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest(
                "Utility bill with a recorded paid amount cannot receive new meter bill lines.");
        }

        var hasPaymentActivity = await dbContext.Payments
            .AsNoTracking()
            .AnyAsync(payment =>
                payment.TargetType == PaymentTargetType.UtilityBill
                && payment.TargetId == utilityBill.Id
                && payment.PaymentStatus != PaymentStatus.Failed
                && payment.PaymentStatus != PaymentStatus.Cancelled,
                cancellationToken);
        if (hasPaymentActivity)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest(
                "Utility bill with payment activity cannot receive new meter bill lines.");
        }

        if (utilityBill.CompoundId != meterReading.CompoundId)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest(
                "Utility bill must belong to the same compound as the meter reading.");
        }

        if (utilityBill.PropertyUnitId != meterReading.PropertyUnitId)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest(
                "Utility bill must belong to the same property unit as the meter reading.");
        }

        var compoundService = await dbContext.CompoundServices
            .AsNoTracking()
            .FirstOrDefaultAsync(service => service.Id == request.CompoundServiceId, cancellationToken);
        if (compoundService is null)
        {
            return ServiceResult<MeterReadingResponse>.NotFound("Compound service was not found.");
        }

        if (!compoundService.IsActive)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest("Compound service is inactive.");
        }

        if (compoundService.CompoundId != meterReading.CompoundId)
        {
            return ServiceResult<MeterReadingResponse>.BadRequest(
                "Compound service must belong to the same compound as the meter reading.");
        }

        if (compoundService.ServiceType != ToUtilityServiceType(meterReading.Meter.MeterType))
        {
            return ServiceResult<MeterReadingResponse>.BadRequest(
                "Compound service type does not match meter type.");
        }

        var description = TrimOrNull(request.DescriptionOverride)
            ?? $"{meterReading.Meter.MeterType} consumption {meterReading.Year}-{meterReading.Month:00}";

        var billLine = new UtilityBillLine
        {
            UtilityBillId = utilityBill.Id,
            CompoundServiceId = compoundService.Id,
            Description = description,
            Quantity = meterReading.Consumption,
            UnitPrice = meterReading.RatePerUnit,
            LineTotal = meterReading.Amount
        };

        utilityBill.Lines.Add(billLine);
        utilityBill.SubtotalAmount = utilityBill.Lines.Sum(line => line.LineTotal);
        utilityBill.TotalAmount = utilityBill.SubtotalAmount
            + utilityBill.PreviousBalanceAmount
            + utilityBill.LateFeeAmount
            - utilityBill.DiscountAmount;
        utilityBill.BillStatus = DetermineBillStatus(
            utilityBill.PaidAmount,
            utilityBill.TotalAmount,
            utilityBill.DueDate,
            isCancelled: false);
        utilityBill.UpdatedAt = DateTime.UtcNow;

        var now = DateTime.UtcNow;
        meterReading.IsBilled = true;
        meterReading.UtilityBillId = utilityBill.Id;
        meterReading.UtilityBillLineId = billLine.Id;
        meterReading.BilledAt = now;
        meterReading.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<MeterReadingResponse>.Success(ToMeterReadingResponse(meterReading));
    }

    private IQueryable<Meter> GetMeterDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.Meters
            .Include(meter => meter.Compound)
            .Include(meter => meter.PropertyUnit)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking().AsSplitQuery() : query;
    }

    private IQueryable<MeterReading> GetMeterReadingDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.MeterReadings
            .Include(reading => reading.Compound)
            .Include(reading => reading.Meter)
            .Include(reading => reading.PropertyUnit)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking().AsSplitQuery() : query;
    }

    private static IQueryable<Meter> ApplyMeterFilters(
        IQueryable<Meter> meters,
        MeterSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            meters = meters.Where(meter => meter.CompoundId == query.CompoundId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            meters = meters.Where(meter => meter.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.MeterType.HasValue)
        {
            meters = meters.Where(meter => meter.MeterType == query.MeterType.Value);
        }

        if (query.IsActive.HasValue)
        {
            meters = meters.Where(meter => meter.IsActive == query.IsActive.Value);
        }

        if (HasText(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm!.Trim();
            meters = meters.Where(meter =>
                meter.MeterNumber.Contains(searchTerm)
                || meter.PropertyUnit.UnitNumber.Contains(searchTerm));
        }

        return meters;
    }

    private static IQueryable<MeterReading> ApplyMeterReadingFilters(
        IQueryable<MeterReading> readings,
        MeterReadingSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            readings = readings.Where(reading => reading.CompoundId == query.CompoundId.Value);
        }

        if (query.MeterId.HasValue)
        {
            readings = readings.Where(reading => reading.MeterId == query.MeterId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            readings = readings.Where(reading => reading.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.MeterType.HasValue)
        {
            readings = readings.Where(reading => reading.Meter.MeterType == query.MeterType.Value);
        }

        if (query.Year.HasValue)
        {
            readings = readings.Where(reading => reading.Year == query.Year.Value);
        }

        if (query.Month.HasValue)
        {
            readings = readings.Where(reading => reading.Month == query.Month.Value);
        }

        if (query.IsBilled.HasValue)
        {
            readings = readings.Where(reading => reading.IsBilled == query.IsBilled.Value);
        }

        return readings;
    }

    private async Task<PagedResult<MeterReadingResponse>> SearchMeterReadingsCoreAsync(
        IQueryable<MeterReading> readings,
        MeterReadingSearchQuery query,
        CancellationToken cancellationToken)
    {
        readings = readings
            .OrderByDescending(reading => reading.Year)
            .ThenByDescending(reading => reading.Month)
            .ThenBy(reading => reading.PropertyUnit.UnitNumber)
            .ThenBy(reading => reading.Meter.MeterNumber);

        var totalCount = await readings.CountAsync(cancellationToken);
        var items = await readings
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<MeterReadingResponse>(
            items.Select(ToMeterReadingResponse).ToArray(),
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    private async Task<(Meter? Meter, ValidationFailure? Failure)> GetValidatedMeterForReadingAsync(
        Guid meterId,
        CancellationToken cancellationToken)
    {
        if (meterId == Guid.Empty)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Meter id is required."));
        }

        var meter = await GetMeterDetailsQuery(asNoTracking: true)
            .FirstOrDefaultAsync(item => item.Id == meterId, cancellationToken);
        if (meter is null)
        {
            return (null, new ValidationFailure(ServiceResultStatus.NotFound, "Meter was not found."));
        }

        if (!meter.IsActive)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Meter is inactive."));
        }

        if (!meter.Compound.IsActive)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Compound is inactive."));
        }

        if (!meter.PropertyUnit.IsActive)
        {
            return (null, new ValidationFailure(ServiceResultStatus.BadRequest, "Property unit is inactive."));
        }

        if (meter.PropertyUnit.CompoundId != meter.CompoundId)
        {
            return (null, new ValidationFailure(
                ServiceResultStatus.BadRequest,
                "Property unit must belong to the meter compound."));
        }

        return (meter, null);
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

        return compound.IsActive
            ? await CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken)
                ? null
                : new ValidationFailure(ServiceResultStatus.Forbidden, "Current user cannot access this compound.")
            : new ValidationFailure(ServiceResultStatus.BadRequest, "Compound is inactive.");
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
                "Property unit must belong to the meter compound."));
        }

        return (propertyUnit, null);
    }

    private async Task<decimal> GetPreviousReadingValueAsync(
        Guid meterId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        return await dbContext.MeterReadings
            .AsNoTracking()
            .Where(reading => reading.MeterId == meterId
                && (reading.Year < year || (reading.Year == year && reading.Month < month)))
            .OrderByDescending(reading => reading.Year)
            .ThenByDescending(reading => reading.Month)
            .Select(reading => (decimal?)reading.CurrentReading)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;
    }

    private IQueryable<MeterReading> ApplyResidentMeterReadingScope(
        IQueryable<MeterReading> readings,
        Guid userId)
    {
        return readings.Where(reading => dbContext.OccupancyRecords
            .AsNoTracking()
            .Any(record =>
                record.ResidentProfile.UserId == userId
                && record.ResidentProfile.IsActive
                && record.OccupancyStatus != OccupancyStatus.Cancelled
                && record.PropertyUnitId == reading.PropertyUnitId
                && (reading.Year > record.StartDate.Year
                    || (reading.Year == record.StartDate.Year
                        && reading.Month >= record.StartDate.Month))
                && (!record.EndDate.HasValue
                    || reading.Year < record.EndDate.Value.Year
                    || (reading.Year == record.EndDate.Value.Year
                        && reading.Month <= record.EndDate.Value.Month))));
    }

    private static ValidationFailure? ValidateReadingPeriod(int year, int month)
    {
        if (year is < 2000 or > 2100)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Meter reading year is invalid.");
        }

        if (month is < 1 or > 12)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Meter reading month must be from 1 to 12.");
        }

        return null;
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

    private static UtilityServiceType ToUtilityServiceType(MeterType meterType)
    {
        return meterType switch
        {
            MeterType.Electricity => UtilityServiceType.Electricity,
            MeterType.Water => UtilityServiceType.Water,
            MeterType.Gas => UtilityServiceType.Gas,
            MeterType.Generator => UtilityServiceType.Generator,
            _ => UtilityServiceType.Other
        };
    }

    private static decimal CalculateAmount(decimal consumption, decimal ratePerUnit)
    {
        return Math.Round(consumption * ratePerUnit, 2, MidpointRounding.AwayFromZero);
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

    private async Task<IQueryable<Meter>> ApplyCurrentMeterScopeAsync(
        IQueryable<Meter> meters,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return meters;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return meters.ApplyCompoundAccess(scope, meter => meter.CompoundId);
    }

    private async Task<IQueryable<MeterReading>> ApplyCurrentMeterReadingScopeAsync(
        IQueryable<MeterReading> meterReadings,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return meterReadings;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return meterReadings.ApplyCompoundAccess(scope, reading => reading.CompoundId);
    }

    private async Task<bool> CanCurrentUserAccessCompoundAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private static MeterResponse ToMeterResponse(Meter meter)
    {
        return new MeterResponse(
            meter.Id,
            meter.CompoundId,
            meter.Compound.Name,
            meter.PropertyUnitId,
            meter.PropertyUnit.UnitNumber,
            meter.MeterType,
            meter.MeterNumber,
            meter.RatePerUnit,
            meter.IsActive,
            meter.CreatedAt,
            meter.UpdatedAt);
    }

    private static MeterReadingResponse ToMeterReadingResponse(MeterReading meterReading)
    {
        return new MeterReadingResponse(
            meterReading.Id,
            meterReading.CompoundId,
            meterReading.Compound.Name,
            meterReading.MeterId,
            meterReading.Meter.MeterNumber,
            meterReading.Meter.MeterType,
            meterReading.PropertyUnitId,
            meterReading.PropertyUnit.UnitNumber,
            meterReading.Year,
            meterReading.Month,
            meterReading.PreviousReading,
            meterReading.CurrentReading,
            meterReading.Consumption,
            meterReading.RatePerUnit,
            meterReading.Amount,
            meterReading.IsBilled,
            meterReading.UtilityBillId,
            meterReading.UtilityBillLineId,
            meterReading.ReadingDate,
            meterReading.CreatedAt,
            meterReading.UpdatedAt,
            meterReading.BilledAt,
            meterReading.Notes);
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
}
