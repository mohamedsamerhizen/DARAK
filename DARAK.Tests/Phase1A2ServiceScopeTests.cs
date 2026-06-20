using DARAK.Api.Data;
using DARAK.Api.DTOs.Buildings;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.DTOs.FamilyMembers;
using DARAK.Api.DTOs.Maintenance;
using DARAK.Api.DTOs.Notifications;
using DARAK.Api.DTOs.Visitors;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using DARAK.Api.Services;
using DARAK.Api.Services.Notifications;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DARAK.Tests;

public sealed class Phase1A2ServiceScopeTests
{
    [Fact]
    public async Task StructureSearchBuildings_FiltersToAllowedCompounds()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "P1A2-STR-A");
        var blocked = await AddCompoundAsync(dbContext, "P1A2-STR-B");
        await AddBuildingAsync(dbContext, allowed.Id, "Allowed Building", "ALW-BLD");
        await AddBuildingAsync(dbContext, blocked.Id, "Blocked Building", "BLK-BLD");
        var service = new CompoundStructureService(dbContext, new FakeCompoundAccessService([allowed.Id]));

        var result = await service.SearchBuildingsAsync(new BuildingSearchQuery());

        result.Items.Should().ContainSingle();
        result.Items.Single().CompoundId.Should().Be(allowed.Id);
    }

    [Fact]
    public async Task StructureGetBuilding_OutsideCompound_ReturnsNotFound()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "P1A2-GET-A");
        var blocked = await AddCompoundAsync(dbContext, "P1A2-GET-B");
        var blockedBuilding = await AddBuildingAsync(dbContext, blocked.Id, "Blocked Building", "BLD-OUT");
        var service = new CompoundStructureService(dbContext, new FakeCompoundAccessService([allowed.Id]));

        var result = await service.GetBuildingAsync(blockedBuilding.Id);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task StructureCreateBuilding_OutsideCompound_ReturnsForbidden()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "P1A2-CB-A");
        var blocked = await AddCompoundAsync(dbContext, "P1A2-CB-B");
        var service = new CompoundStructureService(dbContext, new FakeCompoundAccessService([allowed.Id]));

        var result = await service.CreateBuildingAsync(new CreateBuildingRequest
        {
            CompoundId = blocked.Id,
            Name = "Outside Building",
            Code = "OUT-BLD",
            NumberOfFloors = 2
        });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
    }

    [Fact]
    public async Task EnqueueManualAsync_OutsideCompound_ReturnsForbidden()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "P1A2-NOT-A");
        var blocked = await AddCompoundAsync(dbContext, "P1A2-NOT-B");
        var service = CreateNotificationService(dbContext, [allowed.Id]);

        var result = await service.EnqueueManualAsync(Guid.NewGuid(), new ManualNotificationRequest
        {
            CompoundId = blocked.Id,
            Channel = NotificationChannel.Email,
            RecipientName = "Resident",
            RecipientEmail = "resident@darak.test",
            Subject = "Blocked",
            Body = "This should not be enqueued."
        });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        dbContext.NotificationOutboxes.Should().BeEmpty();
    }

    [Fact]
    public async Task NotificationPreferences_TargetUserOutsideCompound_ReturnsForbidden()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "P1A2-PREF-A");
        var blocked = await AddCompoundAsync(dbContext, "P1A2-PREF-B");
        var currentAdmin = await AddUserAsync(dbContext, "p1a2-pref-admin@darak.test");
        var blockedUser = await AddUserAsync(dbContext, "p1a2-pref-blocked@darak.test");
        await AddResidentProfileAsync(dbContext, blockedUser.Id, blocked.Id, "Blocked Resident");
        var service = CreateCommunicationService(dbContext, [allowed.Id]);

        var result = await service.GetPreferencesAsync(currentAdmin.Id, blockedUser.Id);

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
    }

    [Fact]
    public async Task NotificationPreferences_TargetUserInsideCompound_ReturnsSuccess()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "P1A2-PREF-OK");
        var currentAdmin = await AddUserAsync(dbContext, "p1a2-pref-ok-admin@darak.test");
        var residentUser = await AddUserAsync(dbContext, "p1a2-pref-ok-resident@darak.test");
        await AddResidentProfileAsync(dbContext, residentUser.Id, allowed.Id, "Allowed Resident");
        var service = CreateCommunicationService(dbContext, [allowed.Id]);

        var result = await service.GetPreferencesAsync(currentAdmin.Id, residentUser.Id);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.UserId.Should().Be(residentUser.Id);
    }

    [Fact]
    public async Task CreateCampaign_TargetBuildingOutsideCampaignCompound_ReturnsBadRequest()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "P1A2-CAMP-A");
        var blocked = await AddCompoundAsync(dbContext, "P1A2-CAMP-B");
        var currentAdmin = await AddUserAsync(dbContext, "p1a2-campaign-admin@darak.test");
        var blockedBuilding = await AddBuildingAsync(dbContext, blocked.Id, "Blocked Building", "CAMP-BLD");
        var service = CreateCommunicationService(dbContext, [allowed.Id]);

        var result = await service.CreateCampaignAsync(currentAdmin.Id, new CreateCommunicationCampaignRequest
        {
            CompoundId = allowed.Id,
            Title = "Wrong target",
            Body = "Target building belongs to another compound.",
            TargetType = CommunicationCampaignTargetType.Building,
            TargetBuildingId = blockedBuilding.Id
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task VisitorAccessLogs_OutsideCompound_ReturnsNotFound()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "P1A2-VIS-A");
        var blocked = await AddCompoundAsync(dbContext, "P1A2-VIS-B");
        var pass = await AddVisitorPassAsync(dbContext, blocked.Id);
        var service = new VisitorPassService(dbContext, new FakeCompoundAccessService([allowed.Id]));

        var result = await service.GetAccessLogsAsync(pass.Id, new VisitorAccessLogSearchQuery());

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task MaintenanceHistory_OutsideCompound_ReturnsNotFound()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "P1A2-MNT-A");
        var blocked = await AddCompoundAsync(dbContext, "P1A2-MNT-B");
        var request = await AddMaintenanceRequestAsync(dbContext, blocked.Id);
        var service = new MaintenanceService(
            dbContext,
            IdentityTestHelpers.CreateUserManager(dbContext),
            new FakeCompoundAccessService([allowed.Id]));

        var result = await service.GetHistoryAsync(request.Id, new MaintenanceStatusHistorySearchQuery());

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task ResidentFamilyMembers_OutsideCompound_ReturnsNotFound()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "P1A2-FAM-A");
        var blocked = await AddCompoundAsync(dbContext, "P1A2-FAM-B");
        var blockedUser = await AddUserAsync(dbContext, "p1a2-family-blocked@darak.test");
        var blockedResident = await AddResidentProfileAsync(dbContext, blockedUser.Id, blocked.Id, "Blocked Resident");
        var service = CreateResidentService(dbContext, [allowed.Id]);

        var result = await service.GetFamilyMembersAsync(blockedResident.Id);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task ResidentFamilyMembers_CurrentResidentSelfAccess_ReturnsSuccess()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P1A2-FAM-SELF");
        var residentUser = await AddUserAsync(dbContext, "p1a2-family-self@darak.test");
        var resident = await AddResidentProfileAsync(dbContext, residentUser.Id, compound.Id, "Self Resident");
        dbContext.FamilyMembers.Add(new FamilyMember
        {
            ResidentProfileId = resident.Id,
            FullName = "Family Member",
            Relationship = "Brother"
        });
        await dbContext.SaveChangesAsync();
        var service = CreateResidentService(dbContext, [], currentUserId: residentUser.Id);

        var result = await service.GetFamilyMembersAsync(resident.Id);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task ResidentEmergencyContacts_OutsideCompound_ReturnsNotFound()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "P1A2-EMG-A");
        var blocked = await AddCompoundAsync(dbContext, "P1A2-EMG-B");
        var blockedUser = await AddUserAsync(dbContext, "p1a2-emergency-blocked@darak.test");
        var blockedResident = await AddResidentProfileAsync(dbContext, blockedUser.Id, blocked.Id, "Blocked Emergency Resident");
        var service = CreateResidentService(dbContext, [allowed.Id]);

        var result = await service.GetEmergencyContactsAsync(blockedResident.Id);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task ResidentEmergencyContacts_CurrentResidentSelfAccess_ReturnsSuccess()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P1A2-EMG-SELF");
        var residentUser = await AddUserAsync(dbContext, "p1a2-emergency-self@darak.test");
        var resident = await AddResidentProfileAsync(dbContext, residentUser.Id, compound.Id, "Self Emergency Resident");
        dbContext.EmergencyContacts.Add(new EmergencyContact
        {
            ResidentProfileId = resident.Id,
            FullName = "Emergency Contact",
            Relationship = "Brother",
            PhoneNumber = "+9647222222222"
        });
        await dbContext.SaveChangesAsync();
        var service = CreateResidentService(dbContext, [], currentUserId: residentUser.Id);

        var result = await service.GetEmergencyContactsAsync(resident.Id);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value.Should().ContainSingle();
    }

    private static NotificationOutboxService CreateNotificationService(
        ApplicationDbContext dbContext,
        Guid[] allowedCompoundIds,
        bool isSuperAdmin = false)
    {
        return new NotificationOutboxService(
            dbContext,
            new FakeCompoundAccessService(allowedCompoundIds, isSuperAdmin: isSuperAdmin),
            new RecordingEmailSender(),
            new RecordingSmsSender(),
            Options.Create(new NotificationOptions { RetryDelayMinutes = 5 }));
    }

    private static CommercialCommunicationService CreateCommunicationService(
        ApplicationDbContext dbContext,
        Guid[] allowedCompoundIds,
        bool isSuperAdmin = false)
    {
        var compoundAccess = new FakeCompoundAccessService(allowedCompoundIds, isSuperAdmin: isSuperAdmin);
        return new CommercialCommunicationService(
            dbContext,
            compoundAccess,
            new AuditLogService(dbContext, compoundAccess, new HttpContextAccessor()));
    }

    private static ResidentService CreateResidentService(
        ApplicationDbContext dbContext,
        Guid[] allowedCompoundIds,
        Guid? currentUserId = null,
        bool isSuperAdmin = false)
    {
        return new ResidentService(
            dbContext,
            IdentityTestHelpers.CreateUserManager(dbContext),
            new FakeCompoundAccessService(allowedCompoundIds, isSuperAdmin: isSuperAdmin),
            currentUserId.HasValue ? new FakeCurrentUserService(currentUserId.Value) : null);
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

    private static async Task<Building> AddBuildingAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        string name,
        string code)
    {
        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompoundId = compoundId,
            Name = name,
            Code = code,
            NumberOfFloors = 3,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Buildings.Add(building);
        await dbContext.SaveChangesAsync();
        return building;
    }

    private static async Task<ApplicationUser> AddUserAsync(
        ApplicationDbContext dbContext,
        string email,
        string fullName = "Phase 1A-2 User")
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            FullName = fullName,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    private static async Task<ResidentProfile> AddResidentProfileAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        Guid compoundId,
        string fullName)
    {
        var resident = new ResidentProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompoundId = compoundId,
            FullName = fullName,
            PhoneNumber = "+9647000000000",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }

    private static async Task<PropertyUnit> AddPropertyUnitAsync(
        ApplicationDbContext dbContext,
        Guid compoundId)
    {
        var unit = new PropertyUnit
        {
            Id = Guid.NewGuid(),
            CompoundId = compoundId,
            UnitNumber = $"U-{Guid.NewGuid():N}"[..12],
            PropertyType = PropertyType.Villa,
            UnitStatus = UnitStatus.Available,
            AreaSquareMeters = 120,
            Bedrooms = 3,
            Bathrooms = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.PropertyUnits.Add(unit);
        await dbContext.SaveChangesAsync();
        return unit;
    }

    private static async Task<VisitorPass> AddVisitorPassAsync(
        ApplicationDbContext dbContext,
        Guid compoundId)
    {
        var residentUser = await AddUserAsync(dbContext, $"p1a2-visitor-{Guid.NewGuid():N}@darak.test");
        var resident = await AddResidentProfileAsync(dbContext, residentUser.Id, compoundId, "Visitor Resident");
        var unit = await AddPropertyUnitAsync(dbContext, compoundId);
        var pass = new VisitorPass
        {
            Id = Guid.NewGuid(),
            CompoundId = compoundId,
            ResidentProfileId = resident.Id,
            PropertyUnitId = unit.Id,
            VisitorName = "Visitor",
            VisitorPhoneNumber = "+9647111111111",
            VisitReason = "Test",
            AccessCode = $"VP-{Guid.NewGuid():N}"[..16],
            Status = VisitorPassStatus.Approved,
            ValidFrom = DateTime.UtcNow.AddHours(-1),
            ValidUntil = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            AccessLogs =
            [
                new VisitorAccessLog
                {
                    Action = VisitorAccessAction.CheckIn,
                    Notes = "Test log",
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
        dbContext.VisitorPasses.Add(pass);
        await dbContext.SaveChangesAsync();
        return pass;
    }

    private static async Task<MaintenanceRequest> AddMaintenanceRequestAsync(
        ApplicationDbContext dbContext,
        Guid compoundId)
    {
        var residentUser = await AddUserAsync(dbContext, $"p1a2-maint-{Guid.NewGuid():N}@darak.test");
        var resident = await AddResidentProfileAsync(dbContext, residentUser.Id, compoundId, "Maintenance Resident");
        var unit = await AddPropertyUnitAsync(dbContext, compoundId);
        var request = new MaintenanceRequest
        {
            Id = Guid.NewGuid(),
            CompoundId = compoundId,
            ResidentProfileId = resident.Id,
            PropertyUnitId = unit.Id,
            Title = "Maintenance request",
            Description = "Maintenance scope test.",
            Priority = MaintenancePriority.Medium,
            Status = MaintenanceStatus.Open,
            CreatedAt = DateTime.UtcNow,
            StatusHistory =
            [
                new MaintenanceStatusHistory
                {
                    NewStatus = MaintenanceStatus.Open,
                    Notes = "Created.",
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
        dbContext.MaintenanceRequests.Add(request);
        await dbContext.SaveChangesAsync();
        return request;
    }

    private sealed class FakeCurrentUserService(Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public Task<NotificationDeliveryResult> SendAsync(
            EmailNotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(NotificationDeliveryResult.Success("TestEmail", Guid.NewGuid().ToString()));
        }
    }

    private sealed class RecordingSmsSender : ISmsSender
    {
        public Task<NotificationDeliveryResult> SendAsync(
            SmsNotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(NotificationDeliveryResult.Success("TestSms", Guid.NewGuid().ToString()));
        }
    }
}
