using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class ConversationCompoundOwnershipPass05Tests
{
    [Fact]
    public async Task CreateConversationAsync_RejectsLinkedEntityOwnedByAnotherResident()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedConversationOwnershipWorldAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.CreateConversationAsync(new CreateConversationRequest(
            seed.CompoundId,
            seed.ResidentProfileId,
            PropertyUnitId: null,
            ConversationTopic.Billing,
            ConversationIssueType.BillingMeterReadingIssue,
            ConversationLinkedEntityType.UtilityBill,
            seed.OtherResidentBillId,
            "This bill is not mine.",
            seed.ResidentUserId));

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        (await dbContext.Conversations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateConversationAsync_RejectsLinkedEntityFromAnotherCompound()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedConversationOwnershipWorldAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.CreateConversationAsync(new CreateConversationRequest(
            seed.CompoundId,
            seed.ResidentProfileId,
            PropertyUnitId: null,
            ConversationTopic.Billing,
            ConversationIssueType.BillingMeterReadingIssue,
            ConversationLinkedEntityType.UtilityBill,
            seed.OtherCompoundBillId,
            "This bill is outside my compound.",
            seed.ResidentUserId));

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        (await dbContext.Conversations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateConversationAsync_AllowsOwnLinkedEntity()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedConversationOwnershipWorldAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.CreateConversationAsync(new CreateConversationRequest(
            seed.CompoundId,
            seed.ResidentProfileId,
            PropertyUnitId: null,
            ConversationTopic.Billing,
            ConversationIssueType.BillingMeterReadingIssue,
            ConversationLinkedEntityType.UtilityBill,
            seed.OwnBillId,
            "This is my bill.",
            seed.ResidentUserId));

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.LinkedEntityId.Should().Be(seed.OwnBillId);
        (await dbContext.Conversations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateConversationAsync_RejectsUnitNotOccupiedByResident()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedConversationOwnershipWorldAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.CreateConversationAsync(new CreateConversationRequest(
            seed.CompoundId,
            seed.ResidentProfileId,
            seed.OtherResidentUnitId,
            ConversationTopic.Maintenance,
            ConversationIssueType.MaintenanceWaterLeak,
            ConversationLinkedEntityType.None,
            null,
            "This is not my unit.",
            seed.ResidentUserId));

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        (await dbContext.Conversations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateConversationAsync_AllowsActiveResidentOccupancyUnit()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedConversationOwnershipWorldAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.CreateConversationAsync(new CreateConversationRequest(
            seed.CompoundId,
            seed.ResidentProfileId,
            seed.UnitId,
            ConversationTopic.Maintenance,
            ConversationIssueType.MaintenanceWaterLeak,
            ConversationLinkedEntityType.None,
            null,
            "This is my unit.",
            seed.ResidentUserId));

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.PropertyUnitId.Should().Be(seed.UnitId);
    }

    [Fact]
    public async Task CreateConversationAsync_UnknownLinkedEntityType_FailsClosed()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedConversationOwnershipWorldAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.CreateConversationAsync(new CreateConversationRequest(
            seed.CompoundId,
            seed.ResidentProfileId,
            PropertyUnitId: null,
            ConversationTopic.General,
            ConversationIssueType.GeneralInquiry,
            (ConversationLinkedEntityType)999,
            Guid.NewGuid(),
            "Unknown linked entity should fail.",
            seed.ResidentUserId));

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        (await dbContext.Conversations.CountAsync()).Should().Be(0);
    }

    private static ConversationService CreateService(ApplicationDbContext dbContext)
    {
        return new ConversationService(
            dbContext,
            new ConversationAdvisoryService(),
            new ActivityTimelineService(dbContext));
    }

    private static async Task<ConversationOwnershipSeed> SeedConversationOwnershipWorldAsync(ApplicationDbContext dbContext)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var residentUser = CreateUser("resident-one");
        var otherResidentUser = CreateUser("resident-two");
        var otherCompoundResidentUser = CreateUser("resident-three");

        var compound = CreateCompound("Primary Compound");
        var otherCompound = CreateCompound("Other Compound");

        var unit = CreateUnit(compound.Id, "A-101");
        var otherResidentUnit = CreateUnit(compound.Id, "A-202");
        var otherCompoundUnit = CreateUnit(otherCompound.Id, "B-101");

        var resident = CreateResident(residentUser.Id, compound.Id, "Resident One");
        var otherResident = CreateResident(otherResidentUser.Id, compound.Id, "Resident Two");
        var otherCompoundResident = CreateResident(otherCompoundResidentUser.Id, otherCompound.Id, "Resident Three");

        var occupancy = CreateOccupancy(resident.Id, compound.Id, unit.Id);
        var otherOccupancy = CreateOccupancy(otherResident.Id, compound.Id, otherResidentUnit.Id);
        var otherCompoundOccupancy = CreateOccupancy(otherCompoundResident.Id, otherCompound.Id, otherCompoundUnit.Id);

        var billingCycle = CreateBillingCycle(compound.Id, today);
        var otherCompoundBillingCycle = CreateBillingCycle(otherCompound.Id, today);

        var ownBill = CreateBill(compound.Id, unit.Id, resident.Id, billingCycle.Id, "PASS05-OWN", today);
        var otherResidentBill = CreateBill(compound.Id, otherResidentUnit.Id, otherResident.Id, billingCycle.Id, "PASS05-OTHER-RES", today);
        var otherCompoundBill = CreateBill(otherCompound.Id, otherCompoundUnit.Id, otherCompoundResident.Id, otherCompoundBillingCycle.Id, "PASS05-OTHER-CMP", today);

        dbContext.Users.AddRange(residentUser, otherResidentUser, otherCompoundResidentUser);
        dbContext.Compounds.AddRange(compound, otherCompound);
        dbContext.PropertyUnits.AddRange(unit, otherResidentUnit, otherCompoundUnit);
        dbContext.ResidentProfiles.AddRange(resident, otherResident, otherCompoundResident);
        dbContext.OccupancyRecords.AddRange(occupancy, otherOccupancy, otherCompoundOccupancy);
        dbContext.BillingCycles.AddRange(billingCycle, otherCompoundBillingCycle);
        dbContext.UtilityBills.AddRange(ownBill, otherResidentBill, otherCompoundBill);
        await dbContext.SaveChangesAsync();

        return new ConversationOwnershipSeed(
            compound.Id,
            resident.Id,
            residentUser.Id,
            unit.Id,
            otherResidentUnit.Id,
            ownBill.Id,
            otherResidentBill.Id,
            otherCompoundBill.Id);
    }

    private static ApplicationUser CreateUser(string prefix)
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"{prefix}-{Guid.NewGuid():N}@test.local",
            Email = $"{prefix}-{Guid.NewGuid():N}@test.local",
            FullName = prefix
        };
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
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 120,
            Bedrooms = 2,
            Bathrooms = 2,
            IsActive = true
        };
    }

    private static ResidentProfile CreateResident(Guid userId, Guid compoundId, string fullName)
    {
        return new ResidentProfile
        {
            UserId = userId,
            CompoundId = compoundId,
            FullName = fullName,
            IsActive = true
        };
    }

    private static OccupancyRecord CreateOccupancy(Guid residentProfileId, Guid compoundId, Guid unitId)
    {
        return new OccupancyRecord
        {
            ResidentProfileId = residentProfileId,
            CompoundId = compoundId,
            PropertyUnitId = unitId,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddMonths(-2))
        };
    }

    private static BillingCycle CreateBillingCycle(Guid compoundId, DateOnly today)
    {
        return new BillingCycle
        {
            CompoundId = compoundId,
            Year = today.Year,
            Month = today.Month,
            PeriodStart = new DateOnly(today.Year, today.Month, 1),
            PeriodEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
            DueDate = today.AddDays(15),
            IsClosed = false
        };
    }

    private static UtilityBill CreateBill(
        Guid compoundId,
        Guid unitId,
        Guid residentProfileId,
        Guid billingCycleId,
        string billNumber,
        DateOnly today)
    {
        return new UtilityBill
        {
            CompoundId = compoundId,
            PropertyUnitId = unitId,
            ResidentProfileId = residentProfileId,
            BillingCycleId = billingCycleId,
            BillNumber = $"{billNumber}-{Guid.NewGuid():N}"[..18],
            BillStatus = BillStatus.Unpaid,
            IssueDate = today,
            DueDate = today.AddDays(15),
            SubtotalAmount = 100_000m,
            PreviousBalanceAmount = 0m,
            LateFeeAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 100_000m,
            PaidAmount = 0m
        };
    }

    private sealed record ConversationOwnershipSeed(
        Guid CompoundId,
        Guid ResidentProfileId,
        Guid ResidentUserId,
        Guid UnitId,
        Guid OtherResidentUnitId,
        Guid OwnBillId,
        Guid OtherResidentBillId,
        Guid OtherCompoundBillId);
}
