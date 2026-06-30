using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Visitors;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class VisitorAccessControlPass17Tests
{
    [Fact]
    public async Task Pass17_VerifyAccessCodeAsync_RejectsPendingVisitorPassAsNotGateCleared()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var seed = await SeedVisitorPassAsync(dbContext, VisitorPassStatus.Pending, "VP-20260620-PENDING1");
        var service = new VisitorPassService(dbContext, GuardAccess(guardUserId, seed.CompoundId));

        var result = await service.VerifyAccessCodeAsync(
            guardUserId,
            new VerifyVisitorPassAccessCodeRequest { AccessCode = seed.AccessCode });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Be("Visitor pass is pending admin approval.");
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Pass17_GetGuardAsync_MasksPendingVisitorAccessCode()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var seed = await SeedVisitorPassAsync(dbContext, VisitorPassStatus.Pending, "VP-20260620-PENDING2");
        var service = new VisitorPassService(dbContext, GuardAccess(guardUserId, seed.CompoundId));

        var result = await service.GetGuardAsync(guardUserId, seed.PassId);

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.Status.Should().Be(VisitorPassStatus.Pending);
        result.Value.AccessCode.Should().Be("********");
    }

    [Fact]
    public async Task Pass17_GetGuardAsync_MasksApprovedVisitorAccessCodeInDetailView()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var seed = await SeedVisitorPassAsync(dbContext, VisitorPassStatus.Approved, "VP-20260620-APPROVED1");
        var service = new VisitorPassService(dbContext, GuardAccess(guardUserId, seed.CompoundId));

        var result = await service.GetGuardAsync(guardUserId, seed.PassId);

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.Status.Should().Be(VisitorPassStatus.Approved);
        result.Value.AccessCode.Should().Be("********");
    }

    [Fact]
    public async Task Phase6_CheckInAsync_WrongAccessCodeFailsAndAuditsWithoutSecret()
    {
        await using var dbContext = TestDb.Create();
        var guardUserId = Guid.NewGuid();
        var seed = await SeedVisitorPassAsync(dbContext, VisitorPassStatus.Approved, "VP-20260620-APPROVED2");
        var service = new VisitorPassService(dbContext, GuardAccess(guardUserId, seed.CompoundId));

        var result = await service.CheckInAsync(
            seed.PassId,
            guardUserId,
            new VisitorPassAccessRequest { AccessCode = "WRONG-CODE" });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        var log = dbContext.VisitorAccessLogs.Should().ContainSingle().Subject;
        log.Action.Should().Be(VisitorAccessAction.CredentialFailed);
        log.Notes.Should().NotContain("WRONG-CODE");
    }

    private static FakeCompoundAccessService GuardAccess(Guid guardUserId, Guid compoundId)
    {
        return new FakeCompoundAccessService(
            roleAccess: new Dictionary<(Guid UserId, Guid CompoundId, UserRole Role), bool>
            {
                [(guardUserId, compoundId, UserRole.Guard)] = true
            });
    }

    private static async Task<VisitorSeed> SeedVisitorPassAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        VisitorPassStatus status,
        string accessCode)
    {
        var compound = new Compound
        {
            Name = "Pass 17 Compound",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };
        var unit = new PropertyUnit
        {
            CompoundId = compound.Id,
            UnitNumber = "P17-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied
        };
        var resident = new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            CompoundId = compound.Id,
            FullName = "Pass 17 Resident"
        };
        var now = DateTime.UtcNow;
        var visitorPass = new VisitorPass
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            VisitorName = "Pending Visitor",
            VisitorPhoneNumber = "07700000000",
            VisitReason = "Visit",
            AccessCode = accessCode,
            Status = status,
            ValidFrom = now.AddHours(-1),
            ValidUntil = now.AddHours(3)
        };

        dbContext.AddRange(compound, unit, resident, visitorPass);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return new VisitorSeed(compound.Id, visitorPass.Id, accessCode);
    }

    private sealed record VisitorSeed(Guid CompoundId, Guid PassId, string AccessCode);
}
