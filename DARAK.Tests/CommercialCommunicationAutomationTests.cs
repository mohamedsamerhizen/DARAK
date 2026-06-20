using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class CommercialCommunicationAutomationTests
{
    [Fact]
    public async Task UpdatePreferencesAsync_CreatesPreferenceAndWritesAudit()
    {
        await using var dbContext = TestDb.Create();
        var userId = Guid.NewGuid();
        var service = CreateService(dbContext, []);

        var result = await service.UpdatePreferencesAsync(userId, userId, new UpdateResidentNotificationPreferenceRequest
        {
            InAppEnabled = true,
            CampaignNotificationsEnabled = false,
            DoNotDisturbEnabled = true,
            DoNotDisturbStartLocalTime = new TimeSpan(22, 0, 0),
            DoNotDisturbEndLocalTime = new TimeSpan(7, 0, 0)
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.CampaignNotificationsEnabled.Should().BeFalse();
        dbContext.ResidentNotificationPreferences.Should().ContainSingle(item => item.UserId == userId);
        dbContext.AuditLogEntries.Should().ContainSingle(item => item.ActionType == AuditActionType.NotificationPreferenceUpdated);
    }

    [Fact]
    public async Task CreateCampaignAsync_ReturnsNotFoundOutsideCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "COM19-A");
        var blocked = await AddCompoundAsync(dbContext, "COM19-B");
        var service = CreateService(dbContext, [allowed.Id]);

        var result = await service.CreateCampaignAsync(Guid.NewGuid(), new CreateCommunicationCampaignRequest
        {
            CompoundId = blocked.Id,
            Title = "Blocked campaign",
            Body = "Blocked body",
            TargetType = CommunicationCampaignTargetType.Compound
        });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        dbContext.CommunicationCampaigns.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCampaignAsync_CreatesRecipientsResidentNotificationsOutboxAndAudit()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "COM19-SEND");
        var residentOne = await AddResidentAsync(dbContext, compound.Id, "Resident One");
        var residentTwo = await AddResidentAsync(dbContext, compound.Id, "Resident Two");
        var service = CreateService(dbContext, [compound.Id]);
        var userId = Guid.NewGuid();

        var created = await service.CreateCampaignAsync(userId, new CreateCommunicationCampaignRequest
        {
            CompoundId = compound.Id,
            Title = "Water outage",
            Body = "Water service will stop for maintenance.",
            TargetType = CommunicationCampaignTargetType.Compound,
            Priority = NotificationPriority.High
        });

        var sent = await service.SendCampaignAsync(userId, created.Value!.Id);

        sent.IsSuccess.Should().BeTrue(sent.Message);
        sent.Value!.RecipientCount.Should().Be(2);
        sent.Value.OutboxItemCount.Should().Be(2);
        dbContext.ResidentNotifications.Should().HaveCount(2);
        dbContext.NotificationOutboxes.Should().HaveCount(2);
        dbContext.CommunicationCampaignRecipients.Should().Contain(item => item.ResidentProfileId == residentOne.Id && !item.DeliverySuppressed);
        dbContext.CommunicationCampaignRecipients.Should().Contain(item => item.ResidentProfileId == residentTwo.Id && !item.DeliverySuppressed);
        dbContext.AuditLogEntries.Should().Contain(item => item.ActionType == AuditActionType.CommunicationCampaignSent);
    }

    [Fact]
    public async Task SendCampaignAsync_RespectsCampaignNotificationPreferenceSuppression()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "COM19-PREF");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Muted Resident");
        dbContext.ResidentNotificationPreferences.Add(new ResidentNotificationPreference
        {
            UserId = resident.UserId,
            InAppEnabled = true,
            CampaignNotificationsEnabled = false
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, [compound.Id]);
        var userId = Guid.NewGuid();

        var created = await service.CreateCampaignAsync(userId, new CreateCommunicationCampaignRequest
        {
            CompoundId = compound.Id,
            Title = "Muted campaign",
            Body = "Should be suppressed.",
            TargetType = CommunicationCampaignTargetType.Compound
        });

        var sent = await service.SendCampaignAsync(userId, created.Value!.Id);

        sent.IsSuccess.Should().BeTrue(sent.Message);
        sent.Value!.Recipients.Single().DeliverySuppressed.Should().BeTrue();
        sent.Value.OutboxItemCount.Should().Be(0);
        dbContext.NotificationOutboxes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDeliveryAnalyticsAsync_AggregatesCampaignRecipientsAndOutbox()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "COM19-AN");
        await AddResidentAsync(dbContext, compound.Id, "Analytics Resident");
        var service = CreateService(dbContext, [compound.Id]);
        var userId = Guid.NewGuid();
        var created = await service.CreateCampaignAsync(userId, new CreateCommunicationCampaignRequest
        {
            CompoundId = compound.Id,
            Title = "Analytics",
            Body = "Analytics body",
            TargetType = CommunicationCampaignTargetType.Compound
        });
        await service.SendCampaignAsync(userId, created.Value!.Id);

        var analytics = await service.GetDeliveryAnalyticsAsync(compound.Id);

        analytics.IsSuccess.Should().BeTrue(analytics.Message);
        analytics.Value!.CampaignCount.Should().Be(1);
        analytics.Value.SentCampaignCount.Should().Be(1);
        analytics.Value.TotalRecipientCount.Should().Be(1);
        analytics.Value.OutboxItemCount.Should().Be(1);
    }

    private static CommercialCommunicationService CreateService(ApplicationDbContext dbContext, Guid[] allowedCompoundIds)
    {
        var compoundAccess = new FakeCompoundAccessService(allowedCompoundIds);
        return new CommercialCommunicationService(
            dbContext,
            compoundAccess,
            new AuditLogService(dbContext, compoundAccess, new HttpContextAccessor()));
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string codePrefix)
    {
        var compound = new Compound
        {
            Name = codePrefix,
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Commercial"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<ResidentProfile> AddResidentAsync(ApplicationDbContext dbContext, Guid compoundId, string fullName)
    {
        var resident = new ResidentProfile
        {
            CompoundId = compoundId,
            UserId = Guid.NewGuid(),
            FullName = fullName,
            IsActive = true
        };

        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }
}
