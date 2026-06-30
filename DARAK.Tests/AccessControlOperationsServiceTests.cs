using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class AccessControlOperationsServiceTests
{
    [Fact]
    public async Task CreateContractorWorkPermitAsync_CreatesPendingPermitForActiveVendor()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "AC-1");
        var vendor = await AddVendorAsync(dbContext, compound.Id, "Elevator Contractors", VendorStatus.Active);
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateContractorWorkPermitAsync(
            Guid.NewGuid(),
            new CreateContractorWorkPermitRequest
            {
                CompoundId = compound.Id,
                VendorId = vendor.Id,
                Purpose = "Monthly elevator inspection",
                WorkArea = "Building A elevator room",
                AllowedFromUtc = DateTime.UtcNow.AddMinutes(-5),
                AllowedUntilUtc = DateTime.UtcNow.AddHours(2),
                RequiresEscort = true,
                RiskLevel = ContractorWorkPermitRiskLevel.Medium
            });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(ContractorWorkPermitStatus.PendingApproval);
        result.Value.RequiresEscort.Should().BeTrue();
        dbContext.ContractorWorkPermits.Should().ContainSingle(item => item.CompoundId == compound.Id && item.VendorId == vendor.Id);
    }

    [Fact]
    public async Task CreateContractorWorkPermitAsync_RejectsSuspendedVendor()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "AC-2");
        var vendor = await AddVendorAsync(dbContext, compound.Id, "Suspended Contractors", VendorStatus.Suspended);
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateContractorWorkPermitAsync(
            Guid.NewGuid(),
            new CreateContractorWorkPermitRequest
            {
                CompoundId = compound.Id,
                VendorId = vendor.Id,
                Purpose = "Pump work",
                WorkArea = "Pump room",
                AllowedFromUtc = DateTime.UtcNow,
                AllowedUntilUtc = DateTime.UtcNow.AddHours(1)
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task GuardCheckInAndOutContractorWorkPermitAsync_RequiresApprovedPermitAndGuardScope()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "AC-3");
        var vendor = await AddVendorAsync(dbContext, compound.Id, "Gate Contractors", VendorStatus.Active);
        var guardUserId = Guid.NewGuid();
        var roleAccess = new Dictionary<(Guid UserId, Guid CompoundId, UserRole Role), bool>
        {
            [(guardUserId, compound.Id, UserRole.Guard)] = true
        };
        var service = new AccessControlOperationsService(
            dbContext,
            new FakeCompoundAccessService([compound.Id], roleAccess));
        var created = await service.CreateContractorWorkPermitAsync(
            Guid.NewGuid(),
            new CreateContractorWorkPermitRequest
            {
                CompoundId = compound.Id,
                VendorId = vendor.Id,
                Purpose = "Gate motor repair",
                WorkArea = "Main gate",
                AllowedFromUtc = DateTime.UtcNow.AddMinutes(-10),
                AllowedUntilUtc = DateTime.UtcNow.AddHours(2)
            });
        var approved = await service.ApproveContractorWorkPermitAsync(
            created.Value!.Id,
            Guid.NewGuid(),
            new ContractorPermitDecisionRequest { Notes = "Approved for gate work." });
        var credential = await service.CreateAccessCredentialAsync(new CreateAccessCredentialRequest
        {
            CompoundId = compound.Id,
            CredentialType = AccessCredentialType.ContractorPass,
            OwnerType = AccessCredentialOwnerType.Contractor,
            OwnerDisplayName = "Gate Contractors",
            ValidFromUtc = DateTime.UtcNow.AddMinutes(-5),
            ValidUntilUtc = DateTime.UtcNow.AddHours(2),
            SourceContractorWorkPermitId = approved.Value!.Id
        });

        var checkedIn = await service.GuardCheckInContractorWorkPermitAsync(
            approved.Value!.Id,
            guardUserId,
            new GuardContractorPermitAccessRequest
            {
                AccessCode = credential.Value!.CredentialCode,
                Notes = "Contractor arrived."
            });
        var checkedOut = await service.GuardCheckOutContractorWorkPermitAsync(
            approved.Value.Id,
            guardUserId,
            new GuardContractorPermitAccessRequest { Notes = "Contractor left." });

        checkedIn.IsSuccess.Should().BeTrue(checkedIn.Message);
        checkedIn.Value!.Status.Should().Be(ContractorWorkPermitStatus.CheckedIn);
        checkedOut.IsSuccess.Should().BeTrue(checkedOut.Message);
        checkedOut.Value!.Status.Should().Be(ContractorWorkPermitStatus.CheckedOut);
        dbContext.ContractorAccessLogs.Should().HaveCount(2);
        dbContext.ContractorAccessLogs.Select(log => log.Action).Should().ContainInOrder(
            ContractorAccessAction.CheckIn,
            ContractorAccessAction.CheckOut);
    }

    [Fact]
    public async Task CreateAndRevokeAccessCredentialAsync_TracksCredentialLifecycle()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "AC-4");
        var service = CreateService(dbContext, compound.Id);

        var created = await service.CreateAccessCredentialAsync(new CreateAccessCredentialRequest
        {
            CompoundId = compound.Id,
            CredentialType = AccessCredentialType.ContractorPass,
            OwnerType = AccessCredentialOwnerType.Contractor,
            OwnerDisplayName = "Temporary contractor",
            ValidFromUtc = DateTime.UtcNow.AddMinutes(-5),
            ValidUntilUtc = DateTime.UtcNow.AddHours(1)
        });
        var revoked = await service.RevokeAccessCredentialAsync(
            created.Value!.Id,
            Guid.NewGuid(),
            new RevokeAccessCredentialRequest { Reason = "Work completed." });

        created.IsSuccess.Should().BeTrue(created.Message);
        created.Value!.CredentialCode.Should().StartWith("AC-");
        dbContext.AccessCredentials.Single().CredentialCode.Should().StartWith("AC2$");
        revoked.IsSuccess.Should().BeTrue(revoked.Message);
        revoked.Value!.Status.Should().Be(AccessCredentialStatus.Revoked);
        revoked.Value.CredentialCode.Should().Be("********");
    }

    [Fact]
    public async Task Phase6_ContractorCheckInAsync_WrongCredentialFailsAndAuditsWithoutSecret()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "AC-P6-WRONG");
        var vendor = await AddVendorAsync(dbContext, compound.Id, "Wrong Code Contractors", VendorStatus.Active);
        var guardUserId = Guid.NewGuid();
        var roleAccess = new Dictionary<(Guid UserId, Guid CompoundId, UserRole Role), bool>
        {
            [(guardUserId, compound.Id, UserRole.Guard)] = true
        };
        var service = new AccessControlOperationsService(
            dbContext,
            new FakeCompoundAccessService([compound.Id], roleAccess));
        var created = await service.CreateContractorWorkPermitAsync(Guid.NewGuid(), new CreateContractorWorkPermitRequest
        {
            CompoundId = compound.Id,
            VendorId = vendor.Id,
            Purpose = "Credential check",
            WorkArea = "Service gate",
            AllowedFromUtc = DateTime.UtcNow.AddMinutes(-5),
            AllowedUntilUtc = DateTime.UtcNow.AddHours(1)
        });
        var approved = await service.ApproveContractorWorkPermitAsync(
            created.Value!.Id,
            Guid.NewGuid(),
            new ContractorPermitDecisionRequest());
        await service.CreateAccessCredentialAsync(new CreateAccessCredentialRequest
        {
            CompoundId = compound.Id,
            CredentialType = AccessCredentialType.ContractorPass,
            OwnerType = AccessCredentialOwnerType.Contractor,
            OwnerDisplayName = "Wrong Code Contractors",
            ValidFromUtc = DateTime.UtcNow.AddMinutes(-5),
            ValidUntilUtc = DateTime.UtcNow.AddHours(1),
            SourceContractorWorkPermitId = approved.Value!.Id
        });

        var result = await service.GuardCheckInContractorWorkPermitAsync(
            approved.Value.Id,
            guardUserId,
            new GuardContractorPermitAccessRequest { AccessCode = "WRONG-CONTRACTOR-CODE" });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        var log = dbContext.ContractorAccessLogs.Should().ContainSingle().Subject;
        log.Action.Should().Be(ContractorAccessAction.CredentialFailed);
        log.Notes.Should().NotContain("WRONG-CONTRACTOR-CODE");
    }

    [Fact]
    public async Task SummaryAsync_RespectsCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "AC-5A");
        var denied = await AddCompoundAsync(dbContext, "AC-5B");
        var allowedVendor = await AddVendorAsync(dbContext, allowed.Id, "Allowed Vendor", VendorStatus.Active);
        var deniedVendor = await AddVendorAsync(dbContext, denied.Id, "Denied Vendor", VendorStatus.Active);
        var service = CreateService(dbContext, allowed.Id);

        await service.CreateContractorWorkPermitAsync(Guid.NewGuid(), new CreateContractorWorkPermitRequest
        {
            CompoundId = allowed.Id,
            VendorId = allowedVendor.Id,
            Purpose = "Allowed permit",
            WorkArea = "Allowed area",
            AllowedFromUtc = DateTime.UtcNow,
            AllowedUntilUtc = DateTime.UtcNow.AddHours(2)
        });

        dbContext.ContractorWorkPermits.Add(new ContractorWorkPermit
        {
            CompoundId = denied.Id,
            VendorId = deniedVendor.Id,
            Purpose = "Denied permit",
            WorkArea = "Denied area",
            AllowedFromUtc = DateTime.UtcNow,
            AllowedUntilUtc = DateTime.UtcNow.AddHours(2)
        });
        await dbContext.SaveChangesAsync();

        var summary = await service.GetSummaryAsync(null);

        summary.IsSuccess.Should().BeTrue(summary.Message);
        summary.Value!.PendingContractorPermitCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateContractorWorkPermitAsync_RejectsVendorFromDifferentCompound()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "AC-VS-A");
        var denied = await AddCompoundAsync(dbContext, "AC-VS-D");
        var vendor = await AddVendorAsync(dbContext, denied.Id, "Cross Compound Contractors", VendorStatus.Active);
        var service = CreateService(dbContext, allowed.Id);

        var result = await service.CreateContractorWorkPermitAsync(
            Guid.NewGuid(),
            new CreateContractorWorkPermitRequest
            {
                CompoundId = allowed.Id,
                VendorId = vendor.Id,
                Purpose = "Cross-compound work",
                WorkArea = "Pump room",
                AllowedFromUtc = DateTime.UtcNow,
                AllowedUntilUtc = DateTime.UtcNow.AddHours(1)
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("selected compound");
    }

    private static AccessControlOperationsService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new AccessControlOperationsService(dbContext, new FakeCompoundAccessService([compoundId]));
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

    private static async Task<ServiceVendor> AddVendorAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        string name,
        VendorStatus status)
    {
        var vendor = new ServiceVendor
        {
            CompoundId = compoundId,
            Name = name,
            PhoneNumber = "07700000000",
            ServiceType = VendorServiceType.Maintenance,
            Status = status
        };

        dbContext.ServiceVendors.Add(vendor);
        await dbContext.SaveChangesAsync();
        return vendor;
    }
}
