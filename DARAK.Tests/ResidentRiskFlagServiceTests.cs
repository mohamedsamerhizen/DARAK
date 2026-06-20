using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.RiskFlags;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;

namespace DARAK.Tests;

public sealed class ResidentRiskFlagServiceTests
{
    [Fact]
    public async Task CreateFlagAsync_SucceedsForScopedCompoundAdminAndCreatesAuditArtifacts()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "RF1");
        var residentUser = await CreateUserWithRoleAsync(identity.UserManager, "resident1@darak.test", UserRole.Resident);
        var resident = await AddResidentAsync(dbContext, compound.Id, residentUser.Id, "Resident One");
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin1-risk@darak.test", UserRole.CompoundAdmin);
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.CreateFlagAsync(
            admin.Id,
            new CreateResidentRiskFlagRequest
            {
                CompoundId = compound.Id,
                ResidentProfileId = resident.Id,
                FlagType = ResidentRiskFlagType.StaffSafetyConcern,
                Severity = ResidentRiskFlagSeverity.High,
                Source = ResidentRiskFlagSource.Manual,
                Title = "Repeated aggressive interactions",
                Description = "Staff reported repeated hostile interactions.",
                RecommendedAction = "Supervisor should review next contact.",
                InternalNotes = "Visible to admin users only.",
                RequiresSupervisorReview = true,
                MetadataJson = "{\"case\":\"RF1\"}"
            });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(ResidentRiskFlagStatus.Active);
        result.Value.Severity.Should().Be(ResidentRiskFlagSeverity.High);
        result.Value.HasInternalNotes.Should().BeTrue();

        dbContext.ResidentRiskFlags.Should().ContainSingle();
        dbContext.ResidentRiskFlagActions.Should().ContainSingle(action => action.ActionType == ResidentRiskFlagActionType.Created);
        dbContext.ActivityEvents.Should().ContainSingle(activity =>
            activity.EventType == ActivityEventType.RiskFlagCreated
            && activity.EntityType == ActivityEntityType.ResidentRiskFlag);
        dbContext.NotificationOutboxes.Should().ContainSingle(notification =>
            notification.EventType == NotificationEventType.RiskFlagCreated
            && notification.RelatedEntityType == NotificationRelatedEntityType.ResidentRiskFlag);
    }

    [Fact]
    public async Task CreateFlagAsync_RejectsResidentUsers()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "RF2");
        var residentUser = await CreateUserWithRoleAsync(identity.UserManager, "resident2@darak.test", UserRole.Resident);
        var resident = await AddResidentAsync(dbContext, compound.Id, residentUser.Id, "Resident Two");
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.CreateFlagAsync(
            residentUser.Id,
            NewCreateRequest(compound.Id, resident.Id));

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        dbContext.ResidentRiskFlags.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateFlagAsync_RejectsGuardUsers()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "RF3");
        var residentUser = await CreateUserWithRoleAsync(identity.UserManager, "resident3@darak.test", UserRole.Resident);
        var resident = await AddResidentAsync(dbContext, compound.Id, residentUser.Id, "Resident Three");
        var guard = await CreateUserWithRoleAsync(identity.UserManager, "guard-risk@darak.test", UserRole.Guard);
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.CreateFlagAsync(
            guard.Id,
            NewCreateRequest(compound.Id, resident.Id));

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
    }

    [Fact]
    public async Task CreateFlagAsync_RejectsCompoundOutsideCurrentScope()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var allowedCompound = await AddCompoundAsync(dbContext, "RF4A");
        var blockedCompound = await AddCompoundAsync(dbContext, "RF4B");
        var residentUser = await CreateUserWithRoleAsync(identity.UserManager, "resident4@darak.test", UserRole.Resident);
        var resident = await AddResidentAsync(dbContext, blockedCompound.Id, residentUser.Id, "Resident Four");
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin4-risk@darak.test", UserRole.CompoundAdmin);
        var service = CreateService(dbContext, identity.UserManager, [allowedCompound.Id]);

        var result = await service.CreateFlagAsync(
            admin.Id,
            NewCreateRequest(blockedCompound.Id, resident.Id));

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        dbContext.ResidentRiskFlags.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateFlagAsync_RejectsSourceEntityFromDifferentResident()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "RF5");
        var residentUser1 = await CreateUserWithRoleAsync(identity.UserManager, "resident5a@darak.test", UserRole.Resident);
        var residentUser2 = await CreateUserWithRoleAsync(identity.UserManager, "resident5b@darak.test", UserRole.Resident);
        var resident1 = await AddResidentAsync(dbContext, compound.Id, residentUser1.Id, "Resident Five A");
        var resident2 = await AddResidentAsync(dbContext, compound.Id, residentUser2.Id, "Resident Five B");
        var payment = await AddPaymentAsync(dbContext, compound.Id, resident2.Id);
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin5-risk@darak.test", UserRole.CompoundAdmin);
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var request = new CreateResidentRiskFlagRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident1.Id,
            FlagType = ResidentRiskFlagType.ManualWatchlist,
            Severity = ResidentRiskFlagSeverity.Medium,
            Source = ResidentRiskFlagSource.Payment,
            SourceEntityType = ResidentRiskFlagSourceEntityType.Payment,
            SourceEntityId = payment.Id,
            Title = "Manual watchlist note",
            Description = "Admin wants this resident monitored."
        };

        var result = await service.CreateFlagAsync(admin.Id, request);

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        dbContext.ResidentRiskFlags.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchFlagsAsync_RespectsCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var allowedCompound = await AddCompoundAsync(dbContext, "RF6A");
        var blockedCompound = await AddCompoundAsync(dbContext, "RF6B");
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin6-risk@darak.test", UserRole.CompoundAdmin);
        var allowedResidentUser = await CreateUserWithRoleAsync(identity.UserManager, "resident6a@darak.test", UserRole.Resident);
        var blockedResidentUser = await CreateUserWithRoleAsync(identity.UserManager, "resident6b@darak.test", UserRole.Resident);
        var allowedResident = await AddResidentAsync(dbContext, allowedCompound.Id, allowedResidentUser.Id, "Resident Six A");
        var blockedResident = await AddResidentAsync(dbContext, blockedCompound.Id, blockedResidentUser.Id, "Resident Six B");

        dbContext.ResidentRiskFlags.AddRange(
            NewFlag(allowedCompound.Id, allowedResident.Id, admin.Id),
            NewFlag(blockedCompound.Id, blockedResident.Id, admin.Id));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, identity.UserManager, [allowedCompound.Id]);

        var result = await service.SearchFlagsAsync(admin.Id, new ResidentRiskFlagSearchQuery());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Items.Should().ContainSingle();
        result.Value.Items.Single().CompoundId.Should().Be(allowedCompound.Id);
    }

    [Fact]
    public async Task GetDetailsAsync_ReturnsNotFoundOutsideCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var allowedCompound = await AddCompoundAsync(dbContext, "RF7A");
        var blockedCompound = await AddCompoundAsync(dbContext, "RF7B");
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin7-risk@darak.test", UserRole.CompoundAdmin);
        var residentUser = await CreateUserWithRoleAsync(identity.UserManager, "resident7@darak.test", UserRole.Resident);
        var resident = await AddResidentAsync(dbContext, blockedCompound.Id, residentUser.Id, "Resident Seven");
        var flag = NewFlag(blockedCompound.Id, resident.Id, admin.Id);
        dbContext.ResidentRiskFlags.Add(flag);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, identity.UserManager, [allowedCompound.Id]);

        var result = await service.GetDetailsAsync(admin.Id, flag.Id);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task AssignAsync_RejectsAssignedUserWithoutCompoundAccess()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "RF8");
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin8-risk@darak.test", UserRole.CompoundAdmin);
        var otherAdmin = await CreateUserWithRoleAsync(identity.UserManager, "other8-risk@darak.test", UserRole.CompoundAdmin);
        var residentUser = await CreateUserWithRoleAsync(identity.UserManager, "resident8@darak.test", UserRole.Resident);
        var resident = await AddResidentAsync(dbContext, compound.Id, residentUser.Id, "Resident Eight");
        var flag = NewFlag(compound.Id, resident.Id, admin.Id);
        dbContext.ResidentRiskFlags.Add(flag);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.AssignAsync(
            admin.Id,
            flag.Id,
            new AssignResidentRiskFlagRequest { AssignedToUserId = otherAdmin.Id, Notes = "Assign." });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task AssignAsync_AllowsScopedAssignedUserAndCreatesAction()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "RF9");
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin9-risk@darak.test", UserRole.CompoundAdmin);
        var accountant = await CreateUserWithRoleAsync(identity.UserManager, "accountant9-risk@darak.test", UserRole.Accountant);
        var residentUser = await CreateUserWithRoleAsync(identity.UserManager, "resident9@darak.test", UserRole.Resident);
        var resident = await AddResidentAsync(dbContext, compound.Id, residentUser.Id, "Resident Nine");
        var flag = NewFlag(compound.Id, resident.Id, admin.Id);
        dbContext.ResidentRiskFlags.Add(flag);
        await dbContext.SaveChangesAsync();
        var roleAccess = new Dictionary<(Guid UserId, Guid CompoundId, UserRole Role), bool>
        {
            [(accountant.Id, compound.Id, UserRole.Accountant)] = true
        };
        var service = CreateService(dbContext, identity.UserManager, [compound.Id], roleAccess);

        var result = await service.AssignAsync(
            admin.Id,
            flag.Id,
            new AssignResidentRiskFlagRequest { AssignedToUserId = accountant.Id, Notes = "Assign to finance." });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.AssignedToUserId.Should().Be(accountant.Id);
        dbContext.ResidentRiskFlagActions.Should().Contain(action => action.ActionType == ResidentRiskFlagActionType.Assigned);
    }

    [Fact]
    public async Task ChangeSeverityAsync_RequiresNotes()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "RF10");
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin10-risk@darak.test", UserRole.CompoundAdmin);
        var residentUser = await CreateUserWithRoleAsync(identity.UserManager, "resident10@darak.test", UserRole.Resident);
        var resident = await AddResidentAsync(dbContext, compound.Id, residentUser.Id, "Resident Ten");
        var flag = NewFlag(compound.Id, resident.Id, admin.Id);
        dbContext.ResidentRiskFlags.Add(flag);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.ChangeSeverityAsync(
            admin.Id,
            flag.Id,
            new ChangeResidentRiskFlagSeverityRequest { Severity = ResidentRiskFlagSeverity.Critical, Notes = "   " });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        flag.Severity.Should().Be(ResidentRiskFlagSeverity.Medium);
    }

    [Fact]
    public async Task ChangeSeverityAsync_SucceedsAndCreatesActivityAndNotification()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "RF11");
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin11-risk@darak.test", UserRole.CompoundAdmin);
        var residentUser = await CreateUserWithRoleAsync(identity.UserManager, "resident11@darak.test", UserRole.Resident);
        var resident = await AddResidentAsync(dbContext, compound.Id, residentUser.Id, "Resident Eleven");
        var flag = NewFlag(compound.Id, resident.Id, admin.Id);
        dbContext.ResidentRiskFlags.Add(flag);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.ChangeSeverityAsync(
            admin.Id,
            flag.Id,
            new ChangeResidentRiskFlagSeverityRequest { Severity = ResidentRiskFlagSeverity.Critical, Notes = "Escalated." });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Severity.Should().Be(ResidentRiskFlagSeverity.Critical);
        dbContext.ActivityEvents.Should().Contain(activity => activity.EventType == ActivityEventType.RiskFlagSeverityChanged);
        dbContext.NotificationOutboxes.Should().Contain(notification => notification.EventType == NotificationEventType.RiskFlagSeverityChanged);
    }

    [Fact]
    public async Task ResolveAsync_RejectsAccountantAndRequiresPrivilegedAdmin()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "RF12");
        var accountant = await CreateUserWithRoleAsync(identity.UserManager, "accountant12-risk@darak.test", UserRole.Accountant);
        var residentUser = await CreateUserWithRoleAsync(identity.UserManager, "resident12@darak.test", UserRole.Resident);
        var resident = await AddResidentAsync(dbContext, compound.Id, residentUser.Id, "Resident Twelve");
        var flag = NewFlag(compound.Id, resident.Id, accountant.Id);
        dbContext.ResidentRiskFlags.Add(flag);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.ResolveAsync(
            accountant.Id,
            flag.Id,
            new CloseResidentRiskFlagRequest { Reason = "Done." });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        flag.Status.Should().Be(ResidentRiskFlagStatus.Active);
    }

    [Fact]
    public async Task ResolveAsync_SucceedsForCompoundAdminAndCreatesClosureArtifacts()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "RF13");
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin13-risk@darak.test", UserRole.CompoundAdmin);
        var residentUser = await CreateUserWithRoleAsync(identity.UserManager, "resident13@darak.test", UserRole.Resident);
        var resident = await AddResidentAsync(dbContext, compound.Id, residentUser.Id, "Resident Thirteen");
        var flag = NewFlag(compound.Id, resident.Id, admin.Id);
        dbContext.ResidentRiskFlags.Add(flag);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, identity.UserManager, [compound.Id]);

        var result = await service.ResolveAsync(
            admin.Id,
            flag.Id,
            new CloseResidentRiskFlagRequest { Reason = "Issue was resolved." });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(ResidentRiskFlagStatus.Resolved);
        result.Value.ResolutionNotes.Should().Be("Issue was resolved.");
        dbContext.ResidentRiskFlagActions.Should().Contain(action => action.ActionType == ResidentRiskFlagActionType.Resolved);
        dbContext.ActivityEvents.Should().Contain(activity => activity.EventType == ActivityEventType.RiskFlagResolved);
        dbContext.NotificationOutboxes.Should().Contain(notification => notification.EventType == NotificationEventType.RiskFlagResolved);
    }

    [Fact]
    public async Task GetDashboardAsync_CountsActiveMonitoringHighCriticalOverdueAndUnassigned()
    {
        await using var dbContext = TestDb.Create();
        var identity = await CreateIdentityAsync(dbContext);
        var compound = await AddCompoundAsync(dbContext, "RF14");
        var admin = await CreateUserWithRoleAsync(identity.UserManager, "admin14-risk@darak.test", UserRole.SuperAdmin);
        var residentUser = await CreateUserWithRoleAsync(identity.UserManager, "resident14@darak.test", UserRole.Resident);
        var resident = await AddResidentAsync(dbContext, compound.Id, residentUser.Id, "Resident Fourteen");

        dbContext.ResidentRiskFlags.AddRange(
            NewFlag(compound.Id, resident.Id, admin.Id, ResidentRiskFlagStatus.Active, ResidentRiskFlagSeverity.Critical, DateTime.UtcNow.AddHours(-3)),
            NewFlag(compound.Id, resident.Id, admin.Id, ResidentRiskFlagStatus.Monitoring, ResidentRiskFlagSeverity.High, DateTime.UtcNow.AddHours(12), assignedToUserId: admin.Id),
            NewFlag(compound.Id, resident.Id, admin.Id, ResidentRiskFlagStatus.Resolved, ResidentRiskFlagSeverity.Low),
            NewFlag(compound.Id, resident.Id, admin.Id, ResidentRiskFlagStatus.Dismissed, ResidentRiskFlagSeverity.Medium),
            NewFlag(compound.Id, resident.Id, admin.Id, ResidentRiskFlagStatus.Expired, ResidentRiskFlagSeverity.Medium));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, identity.UserManager, [], isSuperAdmin: true);

        var result = await service.GetDashboardAsync(admin.Id, compound.Id);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.ActiveCount.Should().Be(1);
        result.Value.MonitoringCount.Should().Be(1);
        result.Value.ResolvedCount.Should().Be(1);
        result.Value.DismissedCount.Should().Be(1);
        result.Value.ExpiredCount.Should().Be(1);
        result.Value.HighOrCriticalActiveCount.Should().Be(2);
        result.Value.CriticalActiveCount.Should().Be(1);
        result.Value.OverdueReviewCount.Should().Be(1);
        result.Value.UnassignedActiveCount.Should().Be(1);
        result.Value.OpenBySeverity.Should().HaveCount(2);
    }

    private static CreateResidentRiskFlagRequest NewCreateRequest(
        Guid compoundId,
        Guid residentProfileId)
    {
        return new CreateResidentRiskFlagRequest
        {
            CompoundId = compoundId,
            ResidentProfileId = residentProfileId,
            FlagType = ResidentRiskFlagType.ManualWatchlist,
            Severity = ResidentRiskFlagSeverity.Medium,
            Source = ResidentRiskFlagSource.Manual,
            Title = "Manual watchlist note",
            Description = "Admin wants this resident monitored."
        };
    }

    private static ResidentRiskFlagService CreateService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        Guid[] allowedCompoundIds,
        Dictionary<(Guid UserId, Guid CompoundId, UserRole Role), bool>? roleAccess = null,
        bool isSuperAdmin = false)
    {
        return new ResidentRiskFlagService(
            dbContext,
            new FakeCompoundAccessService(allowedCompoundIds, roleAccess, isSuperAdmin: isSuperAdmin),
            userManager);
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

    private static async Task<ResidentProfile> AddResidentAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid userId,
        string fullName)
    {
        var resident = new ResidentProfile
        {
            CompoundId = compoundId,
            UserId = userId,
            FullName = fullName,
            PhoneNumber = "+9647700000000"
        };

        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }

    private static async Task<Payment> AddPaymentAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid residentProfileId)
    {
        var payment = new Payment
        {
            CompoundId = compoundId,
            ResidentProfileId = residentProfileId,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = Guid.NewGuid(),
            PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = 25000,
            Currency = "IQD",
            PaymentReference = Guid.NewGuid().ToString("N"),
            CompletedAt = DateTime.UtcNow
        };

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        return payment;
    }

    private static ResidentRiskFlag NewFlag(
        Guid compoundId,
        Guid residentProfileId,
        Guid createdByUserId,
        ResidentRiskFlagStatus status = ResidentRiskFlagStatus.Active,
        ResidentRiskFlagSeverity severity = ResidentRiskFlagSeverity.Medium,
        DateTime? nextReviewAtUtc = null,
        Guid? assignedToUserId = null)
    {
        return new ResidentRiskFlag
        {
            CompoundId = compoundId,
            ResidentProfileId = residentProfileId,
            CreatedByUserId = createdByUserId,
            AssignedToUserId = assignedToUserId,
            FlagType = ResidentRiskFlagType.ManualWatchlist,
            Severity = severity,
            Status = status,
            Source = ResidentRiskFlagSource.Manual,
            Title = "Seed risk flag",
            Description = "Seed risk flag description.",
            CreatedAtUtc = DateTime.UtcNow,
            NextReviewAtUtc = nextReviewAtUtc
        };
    }

    private sealed record TestIdentity(
        UserManager<ApplicationUser> UserManager,
        RoleManager<IdentityRole<Guid>> RoleManager);
}
