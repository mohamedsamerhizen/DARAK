using System.Net;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Support;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class Phase1A1AuthorizationBoundaryTests
{
    [Fact]
    public async Task ProcessDue_CompoundAdmin_Returns403()
    {
        using var factory = new DarakApiFactory();
        var compoundAdmin = await factory.CreateAuthenticatedClientAsync(UserRole.CompoundAdmin, Guid.NewGuid());

        var response = await compoundAdmin.Client.PostAsync("/api/admin/notifications/process-due?batchSize=1", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProcessDue_SuperAdmin_ReturnsSuccess()
    {
        using var factory = new DarakApiFactory();
        var superAdmin = await factory.CreateAuthenticatedClientAsync(UserRole.SuperAdmin);

        var response = await superAdmin.Client.PostAsync("/api/admin/notifications/process-due?batchSize=1", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BackgroundJobs_CompoundAdmin_Returns403()
    {
        using var factory = new DarakApiFactory();
        var compoundAdmin = await factory.CreateAuthenticatedClientAsync(UserRole.CompoundAdmin, Guid.NewGuid());

        var response = await compoundAdmin.Client.GetAsync("/api/admin/system/background-jobs");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BackgroundJobs_SuperAdmin_ReturnsSuccess()
    {
        using var factory = new DarakApiFactory();
        var superAdmin = await factory.CreateAuthenticatedClientAsync(UserRole.SuperAdmin);

        var response = await superAdmin.Client.GetAsync("/api/admin/system/background-jobs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IntegrationFailures_CompoundAdminOrAccountant_Returns403()
    {
        using var factory = new DarakApiFactory();
        var compoundAdmin = await factory.CreateAuthenticatedClientAsync(UserRole.CompoundAdmin, Guid.NewGuid());
        var accountant = await factory.CreateAuthenticatedClientAsync(UserRole.Accountant, Guid.NewGuid());

        var compoundAdminResponse = await compoundAdmin.Client.GetAsync("/api/admin/system/integration-failures");
        var accountantResponse = await accountant.Client.GetAsync("/api/admin/system/integration-failures");

        compoundAdminResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        accountantResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task IntegrationFailures_SuperAdmin_ReturnsSuccess()
    {
        using var factory = new DarakApiFactory();
        var superAdmin = await factory.CreateAuthenticatedClientAsync(UserRole.SuperAdmin);

        var response = await superAdmin.Client.GetAsync("/api/admin/system/integration-failures");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AssignCase_NonExistentUser_ReturnsBadRequestOrNotFound()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P1A1-ASG-404");
        var supportCase = await AddSupportCaseAsync(dbContext, compound.Id);
        var service = CreateSupportService(dbContext, [compound.Id]);

        var result = await service.AssignCaseAsync(Guid.NewGuid(), supportCase.Id, new AssignSupportCaseRequest
        {
            AssignedToUserId = Guid.NewGuid(),
            Note = "Unknown assignee."
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().BeOneOf(ServiceResultStatus.BadRequest, ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task AssignCase_ResidentOnlyUser_ReturnsBadRequestOrForbidden()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P1A1-ASG-RES");
        var supportCase = await AddSupportCaseAsync(dbContext, compound.Id);
        var residentOnly = await AddUserWithRolesAsync(dbContext, [UserRole.Resident]);
        var service = CreateSupportService(dbContext, [compound.Id]);

        var result = await service.AssignCaseAsync(Guid.NewGuid(), supportCase.Id, new AssignSupportCaseRequest
        {
            AssignedToUserId = residentOnly.Id,
            Note = "Resident-only user should not receive internal support assignment."
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().BeOneOf(ServiceResultStatus.BadRequest, ServiceResultStatus.Forbidden);
    }

    [Fact]
    public async Task AssignCase_AdminOutsideCompound_ReturnsForbiddenOrNotFound()
    {
        await using var dbContext = TestDb.Create();
        var caseCompound = await AddCompoundAsync(dbContext, "P1A1-ASG-IN");
        var outsideCompound = await AddCompoundAsync(dbContext, "P1A1-ASG-OUT");
        var supportCase = await AddSupportCaseAsync(dbContext, caseCompound.Id);
        var outsideAdmin = await AddUserWithRolesAsync(dbContext, [UserRole.CompoundAdmin], outsideCompound.Id);
        var service = CreateSupportService(dbContext, [caseCompound.Id]);

        var result = await service.AssignCaseAsync(Guid.NewGuid(), supportCase.Id, new AssignSupportCaseRequest
        {
            AssignedToUserId = outsideAdmin.Id,
            Note = "Outside compound assignment should be blocked."
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().BeOneOf(ServiceResultStatus.Forbidden, ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task AssignCase_ValidAdminInsideCompound_ReturnsSuccess()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P1A1-ASG-OK");
        var supportCase = await AddSupportCaseAsync(dbContext, compound.Id);
        var insideAdmin = await AddUserWithRolesAsync(dbContext, [UserRole.CompoundAdmin], compound.Id);
        var service = CreateSupportService(dbContext, [compound.Id]);

        var result = await service.AssignCaseAsync(Guid.NewGuid(), supportCase.Id, new AssignSupportCaseRequest
        {
            AssignedToUserId = insideAdmin.Id,
            Note = "Inside compound assignment is valid."
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.AssignedToUserId.Should().Be(insideAdmin.Id);
        result.Value.Status.Should().Be(SupportCaseStatus.Assigned);
    }

    [Fact]
    public async Task AssignCase_SuperAdmin_CanAssignCrossCompound()
    {
        await using var dbContext = TestDb.Create();
        var caseCompound = await AddCompoundAsync(dbContext, "P1A1-ASG-SA-A");
        var assigneeCompound = await AddCompoundAsync(dbContext, "P1A1-ASG-SA-B");
        var supportCase = await AddSupportCaseAsync(dbContext, caseCompound.Id);
        var superAdmin = await AddUserWithRolesAsync(dbContext, [UserRole.SuperAdmin]);
        var outsideAdmin = await AddUserWithRolesAsync(dbContext, [UserRole.CompoundAdmin], assigneeCompound.Id);
        var service = CreateSupportService(dbContext, [], isSuperAdmin: true);

        var result = await service.AssignCaseAsync(superAdmin.Id, supportCase.Id, new AssignSupportCaseRequest
        {
            AssignedToUserId = outsideAdmin.Id,
            Note = "SuperAdmin cross-compound assignment."
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.AssignedToUserId.Should().Be(outsideAdmin.Id);
        result.Value.Status.Should().Be(SupportCaseStatus.Assigned);
    }

    private static SupportCaseService CreateSupportService(
        ApplicationDbContext dbContext,
        Guid[] allowedCompoundIds,
        bool isSuperAdmin = false)
    {
        var compoundAccess = new FakeCompoundAccessService(allowedCompoundIds, isSuperAdmin: isSuperAdmin);
        return new SupportCaseService(
            dbContext,
            compoundAccess,
            new AuditLogService(dbContext, compoundAccess, new HttpContextAccessor()));
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = new Compound
        {
            Id = Guid.NewGuid(),
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Karrada",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<SupportCase> AddSupportCaseAsync(ApplicationDbContext dbContext, Guid compoundId)
    {
        var supportCase = new SupportCase
        {
            Id = Guid.NewGuid(),
            CompoundId = compoundId,
            Category = SupportCaseCategory.General,
            Priority = SupportCasePriority.Normal,
            Status = SupportCaseStatus.Open,
            Title = "Support assignment boundary",
            Description = "Phase 1A-1 assignment validation test.",
            DueAtUtc = DateTime.UtcNow.AddDays(2),
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.SupportCases.Add(supportCase);
        await dbContext.SaveChangesAsync();
        return supportCase;
    }

    private static async Task<ApplicationUser> AddUserWithRolesAsync(
        ApplicationDbContext dbContext,
        IReadOnlyCollection<UserRole> roles,
        Guid? compoundId = null)
    {
        var email = $"phase1a1-{Guid.NewGuid():N}@darak.test";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            FullName = "Phase 1A-1 Test User",
            EmailConfirmed = true
        };
        dbContext.Users.Add(user);

        foreach (var role in roles.Distinct())
        {
            var roleName = role.ToString();
            var identityRole = dbContext.Roles.Local.FirstOrDefault(item => item.Name == roleName)
                ?? await dbContext.Roles.FirstOrDefaultAsync(item => item.Name == roleName);
            if (identityRole is null)
            {
                identityRole = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant()
                };
                dbContext.Roles.Add(identityRole);
            }

            dbContext.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = user.Id,
                RoleId = identityRole.Id
            });

            if (compoundId.HasValue && role is not UserRole.SuperAdmin and not UserRole.Resident)
            {
                dbContext.UserCompoundAssignments.Add(new UserCompoundAssignment
                {
                    UserId = user.Id,
                    CompoundId = compoundId.Value,
                    Role = role,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        await dbContext.SaveChangesAsync();
        return user;
    }
}
