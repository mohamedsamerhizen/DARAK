using System.Text.Json;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Meters;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.DTOs.PropertySales;
using DARAK.Api.DTOs.Rents;
using DARAK.Api.DTOs.UtilityBills;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class ResidentPortalPrivacyTests
{
    [Fact]
    public async Task GetDashboardAsync_UsesOnlyCurrentUsersActiveResidentProfiles()
    {
        await using var dbContext = TestDb.Create();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var seed = await SeedResidentPortalScopeAsync(dbContext, currentUserId, otherUserId);
        var service = CreateService(dbContext, currentUserId);

        var result = await service.GetDashboardAsync();

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.ResidentProfileId.Should().Be(seed.CurrentResidentProfileId);
        result.Value.ActivePropertiesCount.Should().Be(1);
        result.Value.TotalOutstandingAmount.Should().Be(80m);
        result.Value.UnpaidUtilityBillsCount.Should().Be(1);
        result.Value.Properties.Single().PropertyUnitId.Should().Be(seed.CurrentUnitId);
    }

    [Fact]
    public async Task GetDashboardAsync_IncludesOnlyCurrentResidentsVisibleViolationFinesInFinancialTotals()
    {
        await using var dbContext = TestDb.Create();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var seed = await SeedResidentPortalScopeAsync(dbContext, currentUserId, otherUserId);
        await SeedViolationFinesAsync(dbContext, seed);
        var service = CreateService(dbContext, currentUserId);

        var result = await service.GetDashboardAsync();

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.TotalOutstandingAmount.Should().Be(135m);
        result.Value.OverdueAmount.Should().Be(30m);
        result.Value.UpcomingDueItems.Should().Contain(item =>
            item.Type == "ViolationFine"
            && item.Amount == 30m
            && item.Status == "Overdue");
        result.Value.UpcomingDueItems.Should().Contain(item =>
            item.Type == "ViolationFine"
            && item.Amount == 25m
            && item.Status == ViolationFineStatus.PartiallyPaid.ToString());
    }

    [Fact]
    public async Task GetDashboardAsync_ReturnsNotFoundWhenUserHasNoActiveResidentProfile()
    {
        await using var dbContext = TestDb.Create();
        var service = CreateService(dbContext, Guid.NewGuid());

        var result = await service.GetDashboardAsync();

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task GetMyBillsAsync_RejectsResidentProfileFilterOnResidentEndpoint()
    {
        await using var dbContext = TestDb.Create();
        var currentUserId = Guid.NewGuid();
        await SeedResidentPortalScopeAsync(dbContext, currentUserId, Guid.NewGuid());
        var service = CreateService(dbContext, currentUserId);

        var result = await service.GetMyBillsAsync(
            new UtilityBillSearchQuery { ResidentProfileId = Guid.NewGuid() });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task GetDashboardAsync_CountsOnlyOpenMeterReadingsVisibleToActiveOccupancy()
    {
        await using var dbContext = TestDb.Create();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var seed = await SeedResidentPortalScopeAsync(dbContext, currentUserId, otherUserId);
        await SeedMeterReadingsAsync(dbContext, seed);
        var service = CreateService(dbContext, currentUserId);

        var result = await service.GetDashboardAsync();

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.OpenMeterReadingsCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchOccupanciesForUserAsync_ReturnsResidentSafeOccupancyShapeWithoutInternalNotes()
    {
        await using var dbContext = TestDb.Create();
        var currentUserId = Guid.NewGuid();
        var seed = await SeedResidentPortalScopeAsync(dbContext, currentUserId, Guid.NewGuid());
        var occupancy = await dbContext.OccupancyRecords.FindAsync(seed.CurrentOccupancyId);
        occupancy!.Notes = "Internal legal and risk note - do not show resident";
        await dbContext.SaveChangesAsync();

        var service = new OccupancyService(dbContext);

        var result = await service.SearchOccupanciesForUserAsync(
            currentUserId,
            new PaginationQuery());

        result.Items.Should().ContainSingle();
        var serialized = JsonSerializer.Serialize(result);
        serialized.Should().NotContain("Notes");
        serialized.Should().NotContain("notes");
        serialized.Should().NotContain("Internal legal and risk note");
    }

    private static ResidentPortalService CreateService(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        Guid currentUserId)
    {
        return new ResidentPortalService(
            dbContext,
            new FakeCurrentUserService(currentUserId),
            null!,
            null!,
            null!,
            null!);
    }

    private static async Task<ResidentPortalSeed> SeedResidentPortalScopeAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        Guid currentUserId,
        Guid otherUserId)
    {
        var currentCompound = CreateCompound("Current");
        var otherCompound = CreateCompound("Other");
        var currentUnit = CreateUnit(currentCompound.Id, "A-101");
        var otherUnit = CreateUnit(otherCompound.Id, "B-101");
        var currentProfile = new ResidentProfile
        {
            UserId = currentUserId,
            CompoundId = currentCompound.Id,
            FullName = "Current Resident"
        };
        var otherProfile = new ResidentProfile
        {
            UserId = otherUserId,
            CompoundId = otherCompound.Id,
            FullName = "Other Resident"
        };
        var currentOccupancy = new OccupancyRecord
        {
            CompoundId = currentCompound.Id,
            PropertyUnitId = currentUnit.Id,
            ResidentProfileId = currentProfile.Id,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = new DateOnly(2026, 1, 1)
        };
        var otherOccupancy = new OccupancyRecord
        {
            CompoundId = otherCompound.Id,
            PropertyUnitId = otherUnit.Id,
            ResidentProfileId = otherProfile.Id,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = new DateOnly(2026, 1, 1)
        };
        var currentCycle = CreateBillingCycle(currentCompound.Id, 2026, 6);
        var otherCycle = CreateBillingCycle(otherCompound.Id, 2026, 6);
        var currentBill = new UtilityBill
        {
            CompoundId = currentCompound.Id,
            PropertyUnitId = currentUnit.Id,
            ResidentProfileId = currentProfile.Id,
            BillingCycleId = currentCycle.Id,
            BillNumber = "UB-CURRENT",
            IssueDate = new DateOnly(2026, 6, 1),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            SubtotalAmount = 100m,
            TotalAmount = 100m,
            PaidAmount = 20m,
            BillStatus = BillStatus.PartiallyPaid
        };
        var otherBill = new UtilityBill
        {
            CompoundId = otherCompound.Id,
            PropertyUnitId = otherUnit.Id,
            ResidentProfileId = otherProfile.Id,
            BillingCycleId = otherCycle.Id,
            BillNumber = "UB-OTHER",
            IssueDate = new DateOnly(2026, 6, 1),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            SubtotalAmount = 500m,
            TotalAmount = 500m,
            PaidAmount = 0m,
            BillStatus = BillStatus.Unpaid
        };

        dbContext.AddRange(
            currentCompound,
            otherCompound,
            currentUnit,
            otherUnit,
            currentProfile,
            otherProfile,
            currentOccupancy,
            otherOccupancy,
            currentCycle,
            otherCycle,
            currentBill,
            otherBill);
        await dbContext.SaveChangesAsync();

        return new ResidentPortalSeed(
            currentCompound.Id,
            otherCompound.Id,
            currentUnit.Id,
            otherUnit.Id,
            currentProfile.Id,
            otherProfile.Id,
            currentOccupancy.Id);
    }

    private static async Task SeedViolationFinesAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        ResidentPortalSeed seed)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentViolation = CreateViolation(
            seed.CurrentCompoundId,
            seed.CurrentResidentProfileId,
            seed.CurrentUnitId,
            "Current resident violation");
        var otherViolation = CreateViolation(
            seed.OtherCompoundId,
            seed.OtherResidentProfileId,
            seed.OtherUnitId,
            "Other resident violation");

        dbContext.Violations.AddRange(currentViolation, otherViolation);
        dbContext.ViolationFines.AddRange(
            CreateFine(currentViolation, 30m, 0m, ViolationFineStatus.Unpaid, today.AddDays(-1), "Overdue fine"),
            CreateFine(currentViolation, 40m, 15m, ViolationFineStatus.PartiallyPaid, today.AddDays(5), "Partially paid fine"),
            CreateFine(currentViolation, 100m, 100m, ViolationFineStatus.Paid, today.AddDays(-2), "Paid fine"),
            CreateFine(currentViolation, 999m, 0m, ViolationFineStatus.Cancelled, today.AddDays(-3), "Cancelled fine"),
            CreateFine(otherViolation, 700m, 0m, ViolationFineStatus.Unpaid, today.AddDays(-4), "Other resident fine"));

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedMeterReadingsAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        ResidentPortalSeed seed)
    {
        var currentMeter = new Meter
        {
            CompoundId = seed.CurrentCompoundId,
            PropertyUnitId = seed.CurrentUnitId,
            MeterType = MeterType.Electricity,
            MeterNumber = "M-CURRENT",
            RatePerUnit = 1m
        };
        var otherMeter = new Meter
        {
            CompoundId = seed.CurrentCompoundId,
            PropertyUnitId = seed.OtherUnitId,
            MeterType = MeterType.Electricity,
            MeterNumber = "M-OTHER",
            RatePerUnit = 1m
        };
        var visibleReading = CreateReading(
            seed.CurrentCompoundId,
            seed.CurrentUnitId,
            currentMeter.Id,
            year: 2026,
            month: 6,
            isBilled: false);
        var alreadyBilledReading = CreateReading(
            seed.CurrentCompoundId,
            seed.CurrentUnitId,
            currentMeter.Id,
            year: 2026,
            month: 7,
            isBilled: true);
        var otherUnitReading = CreateReading(
            seed.CurrentCompoundId,
            seed.OtherUnitId,
            otherMeter.Id,
            year: 2026,
            month: 6,
            isBilled: false);

        dbContext.AddRange(currentMeter, otherMeter, visibleReading, alreadyBilledReading, otherUnitReading);
        await dbContext.SaveChangesAsync();
    }

    private static Compound CreateCompound(string name)
    {
        return new Compound
        {
            Name = name,
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };
    }

    private static PropertyUnit CreateUnit(Guid compoundId, string unitNumber)
    {
        return new PropertyUnit
        {
            CompoundId = compoundId,
            UnitNumber = unitNumber,
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied
        };
    }

    private static BillingCycle CreateBillingCycle(Guid compoundId, int year, int month)
    {
        return new BillingCycle
        {
            CompoundId = compoundId,
            Year = year,
            Month = month,
            PeriodStart = new DateOnly(year, month, 1),
            PeriodEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
            DueDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month)).AddDays(10)
        };
    }

    private static Violation CreateViolation(
        Guid compoundId,
        Guid residentProfileId,
        Guid propertyUnitId,
        string title)
    {
        return new Violation
        {
            CompoundId = compoundId,
            ResidentProfileId = residentProfileId,
            PropertyUnitId = propertyUnitId,
            ViolationType = ViolationType.Other,
            Title = title,
            Description = title
        };
    }

    private static ViolationFine CreateFine(
        Violation violation,
        decimal amount,
        decimal paidAmount,
        ViolationFineStatus status,
        DateOnly dueDate,
        string reason)
    {
        return new ViolationFine
        {
            ViolationId = violation.Id,
            CompoundId = violation.CompoundId,
            ResidentProfileId = violation.ResidentProfileId,
            Amount = amount,
            PaidAmount = paidAmount,
            Status = status,
            DueDate = dueDate,
            Reason = reason
        };
    }

    private static MeterReading CreateReading(
        Guid compoundId,
        Guid unitId,
        Guid meterId,
        int year,
        int month,
        bool isBilled)
    {
        return new MeterReading
        {
            CompoundId = compoundId,
            PropertyUnitId = unitId,
            MeterId = meterId,
            Year = year,
            Month = month,
            PreviousReading = 10m,
            CurrentReading = 20m,
            Consumption = 10m,
            RatePerUnit = 1m,
            Amount = 10m,
            IsBilled = isBilled
        };
    }

    private sealed class FakeCurrentUserService(Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
    }

    private sealed record ResidentPortalSeed(
        Guid CurrentCompoundId,
        Guid OtherCompoundId,
        Guid CurrentUnitId,
        Guid OtherUnitId,
        Guid CurrentResidentProfileId,
        Guid OtherResidentProfileId,
        Guid CurrentOccupancyId);
}
