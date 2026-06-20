using DARAK.Api.Data;
using DARAK.Api.DTOs.Approvals;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace DARAK.Tests;

public sealed class Phase5COperationsApprovalsGovernanceTests
{
    [Fact]
    public async Task StartWorkOrderAsync_RejectsUnassignedWorkOrder()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P5C-OPS-START");
        var workOrder = new WorkOrder
        {
            CompoundId = compound.Id,
            Title = "Unassigned pump repair",
            Description = "Should be assigned before work starts.",
            Status = WorkOrderStatus.New,
            SourceType = WorkOrderSourceType.Manual
        };
        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();
        var service = new OperationsService(dbContext, new FakeCompoundAccessService([compound.Id]));

        var result = await service.StartWorkOrderAsync(
            workOrder.Id,
            Guid.NewGuid(),
            new StartWorkOrderRequest { Note = "Starting without assignment." });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("assigned");
        workOrder.Status.Should().Be(WorkOrderStatus.New);
    }

    [Fact]
    public async Task ScheduleWorkOrderAsync_RejectsPastScheduledTime()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P5C-OPS-SCHEDULE");
        var workOrder = new WorkOrder
        {
            CompoundId = compound.Id,
            Title = "Past schedule check",
            Description = "Past operational schedule should not be accepted.",
            Status = WorkOrderStatus.New,
            SourceType = WorkOrderSourceType.Manual,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();
        var service = new OperationsService(dbContext, new FakeCompoundAccessService([compound.Id]));

        var result = await service.ScheduleWorkOrderAsync(
            workOrder.Id,
            Guid.NewGuid(),
            new ScheduleWorkOrderRequest
            {
                ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-30),
                Note = "Past schedule."
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("past");
        workOrder.Status.Should().Be(WorkOrderStatus.New);
    }

    [Fact]
    public async Task AddCostItemAsync_RejectsFinalizedWorkOrder()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P5C-OPS-COST");
        var workOrder = new WorkOrder
        {
            CompoundId = compound.Id,
            Title = "Completed repair",
            Description = "No cost item should be added after finalization.",
            Status = WorkOrderStatus.Completed,
            SourceType = WorkOrderSourceType.Manual,
            StartedAtUtc = DateTime.UtcNow.AddHours(-2),
            CompletedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();
        var service = new OperationsService(dbContext, new FakeCompoundAccessService([compound.Id]));

        var result = await service.AddCostItemAsync(
            workOrder.Id,
            new AddWorkOrderCostItemRequest
            {
                Description = "Late labor charge",
                CostType = WorkOrderCostType.Labor,
                Amount = 25000m
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        dbContext.WorkOrderCostItems.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkExecutedAsync_RejectsRequesterExecutingOwnApprovedRequest()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "P5C-APP-REQ");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "p5c-requester@darak.test", UserRole.Accountant);
        var approval = await AddApprovalAsync(
            dbContext,
            compound.Id,
            requester.Id,
            ApprovalStatus.Approved,
            ApprovalExecutionStatus.ReadyForExecution);
        var service = CreateApprovalService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.MarkExecutedAsync(
            requester.Id,
            approval.Id,
            new MarkApprovalExecutedRequest { Notes = "Requester self-execution." });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        approval.Status.Should().Be(ApprovalStatus.Approved);
        approval.ExecutionStatus.Should().Be(ApprovalExecutionStatus.ReadyForExecution);
    }

    [Fact]
    public async Task MarkExecutedAsync_RejectsApproverExecutingSameRequest()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "P5C-APP-APR");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "p5c-requester2@darak.test", UserRole.Accountant);
        var approver = await CreateUserWithRoleAsync(identity.UserManager, "p5c-approver@darak.test", UserRole.CompoundAdmin);
        var approval = await AddApprovalAsync(
            dbContext,
            compound.Id,
            requester.Id,
            ApprovalStatus.Approved,
            ApprovalExecutionStatus.ReadyForExecution);
        approval.LastDecisionByUserId = approver.Id;
        await dbContext.SaveChangesAsync();
        var service = CreateApprovalService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.MarkExecutedAsync(
            approver.Id,
            approval.Id,
            new MarkApprovalExecutedRequest { Notes = "Same approver execution." });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        approval.Status.Should().Be(ApprovalStatus.Approved);
        approval.ExecutionStatus.Should().Be(ApprovalExecutionStatus.ReadyForExecution);
    }

    private static async Task<Compound> AddCompoundAsync(
        ApplicationDbContext dbContext,
        string code)
    {
        var compound = new Compound
        {
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Karrada"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<TestIdentity> CreateIdentityAsync(ApplicationDbContext dbContext)
    {
        var userManager = IdentityTestHelpers.CreateUserManager(dbContext);
        var roleManager = IdentityTestHelpers.CreateRoleManager(dbContext);
        await IdentityTestHelpers.SeedRolesAsync(roleManager);
        return new TestIdentity(userManager, roleManager);
    }

    private static async Task<ApplicationUser> CreateUserWithRoleAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        UserRole role)
    {
        var user = await IdentityTestHelpers.CreateUserAsync(userManager, email);
        var result = await userManager.AddToRoleAsync(user, role.ToString());
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to assign role {role} to {email}.");
        }

        return user;
    }

    private static async Task<ApprovalRequest> AddApprovalAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid requesterUserId,
        ApprovalStatus status,
        ApprovalExecutionStatus executionStatus)
    {
        var approval = new ApprovalRequest
        {
            CompoundId = compoundId,
            RequestedByUserId = requesterUserId,
            ActionType = ApprovalActionType.OtherSensitiveAdminOperation,
            EntityType = ApprovalEntityType.None,
            Status = status,
            Priority = ApprovalPriority.Normal,
            ExecutionStatus = executionStatus,
            Reason = "Phase 5C approval governance test.",
            CreatedAtUtc = DateTime.UtcNow,
            DueAtUtc = DateTime.UtcNow.AddHours(24)
        };

        dbContext.ApprovalRequests.Add(approval);
        await dbContext.SaveChangesAsync();
        return approval;
    }

    private static ApprovalService CreateApprovalService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        Guid[] allowedCompoundIds)
    {
        return new ApprovalService(
            dbContext,
            new FakeCompoundAccessService(allowedCompoundIds),
            userManager,
            new AuditLogService(
                dbContext,
                new FakeCompoundAccessService(allowedCompoundIds),
                new HttpContextAccessor()));
    }

    private sealed record TestIdentity(
        UserManager<ApplicationUser> UserManager,
        RoleManager<IdentityRole<Guid>> RoleManager);
}
