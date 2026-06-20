using DARAK.Api.Data;
using DARAK.Api.DTOs.Approvals;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace DARAK.Tests;

public sealed class ApprovalServiceTests
{
    [Fact]
    public async Task CreateRequestAsync_SucceedsForScopedCompoundAdminAndCreatesTimelineAndNotification()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D1");
        var payment = await AddPaymentAsync(dbContext, compound.Id);
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin1@darak.test", UserRole.CompoundAdmin);
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.CreateRequestAsync(
            admin.Id,
            new CreateApprovalRequestRequest
            {
                CompoundId = compound.Id,
                ActionType = ApprovalActionType.RefundPayment,
                EntityType = ApprovalEntityType.Payment,
                EntityId = payment.Id,
                Priority = ApprovalPriority.High,
                Reason = "Resident was charged twice.",
                RequestPayloadJson = "{\"amount\":10000}"
            });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(ApprovalStatus.Pending);
        result.Value.Priority.Should().Be(ApprovalPriority.High);
        result.Value.ExecutionStatus.Should().Be(ApprovalExecutionStatus.NotReady);

        dbContext.ApprovalRequests.Should().ContainSingle();
        dbContext.ActivityEvents.Should().ContainSingle(activity =>
            activity.EventType == ActivityEventType.ApprovalRequested
            && activity.EntityType == ActivityEntityType.ApprovalRequest);
        dbContext.NotificationOutboxes.Should().ContainSingle(notification =>
            notification.EventType == NotificationEventType.ApprovalRequested
            && notification.RelatedEntityType == NotificationRelatedEntityType.ApprovalRequest);
    }

    [Fact]
    public async Task CreateRequestAsync_RejectsResidentUsers()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D2");
        var resident = await CreateUserWithRoleAsync(identity.UserManager, "resident@darak.test", UserRole.Resident);
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.CreateRequestAsync(
            resident.Id,
            new CreateApprovalRequestRequest
            {
                CompoundId = compound.Id,
                ActionType = ApprovalActionType.OtherSensitiveAdminOperation,
                EntityType = ApprovalEntityType.None,
                Reason = "Should fail."
            });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        dbContext.ApprovalRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveAsync_RejectsGuardUsers()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D3");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "requester@darak.test", UserRole.Accountant);
        var guard = await CreateUserWithRoleAsync(identity.UserManager, "guard@darak.test", UserRole.Guard);
        var approval = await AddApprovalAsync(dbContext, compound.Id, requester.Id);
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.ApproveAsync(
            guard.Id,
            approval.Id,
            new ApprovalDecisionRequest { Reason = "No." });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        approval.Status.Should().Be(ApprovalStatus.Pending);
    }

    [Fact]
    public async Task ApproveAsync_ReturnsNotFoundForCompoundAdminOutsideAssignedCompound()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var allowedCompound = await AddCompoundAsync(dbContext, "D4A");
        var blockedCompound = await AddCompoundAsync(dbContext, "D4B");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "requester4@darak.test", UserRole.Accountant);
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin4@darak.test", UserRole.CompoundAdmin);
        var approval = await AddApprovalAsync(dbContext, blockedCompound.Id, requester.Id);
        var service = CreateService(dbContext, identity.UserManager, [allowedCompound.Id]);

        var result = await service.ApproveAsync(
            admin.Id,
            approval.Id,
            new ApprovalDecisionRequest { Reason = "Outside scope." });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        approval.Status.Should().Be(ApprovalStatus.Pending);
    }

    [Fact]
    public async Task ApproveAsync_AllowsSuperAdminForAnyCompound()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D5");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "requester5@darak.test", UserRole.Accountant);
        var superAdmin = await CreateUserWithRoleAsync(identity.UserManager, "super5@darak.test", UserRole.SuperAdmin);
        var approval = await AddApprovalAsync(dbContext, compound.Id, requester.Id);
        var service = CreateService(dbContext, identity.UserManager, [], isSuperAdmin: true);

        var result = await service.ApproveAsync(
            superAdmin.Id,
            approval.Id,
            new ApprovalDecisionRequest { Reason = "Approved by HQ." });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(ApprovalStatus.Approved);
        result.Value.ExecutionStatus.Should().Be(ApprovalExecutionStatus.ReadyForExecution);
        dbContext.ApprovalDecisions.Should().ContainSingle(decision => decision.DecisionType == ApprovalDecisionType.Approved);
    }

    [Fact]
    public async Task ApproveAsync_BlocksSelfApprovalWhenPolicyForbidsIt()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D6");
        var superAdmin = await CreateUserWithRoleAsync(identity.UserManager, "super6@darak.test", UserRole.SuperAdmin);
        var approval = await AddApprovalAsync(dbContext, compound.Id, superAdmin.Id);
        var service = CreateService(dbContext, identity.UserManager, [], isSuperAdmin: true);

        var result = await service.ApproveAsync(
            superAdmin.Id,
            approval.Id,
            new ApprovalDecisionRequest { Reason = "Self approve." });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        approval.Status.Should().Be(ApprovalStatus.Pending);
    }

    [Fact]
    public async Task ApproveAsync_EnforcesPolicyRequiredApproverRoles()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D6B");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "requester6b@darak.test", UserRole.Accountant);
        var compoundAdmin = await CreateUserWithRoleAsync(identity.UserManager, "admin6b@darak.test", UserRole.CompoundAdmin);
        var approval = await AddApprovalAsync(dbContext, compound.Id, requester.Id);
        await AddPolicyAsync(dbContext, compound.Id, ApprovalActionType.OtherSensitiveAdminOperation, requiredApproverRoles: "SuperAdmin");
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.ApproveAsync(
            compoundAdmin.Id,
            approval.Id,
            new ApprovalDecisionRequest { Reason = "Policy says no." });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        approval.Status.Should().Be(ApprovalStatus.Pending);
        dbContext.ApprovalDecisions.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveAsync_AllowsPolicyRequiredSuperAdmin()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D6C");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "requester6c@darak.test", UserRole.Accountant);
        var superAdmin = await CreateUserWithRoleAsync(identity.UserManager, "super6c@darak.test", UserRole.SuperAdmin);
        var approval = await AddApprovalAsync(dbContext, compound.Id, requester.Id);
        await AddPolicyAsync(dbContext, compound.Id, ApprovalActionType.OtherSensitiveAdminOperation, requiredApproverRoles: "SuperAdmin");
        var service = CreateService(dbContext, identity.UserManager, [], isSuperAdmin: true);

        var result = await service.ApproveAsync(
            superAdmin.Id,
            approval.Id,
            new ApprovalDecisionRequest { Reason = "Allowed by policy." });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(ApprovalStatus.Approved);
    }

    [Fact]
    public async Task RejectAsync_EnforcesPolicyRequiredApproverRoles()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D6D");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "requester6d@darak.test", UserRole.Accountant);
        var compoundAdmin = await CreateUserWithRoleAsync(identity.UserManager, "admin6d@darak.test", UserRole.CompoundAdmin);
        var approval = await AddApprovalAsync(dbContext, compound.Id, requester.Id);
        await AddPolicyAsync(dbContext, compound.Id, ApprovalActionType.OtherSensitiveAdminOperation, requiredApproverRoles: "SuperAdmin");
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.RejectAsync(
            compoundAdmin.Id,
            approval.Id,
            new ApprovalDecisionRequest { Reason = "Policy says no." });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        approval.Status.Should().Be(ApprovalStatus.Pending);
        dbContext.ApprovalDecisions.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveAsync_FailsClosedForInvalidPolicyRequiredApproverRoles()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D6E");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "requester6e@darak.test", UserRole.Accountant);
        var superAdmin = await CreateUserWithRoleAsync(identity.UserManager, "super6e@darak.test", UserRole.SuperAdmin);
        var approval = await AddApprovalAsync(dbContext, compound.Id, requester.Id);
        await AddPolicyAsync(dbContext, compound.Id, ApprovalActionType.OtherSensitiveAdminOperation, requiredApproverRoles: "SuperAdmin,Hacker");
        var service = CreateService(dbContext, identity.UserManager, [], isSuperAdmin: true);

        var result = await service.ApproveAsync(
            superAdmin.Id,
            approval.Id,
            new ApprovalDecisionRequest { Reason = "Invalid policy." });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        approval.Status.Should().Be(ApprovalStatus.Pending);
        dbContext.ApprovalDecisions.Should().BeEmpty();
    }

    [Fact]
    public async Task RejectAsync_RequiresReason()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D7");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "requester7@darak.test", UserRole.Accountant);
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin7@darak.test", UserRole.CompoundAdmin);
        var approval = await AddApprovalAsync(dbContext, compound.Id, requester.Id);
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.RejectAsync(
            admin.Id,
            approval.Id,
            new ApprovalDecisionRequest { Reason = "   " });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        dbContext.ApprovalDecisions.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveAsync_RejectsAlreadyFinalizedApproval()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D8");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "requester8@darak.test", UserRole.Accountant);
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin8@darak.test", UserRole.CompoundAdmin);
        var approval = await AddApprovalAsync(dbContext, compound.Id, requester.Id, ApprovalStatus.Rejected);
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.ApproveAsync(
            admin.Id,
            approval.Id,
            new ApprovalDecisionRequest { Reason = "Too late." });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        approval.Status.Should().Be(ApprovalStatus.Rejected);
    }

    [Fact]
    public async Task CancelAsync_AllowsOnlyRequesterOrPrivilegedAdmin()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D9");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "requester9@darak.test", UserRole.Accountant);
        var otherAccountant = await CreateUserWithRoleAsync(identity.UserManager, "other9@darak.test", UserRole.Accountant);
        var approval = await AddApprovalAsync(dbContext, compound.Id, requester.Id);
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var blocked = await service.CancelAsync(
            otherAccountant.Id,
            approval.Id,
            new ApprovalDecisionRequest { Reason = "Not mine." });

        blocked.Status.Should().Be(ServiceResultStatus.Forbidden);

        var allowed = await service.CancelAsync(
            requester.Id,
            approval.Id,
            new ApprovalDecisionRequest { Reason = "No longer needed." });

        allowed.IsSuccess.Should().BeTrue(allowed.Message);
        allowed.Value!.Status.Should().Be(ApprovalStatus.Cancelled);
        dbContext.ApprovalDecisions.Should().ContainSingle(decision => decision.DecisionType == ApprovalDecisionType.Cancelled);
    }

    [Fact]
    public async Task MarkExecutedAsync_RequiresApprovedRequest()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D10");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "requester10@darak.test", UserRole.Accountant);
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin10@darak.test", UserRole.CompoundAdmin);
        var approval = await AddApprovalAsync(dbContext, compound.Id, requester.Id);
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.MarkExecutedAsync(
            admin.Id,
            approval.Id,
            new MarkApprovalExecutedRequest { Notes = "Executed." });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        approval.Status.Should().Be(ApprovalStatus.Pending);
    }

    [Fact]
    public async Task MarkExecutedAsync_SucceedsAfterApprovalAndCreatesAuditArtifacts()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D11");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "requester11@darak.test", UserRole.Accountant);
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin11@darak.test", UserRole.CompoundAdmin);
        var approval = await AddApprovalAsync(dbContext, compound.Id, requester.Id, ApprovalStatus.Approved, ApprovalExecutionStatus.ReadyForExecution);
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.MarkExecutedAsync(
            admin.Id,
            approval.Id,
            new MarkApprovalExecutedRequest { Notes = "Refund was completed." });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(ApprovalStatus.Executed);
        result.Value.ExecutionStatus.Should().Be(ApprovalExecutionStatus.Executed);
        dbContext.ActivityEvents.Should().Contain(activity => activity.EventType == ActivityEventType.ApprovalExecuted);
        dbContext.NotificationOutboxes.Should().Contain(notification => notification.EventType == NotificationEventType.ApprovalExecuted);
    }

    [Fact]
    public async Task GetDashboardAsync_CountsPendingApprovedRejectedOverdueAndHighPriority()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D12");
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin12@darak.test", UserRole.SuperAdmin);

        dbContext.ApprovalRequests.AddRange(
            NewApproval(compound.Id, admin.Id, ApprovalStatus.Pending, ApprovalPriority.Critical, DateTime.UtcNow.AddHours(-2)),
            NewApproval(compound.Id, admin.Id, ApprovalStatus.Approved, ApprovalPriority.Normal),
            NewApproval(compound.Id, admin.Id, ApprovalStatus.Rejected, ApprovalPriority.Normal),
            NewApproval(compound.Id, admin.Id, ApprovalStatus.Cancelled, ApprovalPriority.Normal),
            NewApproval(compound.Id, admin.Id, ApprovalStatus.Executed, ApprovalPriority.High));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, identity.UserManager, [], isSuperAdmin: true);

        var result = await service.GetDashboardAsync(admin.Id, compound.Id);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.PendingCount.Should().Be(1);
        result.Value.ApprovedCount.Should().Be(1);
        result.Value.RejectedCount.Should().Be(1);
        result.Value.CancelledCount.Should().Be(1);
        result.Value.ExecutedCount.Should().Be(1);
        result.Value.OverdueCount.Should().Be(1);
        result.Value.HighPriorityPendingCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchRequestsAsync_RespectsCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var allowedCompound = await AddCompoundAsync(dbContext, "D13A");
        var blockedCompound = await AddCompoundAsync(dbContext, "D13B");
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin13@darak.test", UserRole.CompoundAdmin);

        dbContext.ApprovalRequests.AddRange(
            NewApproval(allowedCompound.Id, admin.Id, ApprovalStatus.Pending, ApprovalPriority.Normal),
            NewApproval(blockedCompound.Id, admin.Id, ApprovalStatus.Pending, ApprovalPriority.Normal));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, identity.UserManager, [allowedCompound.Id]);

        var result = await service.SearchRequestsAsync(admin.Id, new ApprovalSearchQuery());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Items.Should().ContainSingle();
        result.Value.Items.Single().CompoundId.Should().Be(allowedCompound.Id);
    }

    [Fact]
    public async Task ApproveAsync_AppendsCriticalAuditLogEntry()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "D-AUD");
        var requester = await CreateUserWithRoleAsync(identity.UserManager, "requester-audit@darak.test", UserRole.Accountant);
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin-audit@darak.test", UserRole.CompoundAdmin);
        var approval = await AddApprovalAsync(dbContext, compound.Id, requester.Id);
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.ApproveAsync(
            admin.Id,
            approval.Id,
            new ApprovalDecisionRequest { Reason = "Commercial audit check." });

        result.IsSuccess.Should().BeTrue(result.Message);
        dbContext.AuditLogEntries.Should().ContainSingle(audit =>
            audit.ActionType == AuditActionType.ApprovalApproved
            && audit.EntityType == AuditEntityType.ApprovalRequest
            && audit.EntityId == approval.Id
            && audit.Severity == AuditSeverity.Critical
            && audit.SourceModule == "Approvals");
        dbContext.AuditLogChanges.Should().Contain(change =>
            change.PropertyName == nameof(ApprovalRequest.Status)
            && change.OldValue == ApprovalStatus.Pending.ToString()
            && change.NewValue == ApprovalStatus.Approved.ToString());
    }

    private static ApprovalService CreateService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        Guid[] allowedCompoundIds,
        bool isSuperAdmin = false)
    {
        return new ApprovalService(
            dbContext,
            new FakeCompoundAccessService(allowedCompoundIds, isSuperAdmin: isSuperAdmin),
            userManager,
            new AuditLogService(
                dbContext,
                new FakeCompoundAccessService(allowedCompoundIds, isSuperAdmin: isSuperAdmin),
                new HttpContextAccessor()));
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

    private static async Task<Payment> AddPaymentAsync(
        ApplicationDbContext dbContext,
        Guid compoundId)
    {
        var payment = new Payment
        {
            CompoundId = compoundId,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = Guid.NewGuid(),
            PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = 10000,
            Currency = "IQD",
            PaymentReference = Guid.NewGuid().ToString("N"),
            CompletedAt = DateTime.UtcNow
        };

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        return payment;
    }

    private static async Task AddPolicyAsync(
        ApplicationDbContext dbContext,
        Guid? compoundId,
        ApprovalActionType actionType,
        string requiredApproverRoles,
        bool allowSelfApproval = false)
    {
        dbContext.ApprovalPolicies.Add(new ApprovalPolicy
        {
            CompoundId = compoundId,
            ActionType = actionType,
            IsEnabled = true,
            AllowSelfApproval = allowSelfApproval,
            DefaultPriority = ApprovalPriority.Normal,
            ExpireAfterHours = 72,
            RequiredApproverRoles = requiredApproverRoles,
            Description = "Test approval policy."
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task<ApprovalRequest> AddApprovalAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid requesterUserId,
        ApprovalStatus status = ApprovalStatus.Pending,
        ApprovalExecutionStatus executionStatus = ApprovalExecutionStatus.NotReady)
    {
        var approval = NewApproval(compoundId, requesterUserId, status, ApprovalPriority.Normal);
        approval.ExecutionStatus = executionStatus;
        dbContext.ApprovalRequests.Add(approval);
        await dbContext.SaveChangesAsync();
        return approval;
    }

    private static ApprovalRequest NewApproval(
        Guid compoundId,
        Guid requesterUserId,
        ApprovalStatus status,
        ApprovalPriority priority,
        DateTime? dueAtUtc = null)
    {
        return new ApprovalRequest
        {
            CompoundId = compoundId,
            RequestedByUserId = requesterUserId,
            ActionType = ApprovalActionType.OtherSensitiveAdminOperation,
            EntityType = ApprovalEntityType.None,
            Status = status,
            Priority = priority,
            ExecutionStatus = status == ApprovalStatus.Approved
                ? ApprovalExecutionStatus.ReadyForExecution
                : status == ApprovalStatus.Executed
                    ? ApprovalExecutionStatus.Executed
                    : ApprovalExecutionStatus.NotReady,
            Reason = "Seed approval request.",
            CreatedAtUtc = DateTime.UtcNow,
            DueAtUtc = dueAtUtc ?? DateTime.UtcNow.AddHours(24)
        };
    }

    private sealed record TestIdentity(
        UserManager<ApplicationUser> UserManager,
        RoleManager<IdentityRole<Guid>> RoleManager);
}
