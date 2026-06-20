using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Meters;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class SmartMeterOperationsServiceTests
{
    [Fact]
    public async Task RegisterDeviceAsync_CreatesCompoundScopedSmartMeterDevice()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "SM-1");
        var unit = await AddUnitAsync(dbContext, compound.Id, "U-101");
        var meter = await AddMeterAsync(dbContext, compound.Id, unit.Id, "MTR-SM-1");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.RegisterDeviceAsync(new RegisterSmartMeterDeviceRequest
        {
            CompoundId = compound.Id,
            MeterId = meter.Id,
            DeviceIdentifier = "DEV-SM-1",
            ProviderName = "IoT Provider",
            FirmwareVersion = "1.0.0",
            SuspiciousConsumptionThreshold = 250
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.DeviceIdentifier.Should().Be("DEV-SM-1");
        result.Value.MeterId.Should().Be(meter.Id);
        dbContext.SmartMeterDevices.Should().ContainSingle(item => item.CompoundId == compound.Id && item.DeviceIdentifier == "DEV-SM-1");
    }

    [Fact]
    public async Task RegisterDeviceAsync_RejectsDuplicateDeviceIdentifierInsideCompound()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "SM-2");
        var unit = await AddUnitAsync(dbContext, compound.Id, "U-201");
        var meter = await AddMeterAsync(dbContext, compound.Id, unit.Id, "MTR-SM-2");
        var service = CreateService(dbContext, compound.Id);
        var request = new RegisterSmartMeterDeviceRequest
        {
            CompoundId = compound.Id,
            MeterId = meter.Id,
            DeviceIdentifier = "DEV-SM-2",
            ProviderName = "Provider"
        };

        var first = await service.RegisterDeviceAsync(request);
        var second = await service.RegisterDeviceAsync(request);

        first.IsSuccess.Should().BeTrue(first.Message);
        second.Status.Should().Be(ServiceResultStatus.Conflict);
    }

    [Fact]
    public async Task IngestReadingAsync_AcceptsNormalReadingAndCreatesMeterReading()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "SM-3");
        var unit = await AddUnitAsync(dbContext, compound.Id, "U-301");
        var meter = await AddMeterAsync(dbContext, compound.Id, unit.Id, "MTR-SM-3", ratePerUnit: 2);
        var service = CreateService(dbContext, compound.Id);
        var device = await service.RegisterDeviceAsync(new RegisterSmartMeterDeviceRequest
        {
            CompoundId = compound.Id,
            MeterId = meter.Id,
            DeviceIdentifier = "DEV-SM-3",
            ProviderName = "Provider",
            SuspiciousConsumptionThreshold = 500
        });

        var ingestion = await service.IngestReadingAsync(
            device.Value!.Id,
            new IngestSmartMeterReadingRequest
            {
                Year = 2026,
                Month = 6,
                CurrentReading = 120,
                ProviderReference = "REF-1"
            });

        ingestion.IsSuccess.Should().BeTrue(ingestion.Message);
        ingestion.Value!.Status.Should().Be(SmartMeterReadingIngestionStatus.Accepted);
        ingestion.Value.MeterReadingId.Should().NotBeNull();
        dbContext.MeterReadings.Should().ContainSingle(reading => reading.MeterId == meter.Id && reading.CurrentReading == 120 && reading.Amount == 240);
        dbContext.SmartMeterDevices.Single().HealthStatus.Should().Be(SmartMeterDeviceHealthStatus.Online);
    }

    [Fact]
    public async Task IngestReadingAsync_FlagsExcessiveConsumptionWithoutCreatingMeterReading()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "SM-4");
        var unit = await AddUnitAsync(dbContext, compound.Id, "U-401");
        var meter = await AddMeterAsync(dbContext, compound.Id, unit.Id, "MTR-SM-4");
        dbContext.MeterReadings.Add(new MeterReading
        {
            CompoundId = compound.Id,
            MeterId = meter.Id,
            PropertyUnitId = unit.Id,
            Year = 2026,
            Month = 5,
            PreviousReading = 0,
            CurrentReading = 100,
            Consumption = 100,
            RatePerUnit = 1,
            Amount = 100
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);
        var device = await service.RegisterDeviceAsync(new RegisterSmartMeterDeviceRequest
        {
            CompoundId = compound.Id,
            MeterId = meter.Id,
            DeviceIdentifier = "DEV-SM-4",
            ProviderName = "Provider",
            SuspiciousConsumptionThreshold = 50
        });

        var ingestion = await service.IngestReadingAsync(
            device.Value!.Id,
            new IngestSmartMeterReadingRequest
            {
                Year = 2026,
                Month = 6,
                CurrentReading = 250
            });

        ingestion.IsSuccess.Should().BeTrue(ingestion.Message);
        ingestion.Value!.Status.Should().Be(SmartMeterReadingIngestionStatus.Suspicious);
        ingestion.Value.AnomalyType.Should().Be(SmartMeterReadingAnomalyType.ExcessiveConsumption);
        ingestion.Value.BillingHoldRecommended.Should().BeTrue();
        ingestion.Value.MeterReadingId.Should().BeNull();
        dbContext.MeterReadings.Count(reading => reading.MeterId == meter.Id).Should().Be(1);
    }

    [Fact]
    public async Task RefreshDeviceHealthAsync_MarksStaleDevicesOfflineWithinAllowedScope()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "SM-5A");
        var denied = await AddCompoundAsync(dbContext, "SM-5B");
        var allowedUnit = await AddUnitAsync(dbContext, allowed.Id, "U-501");
        var deniedUnit = await AddUnitAsync(dbContext, denied.Id, "U-502");
        var allowedMeter = await AddMeterAsync(dbContext, allowed.Id, allowedUnit.Id, "MTR-SM-5A");
        var deniedMeter = await AddMeterAsync(dbContext, denied.Id, deniedUnit.Id, "MTR-SM-5B");
        dbContext.SmartMeterDevices.AddRange(
            new SmartMeterDevice
            {
                CompoundId = allowed.Id,
                MeterId = allowedMeter.Id,
                DeviceIdentifier = "DEV-SM-5A",
                ProviderName = "Provider",
                Status = SmartMeterDeviceStatus.Active,
                HealthStatus = SmartMeterDeviceHealthStatus.Online,
                OfflineAfterMinutes = 10,
                LastSeenAtUtc = DateTime.UtcNow.AddHours(-2)
            },
            new SmartMeterDevice
            {
                CompoundId = denied.Id,
                MeterId = deniedMeter.Id,
                DeviceIdentifier = "DEV-SM-5B",
                ProviderName = "Provider",
                Status = SmartMeterDeviceStatus.Active,
                HealthStatus = SmartMeterDeviceHealthStatus.Online,
                OfflineAfterMinutes = 10,
                LastSeenAtUtc = DateTime.UtcNow.AddHours(-2)
            });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, allowed.Id);

        var changed = await service.RefreshDeviceHealthAsync(null);
        var summary = await service.GetSummaryAsync(null);

        changed.IsSuccess.Should().BeTrue(changed.Message);
        changed.Value.Should().Be(1);
        summary.Value!.OfflineDeviceCount.Should().Be(1);
        dbContext.SmartMeterDevices.Single(item => item.CompoundId == denied.Id).HealthStatus.Should().Be(SmartMeterDeviceHealthStatus.Online);
    }

    private static SmartMeterOperationsService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new SmartMeterOperationsService(dbContext, new FakeCompoundAccessService([compoundId]));
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = new Compound
        {
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Karrada",
            Address = "Baghdad"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<PropertyUnit> AddUnitAsync(ApplicationDbContext dbContext, Guid compoundId, string number)
    {
        var unit = new PropertyUnit
        {
            CompoundId = compoundId,
            UnitNumber = number,
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 100,
            Bedrooms = 2,
            Bathrooms = 1,
            IsActive = true
        };

        dbContext.PropertyUnits.Add(unit);
        await dbContext.SaveChangesAsync();
        return unit;
    }

    private static async Task<Meter> AddMeterAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid unitId,
        string meterNumber,
        decimal ratePerUnit = 1)
    {
        var meter = new Meter
        {
            CompoundId = compoundId,
            PropertyUnitId = unitId,
            MeterType = MeterType.Electricity,
            MeterNumber = meterNumber,
            RatePerUnit = ratePerUnit,
            IsActive = true
        };

        dbContext.Meters.Add(meter);
        await dbContext.SaveChangesAsync();
        return meter;
    }
}
