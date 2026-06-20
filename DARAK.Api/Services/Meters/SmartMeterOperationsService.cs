using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Meters;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class SmartMeterOperationsService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : ISmartMeterOperationsService
{
    private const int MaxIdentifierLength = 100;
    private const int MaxProviderLength = 100;

    public async Task<ServiceResult<SmartMeterOperationsSummaryResponse>> GetSummaryAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<SmartMeterOperationsSummaryResponse>.Forbidden("Current user cannot access smart meter operations.");
        }

        if (compoundId.HasValue && !scope.CanAccess(compoundId.Value))
        {
            return ServiceResult<SmartMeterOperationsSummaryResponse>.NotFound("Compound was not found.");
        }

        var devices = dbContext.SmartMeterDevices.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId);
        var ingestions = dbContext.SmartMeterReadingIngestions.AsNoTracking().ApplyCompoundAccess(scope, item => item.CompoundId);
        if (compoundId.HasValue)
        {
            devices = devices.Where(item => item.CompoundId == compoundId.Value);
            ingestions = ingestions.Where(item => item.CompoundId == compoundId.Value);
        }

        return ServiceResult<SmartMeterOperationsSummaryResponse>.Success(new SmartMeterOperationsSummaryResponse(
            await devices.CountAsync(cancellationToken),
            await devices.CountAsync(item => item.HealthStatus == SmartMeterDeviceHealthStatus.Online, cancellationToken),
            await devices.CountAsync(item => item.HealthStatus == SmartMeterDeviceHealthStatus.Offline, cancellationToken),
            await devices.CountAsync(item => item.HealthStatus == SmartMeterDeviceHealthStatus.Warning, cancellationToken),
            await devices.CountAsync(item => item.Status == SmartMeterDeviceStatus.Suspended, cancellationToken),
            await ingestions.CountAsync(item => item.Status == SmartMeterReadingIngestionStatus.Accepted, cancellationToken),
            await ingestions.CountAsync(item => item.Status == SmartMeterReadingIngestionStatus.Suspicious, cancellationToken),
            await ingestions.CountAsync(item => item.Status == SmartMeterReadingIngestionStatus.Rejected, cancellationToken),
            await ingestions.CountAsync(item => item.BillingHoldRecommended, cancellationToken)));
    }

    public async Task<PagedResult<SmartMeterDeviceResponse>> SearchDevicesAsync(
        SmartMeterDeviceQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        var devices = ApplyDeviceFilters(GetDeviceDetailsQuery(asNoTracking: true), query)
            .ApplyCompoundAccess(scope, item => item.CompoundId)
            .OrderBy(item => item.CompoundId)
            .ThenBy(item => item.Meter.MeterNumber)
            .ThenBy(item => item.DeviceIdentifier);

        var totalCount = await devices.CountAsync(cancellationToken);
        var items = await devices
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<SmartMeterDeviceResponse>(
            items.Select(ToDeviceResponse).ToArray(),
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    public async Task<ServiceResult<SmartMeterDeviceResponse>> GetDeviceAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        var device = await GetDeviceDetailsQuery(asNoTracking: true)
            .ApplyCompoundAccess(scope, item => item.CompoundId)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return device is null
            ? ServiceResult<SmartMeterDeviceResponse>.NotFound("Smart meter device was not found.")
            : ServiceResult<SmartMeterDeviceResponse>.Success(ToDeviceResponse(device));
    }

    public async Task<ServiceResult<SmartMeterDeviceResponse>> RegisterDeviceAsync(
        RegisterSmartMeterDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        var identifier = TrimOrNull(request.DeviceIdentifier);
        var provider = TrimOrNull(request.ProviderName);
        if (identifier is null)
        {
            return ServiceResult<SmartMeterDeviceResponse>.BadRequest("Device identifier is required.");
        }

        if (provider is null)
        {
            return ServiceResult<SmartMeterDeviceResponse>.BadRequest("Provider name is required.");
        }

        if (identifier.Length > MaxIdentifierLength || provider.Length > MaxProviderLength)
        {
            return ServiceResult<SmartMeterDeviceResponse>.BadRequest("Device identifier or provider name exceeds the allowed length.");
        }

        if (request.ExpectedReadIntervalMinutes <= 0 || request.OfflineAfterMinutes <= 0)
        {
            return ServiceResult<SmartMeterDeviceResponse>.BadRequest("Device read interval and offline threshold must be positive.");
        }

        if (request.SuspiciousConsumptionThreshold <= 0)
        {
            return ServiceResult<SmartMeterDeviceResponse>.BadRequest("Suspicious consumption threshold must be positive.");
        }

        if (!await CanAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<SmartMeterDeviceResponse>.NotFound("Compound was not found.");
        }

        var meter = await dbContext.Meters
            .Include(item => item.PropertyUnit)
            .FirstOrDefaultAsync(item => item.Id == request.MeterId, cancellationToken);
        if (meter is null || meter.CompoundId != request.CompoundId)
        {
            return ServiceResult<SmartMeterDeviceResponse>.NotFound("Meter was not found.");
        }

        if (!meter.IsActive)
        {
            return ServiceResult<SmartMeterDeviceResponse>.BadRequest("Smart meter device cannot be linked to an inactive meter.");
        }

        var duplicateIdentifier = await dbContext.SmartMeterDevices.AnyAsync(
            item => item.CompoundId == request.CompoundId && item.DeviceIdentifier == identifier,
            cancellationToken);
        if (duplicateIdentifier)
        {
            return ServiceResult<SmartMeterDeviceResponse>.Conflict("Device identifier already exists in this compound.");
        }

        var activeDeviceForMeter = await dbContext.SmartMeterDevices.AnyAsync(
            item => item.MeterId == request.MeterId && item.Status == SmartMeterDeviceStatus.Active,
            cancellationToken);
        if (activeDeviceForMeter)
        {
            return ServiceResult<SmartMeterDeviceResponse>.Conflict("An active smart device is already linked to this meter.");
        }

        var device = new SmartMeterDevice
        {
            CompoundId = request.CompoundId,
            MeterId = request.MeterId,
            DeviceIdentifier = identifier,
            ProviderName = provider,
            FirmwareVersion = TrimOrNull(request.FirmwareVersion),
            ExpectedReadIntervalMinutes = request.ExpectedReadIntervalMinutes,
            OfflineAfterMinutes = request.OfflineAfterMinutes,
            SuspiciousConsumptionThreshold = request.SuspiciousConsumptionThreshold,
            HealthStatus = SmartMeterDeviceHealthStatus.Unknown
        };

        dbContext.SmartMeterDevices.Add(device);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetDeviceAsync(device.Id, cancellationToken);
    }

    public async Task<ServiceResult<SmartMeterDeviceResponse>> UpdateDeviceStatusAsync(
        Guid id,
        UpdateSmartMeterDeviceStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var device = await GetEditableDeviceAsync(id, cancellationToken);
        if (device is null)
        {
            return ServiceResult<SmartMeterDeviceResponse>.NotFound("Smart meter device was not found.");
        }

        device.Status = request.Status;
        device.UpdatedAtUtc = DateTime.UtcNow;
        if (request.Status != SmartMeterDeviceStatus.Active)
        {
            device.HealthStatus = SmartMeterDeviceHealthStatus.Warning;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<SmartMeterDeviceResponse>.Success(ToDeviceResponse(device));
    }

    public async Task<ServiceResult<SmartMeterReadingIngestionResponse>> IngestReadingAsync(
        Guid deviceId,
        IngestSmartMeterReadingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Year < 2000 || request.Year > 2100 || request.Month < 1 || request.Month > 12)
        {
            return ServiceResult<SmartMeterReadingIngestionResponse>.BadRequest("Reading period is invalid.");
        }

        if (request.CurrentReading < 0)
        {
            return ServiceResult<SmartMeterReadingIngestionResponse>.BadRequest("Current reading cannot be negative.");
        }

        var device = await GetEditableDeviceAsync(deviceId, cancellationToken);
        if (device is null)
        {
            return ServiceResult<SmartMeterReadingIngestionResponse>.NotFound("Smart meter device was not found.");
        }

        var readingTimestamp = request.ReadingTimestampUtc ?? DateTime.UtcNow;
        var previousReading = await GetPreviousReadingValueAsync(device.MeterId, request.Year, request.Month, cancellationToken);
        var consumption = request.CurrentReading - previousReading;
        var status = SmartMeterReadingIngestionStatus.Accepted;
        var anomaly = SmartMeterReadingAnomalyType.None;
        var billingHold = false;
        string? message = null;

        var duplicatePeriod = await dbContext.MeterReadings.AnyAsync(
            item => item.MeterId == device.MeterId && item.Year == request.Year && item.Month == request.Month,
            cancellationToken);

        if (device.Status != SmartMeterDeviceStatus.Active || !device.Meter.IsActive)
        {
            status = SmartMeterReadingIngestionStatus.Rejected;
            anomaly = SmartMeterReadingAnomalyType.InactiveDevice;
            billingHold = true;
            message = "Device or linked meter is inactive.";
        }
        else if (duplicatePeriod)
        {
            status = SmartMeterReadingIngestionStatus.Rejected;
            anomaly = SmartMeterReadingAnomalyType.DuplicatePeriod;
            message = "A meter reading already exists for this device period.";
        }
        else if (request.CurrentReading < previousReading)
        {
            status = SmartMeterReadingIngestionStatus.Suspicious;
            anomaly = SmartMeterReadingAnomalyType.LowerThanPrevious;
            billingHold = true;
            consumption = 0;
            message = "Reading is lower than the previous accepted reading.";
        }
        else if (consumption > device.SuspiciousConsumptionThreshold)
        {
            status = SmartMeterReadingIngestionStatus.Suspicious;
            anomaly = SmartMeterReadingAnomalyType.ExcessiveConsumption;
            billingHold = true;
            message = "Reading consumption exceeds the configured threshold.";
        }

        var ingestion = new SmartMeterReadingIngestion
        {
            CompoundId = device.CompoundId,
            SmartMeterDeviceId = device.Id,
            MeterId = device.MeterId,
            PropertyUnitId = device.Meter.PropertyUnitId,
            Year = request.Year,
            Month = request.Month,
            PreviousReading = previousReading,
            CurrentReading = request.CurrentReading,
            Consumption = consumption < 0 ? 0 : consumption,
            Status = status,
            AnomalyType = anomaly,
            BillingHoldRecommended = billingHold,
            Message = message,
            ProviderReference = TrimOrNull(request.ProviderReference),
            RawPayload = TrimOrNull(request.RawPayload),
            ReadingTimestampUtc = readingTimestamp
        };

        if (status == SmartMeterReadingIngestionStatus.Accepted)
        {
            var meterReading = new MeterReading
            {
                CompoundId = device.CompoundId,
                MeterId = device.MeterId,
                PropertyUnitId = device.Meter.PropertyUnitId,
                Year = request.Year,
                Month = request.Month,
                PreviousReading = previousReading,
                CurrentReading = request.CurrentReading,
                Consumption = consumption,
                RatePerUnit = device.Meter.RatePerUnit,
                Amount = Math.Round(consumption * device.Meter.RatePerUnit, 2, MidpointRounding.AwayFromZero),
                ReadingDate = readingTimestamp,
                Notes = $"Smart meter ingestion from {device.DeviceIdentifier}."
            };
            dbContext.MeterReadings.Add(meterReading);
            ingestion.MeterReadingId = meterReading.Id;
            device.LastReadingValue = request.CurrentReading;
            device.LastReadingAtUtc = readingTimestamp;
        }

        device.LastSeenAtUtc = readingTimestamp;
        device.HealthStatus = status == SmartMeterReadingIngestionStatus.Rejected
            ? SmartMeterDeviceHealthStatus.Warning
            : SmartMeterDeviceHealthStatus.Online;
        device.UpdatedAtUtc = DateTime.UtcNow;

        dbContext.SmartMeterReadingIngestions.Add(ingestion);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<SmartMeterReadingIngestionResponse>.Success(ToIngestionResponse(ingestion));
    }

    public async Task<PagedResult<SmartMeterReadingIngestionResponse>> SearchIngestionsAsync(
        SmartMeterIngestionQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        var ingestions = ApplyIngestionFilters(dbContext.SmartMeterReadingIngestions.AsNoTracking(), query)
            .ApplyCompoundAccess(scope, item => item.CompoundId)
            .OrderByDescending(item => item.CreatedAtUtc);

        var totalCount = await ingestions.CountAsync(cancellationToken);
        var items = await ingestions
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<SmartMeterReadingIngestionResponse>(
            items.Select(ToIngestionResponse).ToArray(),
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    public async Task<ServiceResult<int>> RefreshDeviceHealthAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<int>.Forbidden("Current user cannot access smart meter operations.");
        }

        if (compoundId.HasValue && !scope.CanAccess(compoundId.Value))
        {
            return ServiceResult<int>.NotFound("Compound was not found.");
        }

        var now = DateTime.UtcNow;
        var devices = dbContext.SmartMeterDevices
            .ApplyCompoundAccess(scope, item => item.CompoundId)
            .Where(item => item.Status == SmartMeterDeviceStatus.Active);
        if (compoundId.HasValue)
        {
            devices = devices.Where(item => item.CompoundId == compoundId.Value);
        }

        var items = await devices.ToArrayAsync(cancellationToken);
        var changed = 0;
        foreach (var device in items)
        {
            var lastSeen = device.LastSeenAtUtc ?? device.CreatedAtUtc;
            if (lastSeen.AddMinutes(device.OfflineAfterMinutes) < now
                && device.HealthStatus != SmartMeterDeviceHealthStatus.Offline)
            {
                device.HealthStatus = SmartMeterDeviceHealthStatus.Offline;
                device.UpdatedAtUtc = now;
                changed++;
            }
        }

        if (changed > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<int>.Success(changed);
    }

    private IQueryable<SmartMeterDevice> GetDeviceDetailsQuery(bool asNoTracking)
    {
        var query = dbContext.SmartMeterDevices
            .Include(item => item.Meter)
            .AsQueryable();

        return asNoTracking ? query.AsNoTracking().AsSplitQuery() : query;
    }

    private async Task<SmartMeterDevice?> GetEditableDeviceAsync(Guid id, CancellationToken cancellationToken)
    {
        var device = await GetDeviceDetailsQuery(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (device is null)
        {
            return null;
        }

        return await CanAccessCompoundAsync(device.CompoundId, cancellationToken) ? device : null;
    }

    private async Task<decimal> GetPreviousReadingValueAsync(
        Guid meterId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var previous = await dbContext.MeterReadings
            .Where(item => item.MeterId == meterId
                && (item.Year < year || (item.Year == year && item.Month < month)))
            .OrderByDescending(item => item.Year)
            .ThenByDescending(item => item.Month)
            .Select(item => (decimal?)item.CurrentReading)
            .FirstOrDefaultAsync(cancellationToken);

        return previous ?? 0m;
    }

    private async Task<CompoundAccessScope> GetScopeAsync(CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            ? new CompoundAccessScope(true, true, [])
            : await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
    }

    private async Task<bool> CanAccessCompoundAsync(Guid compoundId, CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private static IQueryable<SmartMeterDevice> ApplyDeviceFilters(
        IQueryable<SmartMeterDevice> devices,
        SmartMeterDeviceQueryRequest query)
    {
        if (query.CompoundId.HasValue)
        {
            devices = devices.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.MeterId.HasValue)
        {
            devices = devices.Where(item => item.MeterId == query.MeterId.Value);
        }

        if (query.Status.HasValue)
        {
            devices = devices.Where(item => item.Status == query.Status.Value);
        }

        if (query.HealthStatus.HasValue)
        {
            devices = devices.Where(item => item.HealthStatus == query.HealthStatus.Value);
        }

        if (query.OfflineOnly)
        {
            devices = devices.Where(item => item.HealthStatus == SmartMeterDeviceHealthStatus.Offline);
        }

        return devices;
    }

    private static IQueryable<SmartMeterReadingIngestion> ApplyIngestionFilters(
        IQueryable<SmartMeterReadingIngestion> ingestions,
        SmartMeterIngestionQueryRequest query)
    {
        if (query.CompoundId.HasValue)
        {
            ingestions = ingestions.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.DeviceId.HasValue)
        {
            ingestions = ingestions.Where(item => item.SmartMeterDeviceId == query.DeviceId.Value);
        }

        if (query.MeterId.HasValue)
        {
            ingestions = ingestions.Where(item => item.MeterId == query.MeterId.Value);
        }

        if (query.Status.HasValue)
        {
            ingestions = ingestions.Where(item => item.Status == query.Status.Value);
        }

        if (query.AnomalyType.HasValue)
        {
            ingestions = ingestions.Where(item => item.AnomalyType == query.AnomalyType.Value);
        }

        if (query.BillingHoldOnly)
        {
            ingestions = ingestions.Where(item => item.BillingHoldRecommended);
        }

        return ingestions;
    }

    private static SmartMeterDeviceResponse ToDeviceResponse(SmartMeterDevice device)
    {
        return new SmartMeterDeviceResponse(
            device.Id,
            device.CompoundId,
            device.MeterId,
            device.Meter.MeterNumber,
            device.Meter.PropertyUnitId,
            device.Meter.MeterType,
            device.DeviceIdentifier,
            device.ProviderName,
            device.FirmwareVersion,
            device.Status,
            device.HealthStatus,
            device.ExpectedReadIntervalMinutes,
            device.OfflineAfterMinutes,
            device.SuspiciousConsumptionThreshold,
            device.LastSeenAtUtc,
            device.LastReadingAtUtc,
            device.LastReadingValue,
            device.CreatedAtUtc,
            device.UpdatedAtUtc);
    }

    private static SmartMeterReadingIngestionResponse ToIngestionResponse(SmartMeterReadingIngestion ingestion)
    {
        return new SmartMeterReadingIngestionResponse(
            ingestion.Id,
            ingestion.CompoundId,
            ingestion.SmartMeterDeviceId,
            ingestion.MeterId,
            ingestion.PropertyUnitId,
            ingestion.MeterReadingId,
            ingestion.Year,
            ingestion.Month,
            ingestion.PreviousReading,
            ingestion.CurrentReading,
            ingestion.Consumption,
            ingestion.Status,
            ingestion.AnomalyType,
            ingestion.BillingHoldRecommended,
            ingestion.Message,
            ingestion.ProviderReference,
            ingestion.ReadingTimestampUtc,
            ingestion.CreatedAtUtc);
    }

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
