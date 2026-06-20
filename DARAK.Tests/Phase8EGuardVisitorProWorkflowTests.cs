using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Visitors;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class Phase8EGuardVisitorProWorkflowTests
{
    [Fact]
    public async Task VerifyAccessCodeAsync_ReturnsPassForAssignedGuardAndValidCode()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var seed = await SeedVisitorPassesInTwoCompoundsAsync(dbContext);
        var service = new VisitorPassService(dbContext, GuardAccess(guardUserId, seed.AllowedCompoundId));

        var result = await service.VerifyAccessCodeAsync(
            guardUserId,
            new VerifyVisitorPassAccessCodeRequest { AccessCode = seed.AllowedAccessCode });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.Id.Should().Be(seed.AllowedPassId);
        result.Value.AccessCode.Should().Be(seed.AllowedAccessCode);
    }

    [Fact]
    public async Task VerifyAccessCodeAsync_ReturnsNotFoundForPassOutsideGuardCompound()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var seed = await SeedVisitorPassesInTwoCompoundsAsync(dbContext);
        var service = new VisitorPassService(dbContext, GuardAccess(guardUserId, seed.AllowedCompoundId));

        var result = await service.VerifyAccessCodeAsync(
            guardUserId,
            new VerifyVisitorPassAccessCodeRequest { AccessCode = seed.BlockedAccessCode });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task VerifyAccessCodeAsync_RejectsEmptyCode()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var service = new VisitorPassService(dbContext, new FakeCompoundAccessService());

        var result = await service.VerifyAccessCodeAsync(
            guardUserId,
            new VerifyVisitorPassAccessCodeRequest { AccessCode = "   " });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task CheckInAsync_RejectsMissingAccessCode()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var seed = await SeedVisitorPassesInTwoCompoundsAsync(dbContext);
        var service = new VisitorPassService(dbContext, GuardAccess(guardUserId, seed.AllowedCompoundId));

        var result = await service.CheckInAsync(
            seed.AllowedPassId,
            guardUserId,
            new VisitorPassAccessRequest
            {
                AccessCode = "   ",
                Notes = "No code provided."
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Be("Visitor access code is required.");
        var visitorPass = await dbContext.VisitorPasses.SingleAsync(pass => pass.Id == seed.AllowedPassId);
        visitorPass.Status.Should().Be(VisitorPassStatus.Approved);
        visitorPass.CheckedInAt.Should().BeNull();
    }

    [Fact]
    public async Task CheckInAsync_RejectsMismatchedAccessCode()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var seed = await SeedVisitorPassesInTwoCompoundsAsync(dbContext);
        var service = new VisitorPassService(dbContext, GuardAccess(guardUserId, seed.AllowedCompoundId));

        var result = await service.CheckInAsync(
            seed.AllowedPassId,
            guardUserId,
            new VisitorPassAccessRequest
            {
                AccessCode = "WRONG-CODE",
                Notes = "Gate verification failed."
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        var visitorPass = await dbContext.VisitorPasses.SingleAsync(pass => pass.Id == seed.AllowedPassId);
        visitorPass.Status.Should().Be(VisitorPassStatus.Approved);
        visitorPass.CheckedInAt.Should().BeNull();
    }

    [Fact]
    public async Task CheckInAsync_WithMatchingAccessCode_PassesAccessCodeValidation()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var seed = await SeedVisitorPassesInTwoCompoundsAsync(dbContext);
        var service = new VisitorPassService(dbContext, GuardAccess(guardUserId, seed.AllowedCompoundId));

        var result = await service.CheckInAsync(
            seed.AllowedPassId,
            guardUserId,
            new VisitorPassAccessRequest
            {
                AccessCode = seed.AllowedAccessCode.ToLowerInvariant(),
                Notes = "Verified at main gate."
            });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.Status.Should().Be(VisitorPassStatus.CheckedIn);
        result.Value.CheckedInAt.Should().NotBeNull();

        var visitorPass = await dbContext.VisitorPasses.SingleAsync(pass => pass.Id == seed.AllowedPassId);
        visitorPass.Status.Should().Be(VisitorPassStatus.CheckedIn);
        visitorPass.CheckedInAt.Should().NotBeNull();
        dbContext.VisitorAccessLogs.Should().Contain(log =>
            log.VisitorPassId == seed.AllowedPassId && log.Action == VisitorAccessAction.CheckIn);
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
        const string allowedAccessCode = "VP-20260615-ALLOWED1";
        const string blockedAccessCode = "VP-20260615-BLOCKED1";
        var allowedPass = CreatePass(
            allowed.Id,
            allowedUnit.Id,
            allowedResident.Id,
            "Allowed Visitor",
            allowedAccessCode,
            now);
        var blockedPass = CreatePass(
            blocked.Id,
            blockedUnit.Id,
            blockedResident.Id,
            "Blocked Visitor",
            blockedAccessCode,
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
        dbContext.ChangeTracker.Clear();

        return new VisitorSeed(
            allowed.Id,
            blocked.Id,
            allowedPass.Id,
            blockedPass.Id,
            allowedAccessCode,
            blockedAccessCode);
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
        string accessCode,
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
            AccessCode = accessCode,
            Status = VisitorPassStatus.Approved,
            ValidFrom = now.AddHours(-1),
            ValidUntil = now.AddHours(3)
        };
    }

    private sealed record VisitorSeed(
        Guid AllowedCompoundId,
        Guid BlockedCompoundId,
        Guid AllowedPassId,
        Guid BlockedPassId,
        string AllowedAccessCode,
        string BlockedAccessCode);
}




