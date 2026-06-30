using DARAK.Api.Data;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class AccessControlPack5ProTests
{
    [Fact]
    public async Task GetGateSituationReportAsync_FlagsCriticalAccessExposure()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAccessScenarioAsync(dbContext);
        var service = CreateService(dbContext, seed.CompoundId);

        var report = await service.GetGateSituationReportAsync(null);

        report.IsSuccess.Should().BeTrue(report.Message);
        report.Value!.VisitorOverstayingCount.Should().Be(1);
        report.Value.ContractorOverstayingCount.Should().Be(1);
        report.Value.ExpiredActiveCredentialCount.Should().Be(1);
        report.Value.CriticalActionCount.Should().BeGreaterThan(0);
        report.Value.TopSecurityActions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetVisitorVerificationBoardAsync_ReturnsActionRequiredVisitorItems()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAccessScenarioAsync(dbContext);
        var service = CreateService(dbContext, seed.CompoundId);

        var board = await service.GetVisitorVerificationBoardAsync(new VisitorVerificationBoardQueryRequest
        {
            OnlyActionRequired = true,
            PageSize = 50
        });

        board.TotalCount.Should().BeGreaterThan(0);
        board.Items.Should().Contain(item => item.VisitorPassId == seed.VisitorPassId
            && item.VerificationStatus == "Overstaying"
            && item.RiskLevel == "Critical");
    }

    [Fact]
    public async Task GetGuardShiftHandoverReportAsync_SummarizesRecentAccessAndOpenActions()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAccessScenarioAsync(dbContext);
        var service = CreateService(dbContext, seed.CompoundId);

        var handover = await service.GetGuardShiftHandoverReportAsync(null);

        handover.IsSuccess.Should().BeTrue(handover.Message);
        handover.Value!.OpenVisitorOnSiteCount.Should().Be(1);
        handover.Value.OpenContractorOnSiteCount.Should().Be(1);
        handover.Value.CriticalOpenActionCount.Should().BeGreaterThan(0);
        handover.Value.RecentAccessEvents.Should().NotBeEmpty();
    }

    private static AccessControlOperationsService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new AccessControlOperationsService(dbContext, new FakeCompoundAccessService([compoundId]));
    }

    private static async Task<AccessSeed> SeedAccessScenarioAsync(ApplicationDbContext dbContext)
    {
        var now = DateTime.UtcNow;
        var compound = new Compound
        {
            Name = "Pack5 Compound",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada",
            Address = "Baghdad"
        };
        var unit = new PropertyUnit
        {
            CompoundId = compound.Id,
            UnitNumber = "P5-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied
        };
        var resident = new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            CompoundId = compound.Id,
            FullName = "Resident Pack5"
        };
        var visitorPass = new VisitorPass
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            VisitorName = "Overstaying Visitor",
            VisitorPhoneNumber = "07700000000",
            VisitReason = "Family visit",
            AccessCode = "P5-VISITOR",
            Status = VisitorPassStatus.CheckedIn,
            ValidFrom = now.AddHours(-4),
            ValidUntil = now.AddMinutes(-30),
            CheckedInAt = now.AddHours(-3)
        };
        visitorPass.AccessLogs.Add(new VisitorAccessLog
        {
            VisitorPassId = visitorPass.Id,
            Action = VisitorAccessAction.CheckIn,
            Notes = "Visitor entered.",
            CreatedAt = now.AddHours(-3)
        });
        var vendor = new ServiceVendor
        {
            CompoundId = compound.Id,
            Name = "Pack5 Contractor",
            PhoneNumber = "07700000001",
            ServiceType = VendorServiceType.Maintenance,
            Status = VendorStatus.Active
        };
        var contractorPermit = new ContractorWorkPermit
        {
            CompoundId = compound.Id,
            VendorId = vendor.Id,
            Purpose = "Gate repair",
            WorkArea = "Main gate",
            RiskLevel = ContractorWorkPermitRiskLevel.High,
            Status = ContractorWorkPermitStatus.CheckedIn,
            AllowedFromUtc = now.AddHours(-3),
            AllowedUntilUtc = now.AddMinutes(-10),
            RequiresEscort = true,
            CheckedInAtUtc = now.AddHours(-2),
            GuardNotes = "Contractor entered for repair."
        };
        var expiredCredential = new AccessCredential
        {
            CompoundId = compound.Id,
            CredentialType = AccessCredentialType.ContractorPass,
            OwnerType = AccessCredentialOwnerType.Contractor,
            OwnerDisplayName = "Expired contractor credential",
            CredentialCode = "P5-CRED",
            Status = AccessCredentialStatus.Active,
            ValidFromUtc = now.AddDays(-1),
            ValidUntilUtc = now.AddMinutes(-5)
        };

        dbContext.AddRange(compound, unit, resident, visitorPass, vendor, contractorPermit, expiredCredential);
        await dbContext.SaveChangesAsync();

        return new AccessSeed(compound.Id, visitorPass.Id, contractorPermit.Id, expiredCredential.Id);
    }

    private sealed record AccessSeed(
        Guid CompoundId,
        Guid VisitorPassId,
        Guid ContractorWorkPermitId,
        Guid AccessCredentialId);
}
