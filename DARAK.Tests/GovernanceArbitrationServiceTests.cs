using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Governance;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class GovernanceArbitrationServiceTests
{
    [Fact]
    public async Task CreateCaseAsync_CreatesSuperAdminArbitrationForComplaint()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "ARB-1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Arbitration Resident");
        var complaint = await AddComplaintAsync(dbContext, compound.Id, resident.Id, "Noise complaint");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateCaseAsync(Guid.NewGuid(), new CreateArbitrationCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            SourceType = ArbitrationCaseSourceType.Complaint,
            SourceId = complaint.Id,
            Priority = ArbitrationCasePriority.Critical,
            Title = "Sensitive resident complaint",
            Reason = "Complaint requires SuperAdmin arbitration."
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(ArbitrationCaseStatus.Open);
        result.Value.Priority.Should().Be(ArbitrationCasePriority.Critical);
        result.Value.Events.Should().ContainSingle(item => item.EventType == ArbitrationCaseEventType.Created);
        dbContext.ArbitrationCases.Should().ContainSingle(item => item.SourceId == complaint.Id);
    }

    [Fact]
    public async Task CreateCaseAsync_RejectsDuplicateActiveSourceCase()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "ARB-2");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Duplicate Resident");
        var complaint = await AddComplaintAsync(dbContext, compound.Id, resident.Id, "Duplicate complaint");
        var service = CreateService(dbContext, compound.Id);
        var request = new CreateArbitrationCaseRequest
        {
            CompoundId = compound.Id,
            SourceType = ArbitrationCaseSourceType.Complaint,
            SourceId = complaint.Id,
            Priority = ArbitrationCasePriority.High,
            Title = "Duplicate arbitration",
            Reason = "Same source should not have two active arbitration cases."
        };

        var first = await service.CreateCaseAsync(Guid.NewGuid(), request);
        var second = await service.CreateCaseAsync(Guid.NewGuid(), request);

        first.IsSuccess.Should().BeTrue(first.Message);
        second.Status.Should().Be(ServiceResultStatus.Conflict);
        dbContext.ArbitrationCases.Should().ContainSingle();
    }

    [Fact]
    public async Task IssueFinalDecisionAsync_LocksCaseAndBlocksNewEvents()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "ARB-3");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Decision Resident");
        var dispute = await AddFinancialDisputeAsync(dbContext, compound.Id, resident.Id);
        var service = CreateService(dbContext, compound.Id);
        var created = await service.CreateCaseAsync(Guid.NewGuid(), new CreateArbitrationCaseRequest
        {
            CompoundId = compound.Id,
            SourceType = ArbitrationCaseSourceType.FinancialDispute,
            SourceId = dispute.Id,
            Title = "Financial dispute arbitration",
            Reason = "Financial dispute requires final decision."
        });

        var decided = await service.IssueFinalDecisionAsync(
            created.Value!.Id,
            Guid.NewGuid(),
            new IssueArbitrationFinalDecisionRequest
            {
                Decision = "Final decision: resident objection accepted.",
                DecisionSummary = "The dispute evidence was sufficient."
            });
        var addEvent = await service.AddEventAsync(
            created.Value.Id,
            Guid.NewGuid(),
            new AddArbitrationCaseEventRequest { Message = "Late note should be rejected." });

        decided.IsSuccess.Should().BeTrue(decided.Message);
        decided.Value!.Status.Should().Be(ArbitrationCaseStatus.FinalDecisionIssued);
        decided.Value.Events.Should().Contain(item => item.EventType == ArbitrationCaseEventType.FinalDecisionIssued);
        addEvent.Status.Should().Be(ServiceResultStatus.Conflict);
    }

    [Fact]
    public async Task SearchCasesAsync_AppliesCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "ARB-4A");
        var blocked = await AddCompoundAsync(dbContext, "ARB-4B");
        var allowedResident = await AddResidentAsync(dbContext, allowed.Id, "Allowed Resident");
        var blockedResident = await AddResidentAsync(dbContext, blocked.Id, "Blocked Resident");
        var allowedComplaint = await AddComplaintAsync(dbContext, allowed.Id, allowedResident.Id, "Allowed complaint");
        var blockedComplaint = await AddComplaintAsync(dbContext, blocked.Id, blockedResident.Id, "Blocked complaint");
        var allowedService = CreateService(dbContext, allowed.Id);
        await allowedService.CreateCaseAsync(Guid.NewGuid(), new CreateArbitrationCaseRequest
        {
            CompoundId = allowed.Id,
            SourceType = ArbitrationCaseSourceType.Complaint,
            SourceId = allowedComplaint.Id,
            Title = "Allowed case",
            Reason = "Visible case."
        });
        var superAdminService = CreateSuperAdminService(dbContext, allowed.Id, blocked.Id);
        await superAdminService.CreateCaseAsync(Guid.NewGuid(), new CreateArbitrationCaseRequest
        {
            CompoundId = blocked.Id,
            SourceType = ArbitrationCaseSourceType.Complaint,
            SourceId = blockedComplaint.Id,
            Title = "Blocked case",
            Reason = "Hidden from scoped service."
        });

        var result = await allowedService.SearchCasesAsync(new ArbitrationCaseQueryRequest());

        result.Items.Should().ContainSingle();
        result.Items.Single().CompoundId.Should().Be(allowed.Id);
    }

    private static GovernanceArbitrationService CreateService(ApplicationDbContext dbContext, params Guid[] allowedCompoundIds)
    {
        return new GovernanceArbitrationService(dbContext, new FakeCompoundAccessService(allowedCompoundIds));
    }

    private static GovernanceArbitrationService CreateSuperAdminService(ApplicationDbContext dbContext, params Guid[] compoundIds)
    {
        return new GovernanceArbitrationService(
            dbContext,
            new FakeCompoundAccessService(compoundIds, isSuperAdmin: true));
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

    private static async Task<ResidentProfile> AddResidentAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        string fullName)
    {
        var resident = new ResidentProfile
        {
            CompoundId = compoundId,
            FullName = fullName,
            UserId = Guid.NewGuid(),
            IsActive = true
        };

        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }

    private static async Task<Complaint> AddComplaintAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid residentProfileId,
        string title)
    {
        var complaint = new Complaint
        {
            CompoundId = compoundId,
            ResidentProfileId = residentProfileId,
            Title = title,
            Description = "Complaint description"
        };

        dbContext.Complaints.Add(complaint);
        await dbContext.SaveChangesAsync();
        return complaint;
    }

    private static async Task<FinancialDispute> AddFinancialDisputeAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid residentProfileId)
    {
        var dispute = new FinancialDispute
        {
            CompoundId = compoundId,
            ResidentProfileId = residentProfileId,
            TargetType = FinancialDisputeTargetType.UtilityBill,
            TargetId = Guid.NewGuid(),
            Reason = "Billing dispute",
            ResidentMessage = "The bill should be reviewed.",
            CreatedByUserId = Guid.NewGuid()
        };

        dbContext.FinancialDisputes.Add(dispute);
        await dbContext.SaveChangesAsync();
        return dispute;
    }
}
