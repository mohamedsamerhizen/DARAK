using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Meters;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class MeterServiceTests
{
    [Fact]
    public async Task CreateMeterReadingAsync_RejectsDuplicateMonth()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedMeterAsync(dbContext);
        dbContext.MeterReadings.Add(CreateReading(seed, 2026, 5, 0m, 100m));
        await dbContext.SaveChangesAsync();

        var service = new MeterService(dbContext);
        var result = await service.CreateMeterReadingAsync(new CreateMeterReadingRequest
        {
            MeterId = seed.MeterId,
            Year = 2026,
            Month = 5,
            CurrentReading = 110m
        });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
    }

    [Fact]
    public async Task CreateMeterReadingAsync_RejectsCurrentReadingLowerThanPrevious()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedMeterAsync(dbContext);
        dbContext.MeterReadings.Add(CreateReading(seed, 2026, 5, 0m, 100m));
        await dbContext.SaveChangesAsync();

        var service = new MeterService(dbContext);
        var result = await service.CreateMeterReadingAsync(new CreateMeterReadingRequest
        {
            MeterId = seed.MeterId,
            Year = 2026,
            Month = 6,
            CurrentReading = 90m
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task UpdateMeterReadingAsync_RejectsHistoricalEditAboveNextReading()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedMeterAsync(dbContext);
        var may = CreateReading(seed, 2026, 5, 0m, 100m);
        var june = CreateReading(seed, 2026, 6, 100m, 150m);
        dbContext.MeterReadings.AddRange(may, june);
        await dbContext.SaveChangesAsync();

        var service = new MeterService(dbContext);
        var result = await service.UpdateMeterReadingAsync(may.Id, new UpdateMeterReadingRequest
        {
            CurrentReading = 160m
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task UpdateMeterReadingAsync_RecalculatesNextUnbilledReadingWhenHistoricalReadingChanges()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedMeterAsync(dbContext);
        var may = CreateReading(seed, 2026, 5, 0m, 100m);
        var june = CreateReading(seed, 2026, 6, 100m, 150m);
        dbContext.MeterReadings.AddRange(may, june);
        await dbContext.SaveChangesAsync();

        var service = new MeterService(dbContext);
        var result = await service.UpdateMeterReadingAsync(may.Id, new UpdateMeterReadingRequest
        {
            CurrentReading = 120m
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        var next = await dbContext.MeterReadings.SingleAsync(reading => reading.Id == june.Id);
        next.PreviousReading.Should().Be(120m);
        next.Consumption.Should().Be(30m);
    }

    [Fact]
    public async Task UpdateMeterReadingAsync_RejectsBilledReading()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedMeterAsync(dbContext);
        var reading = CreateReading(seed, 2026, 5, 0m, 100m);
        reading.IsBilled = true;
        dbContext.MeterReadings.Add(reading);
        await dbContext.SaveChangesAsync();

        var service = new MeterService(dbContext);
        var result = await service.UpdateMeterReadingAsync(reading.Id, new UpdateMeterReadingRequest
        {
            CurrentReading = 101m
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task SearchResidentMeterReadingsAsync_ExcludesReadingsAfterOccupancyEndDate()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedMeterAsync(dbContext);
        var userId = Guid.NewGuid();
        var resident = new ResidentProfile
        {
            UserId = userId,
            CompoundId = seed.CompoundId,
            FullName = "Resident"
        };
        var occupancy = new OccupancyRecord
        {
            ResidentProfileId = resident.Id,
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.PropertyUnitId,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Ended,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 3, 31)
        };
        var february = CreateReading(seed, 2026, 2, 0m, 100m);
        var april = CreateReading(seed, 2026, 4, 100m, 150m);
        dbContext.AddRange(resident, occupancy, february, april);
        await dbContext.SaveChangesAsync();

        var service = new MeterService(dbContext);

        var result = await service.SearchResidentMeterReadingsAsync(userId, new MeterReadingSearchQuery());

        result.TotalCount.Should().Be(1);
        result.Items.Single().Id.Should().Be(february.Id);
    }

    private static async Task<MeterSeed> SeedMeterAsync(DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        var compound = new Compound { Name = "Darak", Code = "D1", City = "Baghdad", Area = "Karrada" };
        var unit = new PropertyUnit
        {
            CompoundId = compound.Id,
            UnitNumber = "A-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied
        };
        var meter = new Meter
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            MeterType = MeterType.Water,
            MeterNumber = "W-1",
            RatePerUnit = 2m
        };

        dbContext.AddRange(compound, unit, meter);
        await dbContext.SaveChangesAsync();

        return new MeterSeed(compound.Id, unit.Id, meter.Id);
    }

    private static MeterReading CreateReading(
        MeterSeed seed,
        int year,
        int month,
        decimal previousReading,
        decimal currentReading)
    {
        return new MeterReading
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.PropertyUnitId,
            MeterId = seed.MeterId,
            Year = year,
            Month = month,
            PreviousReading = previousReading,
            CurrentReading = currentReading,
            Consumption = currentReading - previousReading,
            RatePerUnit = 2m,
            Amount = (currentReading - previousReading) * 2m
        };
    }

    private sealed record MeterSeed(Guid CompoundId, Guid PropertyUnitId, Guid MeterId);
}
