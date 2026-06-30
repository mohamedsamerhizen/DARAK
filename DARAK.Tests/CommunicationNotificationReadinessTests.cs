using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class CommunicationNotificationReadinessTests
{
    [Fact]
    public async Task PublishAnnouncementAsync_QueuesNotificationsOnlyForTargetCompoundResidents()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "ANN-RDY-A");
        var blocked = await AddCompoundAsync(dbContext, "ANN-RDY-B");
        var allowedUnit = await AddUnitAsync(dbContext, allowed.Id, "A-101");
        var blockedUnit = await AddUnitAsync(dbContext, blocked.Id, "B-101");
        var allowedResident = await AddResidentWithOccupancyAsync(dbContext, allowed.Id, allowedUnit.Id, "Allowed Resident", OccupancyType.Tenant);
        var blockedResident = await AddResidentWithOccupancyAsync(dbContext, blocked.Id, blockedUnit.Id, "Blocked Resident", OccupancyType.Tenant);
        var service = CreateAnnouncementService(dbContext, allowed.Id);
        var created = await service.CreateAnnouncementAsync(Guid.NewGuid(), new CreateAnnouncementRequest
        {
            CompoundId = allowed.Id,
            Title = "Pool maintenance",
            Body = "The pool area will close tomorrow.",
            Category = AnnouncementCategory.General,
            Priority = AnnouncementPriority.High,
            Audience = AnnouncementAudience.AllResidents
        });

        var published = await service.PublishAnnouncementAsync(created.Value!.Id, new PublishAnnouncementRequest());

        published.IsSuccess.Should().BeTrue(published.Message);
        dbContext.ResidentNotifications.Should().ContainSingle(item => item.UserId == allowedResident.UserId);
        dbContext.ResidentNotifications.Should().NotContain(item => item.UserId == blockedResident.UserId);
        dbContext.NotificationOutboxes.Should().ContainSingle(item =>
            item.RecipientUserId == allowedResident.UserId
            && item.EventType == NotificationEventType.AnnouncementPublished
            && item.RelatedEntityType == NotificationRelatedEntityType.Announcement);
        dbContext.AuditLogEntries.Should().ContainSingle(item => item.ActionType == AuditActionType.AnnouncementPublished);
    }

    [Fact]
    public async Task PublishAnnouncementAsync_RespectsOptionalOptOutButEmergencyBypassesIt()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "ANN-RDY-PREF");
        var unit = await AddUnitAsync(dbContext, compound.Id, "P-101");
        var resident = await AddResidentWithOccupancyAsync(dbContext, compound.Id, unit.Id, "Muted Resident", OccupancyType.Tenant);
        dbContext.ResidentNotificationPreferences.Add(new ResidentNotificationPreference
        {
            UserId = resident.UserId,
            InAppEnabled = false,
            AnnouncementNotificationsEnabled = false
        });
        await dbContext.SaveChangesAsync();
        var service = CreateAnnouncementService(dbContext, compound.Id);

        var optional = await service.CreateAnnouncementAsync(Guid.NewGuid(), new CreateAnnouncementRequest
        {
            CompoundId = compound.Id,
            Title = "Garden update",
            Body = "Garden work continues this week.",
            Category = AnnouncementCategory.General,
            Priority = AnnouncementPriority.Normal,
            Audience = AnnouncementAudience.AllResidents
        });
        await service.PublishAnnouncementAsync(optional.Value!.Id, new PublishAnnouncementRequest());

        var emergency = await service.CreateAnnouncementAsync(Guid.NewGuid(), new CreateAnnouncementRequest
        {
            CompoundId = compound.Id,
            Title = "Emergency evacuation drill",
            Body = "Please follow the emergency instructions immediately.",
            Category = AnnouncementCategory.Emergency,
            Priority = AnnouncementPriority.Critical,
            Audience = AnnouncementAudience.AllResidents
        });
        await service.PublishAnnouncementAsync(emergency.Value!.Id, new PublishAnnouncementRequest());

        dbContext.ResidentNotifications.Should().ContainSingle(item =>
            item.UserId == resident.UserId
            && item.Title == "Emergency evacuation drill"
            && item.Severity == ResidentNotificationSeverity.Critical);
        dbContext.ResidentNotifications.Should().NotContain(item => item.Title == "Garden update");
        dbContext.NotificationOutboxes.Should().ContainSingle(item => item.Subject == "Emergency evacuation drill");
    }

    [Fact]
    public async Task CreateNotificationAsync_RejectsTargetUserOutsideCurrentCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "NTF-RDY-A");
        var blocked = await AddCompoundAsync(dbContext, "NTF-RDY-B");
        var allowedUser = await AddUserAsync(dbContext, "allowed@darak.test");
        var blockedUser = await AddUserAsync(dbContext, "blocked@darak.test");
        dbContext.ResidentProfiles.AddRange(
            new ResidentProfile
            {
                CompoundId = allowed.Id,
                UserId = allowedUser.Id,
                FullName = "Allowed Resident",
                IsActive = true
            },
            new ResidentProfile
            {
                CompoundId = blocked.Id,
                UserId = blockedUser.Id,
                FullName = "Blocked Resident",
                IsActive = true
            });
        await dbContext.SaveChangesAsync();
        var service = new ResidentNotificationService(dbContext, new FakeCompoundAccessService([allowed.Id]));

        var result = await service.CreateNotificationAsync(new CreateResidentNotificationRequest
        {
            UserId = blockedUser.Id,
            Title = "Blocked direct notification",
            Message = "This should not cross compounds.",
            Type = ResidentNotificationType.System,
            Severity = ResidentNotificationSeverity.Info
        });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        dbContext.ResidentNotifications.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchNotificationsAsync_ReturnsOnlyCurrentResidentsNotifications()
    {
        await using var dbContext = TestDb.Create();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        dbContext.ResidentNotifications.AddRange(
            new ResidentNotification
            {
                UserId = currentUserId,
                Title = "Visible",
                Message = "Current resident notification.",
                Type = ResidentNotificationType.System,
                Severity = ResidentNotificationSeverity.Info
            },
            new ResidentNotification
            {
                UserId = otherUserId,
                Title = "Hidden",
                Message = "Other resident notification.",
                Type = ResidentNotificationType.System,
                Severity = ResidentNotificationSeverity.Info
            });
        await dbContext.SaveChangesAsync();
        var service = new ResidentNotificationService(dbContext);

        var result = await service.SearchNotificationsAsync(new ResidentNotificationSearchQuery { PageSize = 20 }, currentUserId);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Items.Should().ContainSingle(item => item.Title == "Visible");
    }

    private static AnnouncementService CreateAnnouncementService(ApplicationDbContext dbContext, params Guid[] allowedCompoundIds)
    {
        var compoundAccess = new FakeCompoundAccessService(allowedCompoundIds);
        return new AnnouncementService(
            dbContext,
            compoundAccess,
            new AuditLogService(dbContext, compoundAccess, new HttpContextAccessor()));
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

    private static async Task<PropertyUnit> AddUnitAsync(ApplicationDbContext dbContext, Guid compoundId, string unitNumber)
    {
        var unit = new PropertyUnit
        {
            CompoundId = compoundId,
            UnitNumber = unitNumber,
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 100,
            Bedrooms = 2,
            Bathrooms = 1
        };
        dbContext.PropertyUnits.Add(unit);
        await dbContext.SaveChangesAsync();
        return unit;
    }

    private static async Task<ResidentProfile> AddResidentWithOccupancyAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid unitId,
        string fullName,
        OccupancyType occupancyType)
    {
        var resident = new ResidentProfile
        {
            CompoundId = compoundId,
            UserId = Guid.NewGuid(),
            FullName = fullName,
            PhoneNumber = "07700000000",
            IsActive = true
        };
        dbContext.ResidentProfiles.Add(resident);
        dbContext.OccupancyRecords.Add(new OccupancyRecord
        {
            CompoundId = compoundId,
            ResidentProfileId = resident.Id,
            PropertyUnitId = unitId,
            OccupancyType = occupancyType,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = new DateOnly(2026, 1, 1)
        });
        await dbContext.SaveChangesAsync();
        return resident;
    }

    private static async Task<ApplicationUser> AddUserAsync(ApplicationDbContext dbContext, string email)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            FullName = email,
            EmailConfirmed = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }
}
