using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Visitors;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class VisitorPassServiceAccessTests
{
    [Fact]
    public async Task SearchTodayForGuardAsync_ReturnsOnlyPassesInAssignedCompounds()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var seed = await SeedVisitorPassesInTwoCompoundsAsync(dbContext);
        var access = new FakeCompoundAccessService(
            roleAccess: new Dictionary<(Guid UserId, Guid CompoundId, UserRole Role), bool>
            {
                [(guardUserId, seed.AllowedCompoundId, UserRole.Guard)] = true,
                [(guardUserId, seed.BlockedCompoundId, UserRole.Guard)] = false
            });
        var service = new VisitorPassService(dbContext, access);

        var result = await service.SearchTodayForGuardAsync(guardUserId, new VisitorPassSearchQuery());

        result.TotalCount.Should().Be(1);
        result.Items.Single().CompoundId.Should().Be(seed.AllowedCompoundId);
    }

    [Fact]
    public async Task SearchTodayForGuardAsync_ReturnsEmptyForMissingGuardIdentity()
    {
        await using var dbContext = TestDb.Create();
        await SeedVisitorPassesInTwoCompoundsAsync(dbContext);
        var service = new VisitorPassService(dbContext, new FakeCompoundAccessService());

        var result = await service.SearchTodayForGuardAsync(null, new VisitorPassSearchQuery());

        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetGuardAsync_ReturnsNotFoundForPassInUnassignedCompound()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var seed = await SeedVisitorPassesInTwoCompoundsAsync(dbContext);
        var service = new VisitorPassService(
            dbContext,
            GuardAccess(guardUserId, seed.AllowedCompoundId));

        var result = await service.GetGuardAsync(guardUserId, seed.BlockedPassId);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task CheckInAsync_ReturnsNotFoundForPassInUnassignedCompound()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var seed = await SeedVisitorPassesInTwoCompoundsAsync(dbContext);
        var service = new VisitorPassService(
            dbContext,
            GuardAccess(guardUserId, seed.AllowedCompoundId));

        var result = await service.CheckInAsync(
            seed.BlockedPassId,
            guardUserId,
            new VisitorPassAccessRequest { AccessCode = "non-empty-code" });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task DenyAsync_ReturnsNotFoundForPassInUnassignedCompound()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var seed = await SeedVisitorPassesInTwoCompoundsAsync(dbContext);
        var service = new VisitorPassService(
            dbContext,
            GuardAccess(guardUserId, seed.AllowedCompoundId));

        var result = await service.DenyAsync(
            seed.BlockedPassId,
            new DenyVisitorPassRequest { Reason = "No entry" },
            guardUserId);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task GetGuardAsync_AllowsAssignedGuardToViewPass()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var seed = await SeedVisitorPassesInTwoCompoundsAsync(dbContext);
        var service = new VisitorPassService(
            dbContext,
            GuardAccess(guardUserId, seed.AllowedCompoundId));

        var result = await service.GetGuardAsync(guardUserId, seed.AllowedPassId);

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.CompoundId.Should().Be(seed.AllowedCompoundId);
    }

    [Fact]
    public async Task SearchAdminAsync_MasksAccessCodeInListResults()
    {
        await using var dbContext = TestDb.Create();
        await SeedVisitorPassesInTwoCompoundsAsync(dbContext);
        var service = new VisitorPassService(dbContext);

        var result = await service.SearchAdminAsync(new VisitorPassSearchQuery());

        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(item => item.AccessCode == "********");
    }

    [Fact]
    public async Task GetAdminAsync_ReturnsFullAccessCodeForDetailView()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedVisitorPassesInTwoCompoundsAsync(dbContext);
        var expectedCode = await dbContext.VisitorPasses
            .Where(pass => pass.Id == seed.AllowedPassId)
            .Select(pass => pass.AccessCode)
            .SingleAsync();
        var service = new VisitorPassService(dbContext);

        var result = await service.GetAdminAsync(seed.AllowedPassId);

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.AccessCode.Should().Be(expectedCode);
        result.Value.AccessCode.Should().NotBe("********");
    }

    private static FakeCompoundAccessService GuardAccess(Guid guardUserId, Guid compoundId)
    {
        return new FakeCompoundAccessService(
            roleAccess: new Dictionary<(Guid UserId, Guid CompoundId, UserRole Role), bool>
            {
                [(guardUserId, compoundId, UserRole.Guard)] = true
            });
    }

    private static async Task<VisitorSeed> SeedVisitorPassesInTwoCompoundsAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        var now = DateTime.UtcNow;
        var allowed = CreateCompound("Allowed");
        var blocked = CreateCompound("Blocked");
        var allowedUnit = CreateUnit(allowed.Id, "A-101");
        var blockedUnit = CreateUnit(blocked.Id, "B-101");
        var allowedResident = CreateResident(allowed.Id);
        var blockedResident = CreateResident(blocked.Id);
        var allowedPass = CreatePass(
            allowed.Id,
            allowedUnit.Id,
            allowedResident.Id,
            "Allowed Visitor",
            now);
        var blockedPass = CreatePass(
            blocked.Id,
            blockedUnit.Id,
            blockedResident.Id,
            "Blocked Visitor",
            now);

        dbContext.AddRange(
            allowed,
            blocked,
            allowedUnit,
            blockedUnit,
            allowedResident,
            blockedResident,
            allowedPass,
            blockedPass);
        await dbContext.SaveChangesAsync();

        return new VisitorSeed(allowed.Id, blocked.Id, allowedPass.Id, blockedPass.Id);
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

    private static ResidentProfile CreateResident(Guid compoundId)
    {
        return new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            CompoundId = compoundId,
            FullName = "Resident"
        };
    }

    private static VisitorPass CreatePass(
        Guid compoundId,
        Guid propertyUnitId,
        Guid residentProfileId,
        string visitorName,
        DateTime now)
    {
        return new VisitorPass
        {
            CompoundId = compoundId,
            PropertyUnitId = propertyUnitId,
            ResidentProfileId = residentProfileId,
            VisitorName = visitorName,
            VisitorPhoneNumber = "07700000000",
            VisitReason = "Visit",
            AccessCode = Guid.NewGuid().ToString("N")[..8],
            Status = VisitorPassStatus.Approved,
            ValidFrom = now.AddHours(-1),
            ValidUntil = now.AddHours(3)
        };
    }

    private sealed record VisitorSeed(
        Guid AllowedCompoundId,
        Guid BlockedCompoundId,
        Guid AllowedPassId,
        Guid BlockedPassId);
}
