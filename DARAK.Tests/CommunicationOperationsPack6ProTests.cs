using DARAK.Api.Data;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class CommunicationOperationsPack6ProTests
{
    [Fact]
    public async Task GetCommunicationCommandCenterAsync_SummarizesCriticalCommunicationWork()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P6-CC");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Pack Six Resident");
        var service = CreateService(dbContext, compound.Id);

        dbContext.Announcements.Add(new Announcement
        {
            CompoundId = compound.Id,
            Title = "Critical water outage",
            Body = "Critical water outage body.",
            Category = AnnouncementCategory.Utility,
            Priority = AnnouncementPriority.Critical,
            Status = AnnouncementStatus.Published,
            PublishedAt = DateTime.UtcNow.AddHours(-1),
            IsActive = true,
            IsPinned = true
        });
        dbContext.UtilityOutages.Add(new UtilityOutage
        {
            CompoundId = compound.Id,
            ServiceType = UtilityOutageServiceType.Water,
            AffectedScope = UtilityOutageAffectedScope.Compound,
            Status = UtilityOutageStatus.Active,
            Severity = UtilityOutageSeverity.Critical,
            Title = "Critical outage",
            Description = "Outage body.",
            EstimatedStartAtUtc = DateTime.UtcNow.AddHours(-6),
            EstimatedEndAtUtc = DateTime.UtcNow.AddHours(-1)
        });
        dbContext.Conversations.Add(new Conversation
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            Status = ConversationStatus.PendingAdminReply,
            Priority = ConversationPriority.Urgent,
            Topic = ConversationTopic.Maintenance,
            IssueType = ConversationIssueType.MaintenanceWaterLeak,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-10),
            LastMessageAtUtc = DateTime.UtcNow.AddHours(-6)
        });
        dbContext.NotificationOutboxes.Add(new NotificationOutbox
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            RecipientUserId = resident.UserId,
            Channel = NotificationChannel.InApp,
            Status = NotificationStatus.Failed,
            Priority = NotificationPriority.Urgent,
            RecipientName = resident.FullName,
            Subject = "Failed",
            Body = "Failed body."
        });
        await dbContext.SaveChangesAsync();

        var result = await service.GetCommunicationCommandCenterAsync(compound.Id);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.CriticalAnnouncementCount.Should().Be(1);
        result.Value.CriticalOutageCount.Should().Be(1);
        result.Value.OverdueOutageCount.Should().Be(1);
        result.Value.FailedOutboxItemCount.Should().Be(1);
        result.Value.UrgentConversationCount.Should().Be(1);
        result.Value.OverallRiskLevel.Should().Be("Critical");
    }

    [Fact]
    public async Task GetAnnouncementAcknowledgementBoardAsync_TracksAcknowledgementGap()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P6-ACK");
        var firstResident = await AddResidentAsync(dbContext, compound.Id, "Ack One");
        await AddResidentAsync(dbContext, compound.Id, "Ack Two");
        var announcement = new Announcement
        {
            CompoundId = compound.Id,
            Title = "Critical announcement",
            Body = "Critical announcement body.",
            Category = AnnouncementCategory.Emergency,
            Priority = AnnouncementPriority.Critical,
            Status = AnnouncementStatus.Published,
            PublishedAt = DateTime.UtcNow.AddMinutes(-30),
            IsActive = true,
            IsPinned = true
        };
        dbContext.Announcements.Add(announcement);
        dbContext.AnnouncementReadReceipts.Add(new AnnouncementReadReceipt
        {
            AnnouncementId = announcement.Id,
            UserId = firstResident.UserId,
            ReadAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetAnnouncementAcknowledgementBoardAsync(compound.Id);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.TotalExpectedAcknowledgements.Should().Be(2);
        result.Value.TotalAcknowledgedCount.Should().Be(1);
        result.Value.TotalMissingAcknowledgementCount.Should().Be(1);
        result.Value.Items.Single().RiskLevel.Should().Be("Critical");
    }

    [Fact]
    public async Task GetUtilityOutageOperationsBoardAsync_FlagsOverdueOutage()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P6-OUT");
        dbContext.UtilityOutages.Add(new UtilityOutage
        {
            CompoundId = compound.Id,
            ServiceType = UtilityOutageServiceType.Electricity,
            AffectedScope = UtilityOutageAffectedScope.Compound,
            Status = UtilityOutageStatus.Active,
            Severity = UtilityOutageSeverity.High,
            Title = "Electricity outage",
            Description = "Electricity outage body.",
            EstimatedStartAtUtc = DateTime.UtcNow.AddHours(-5),
            EstimatedEndAtUtc = DateTime.UtcNow.AddHours(-1),
            RecipientCount = 12,
            OutboxItemCount = 12
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetUtilityOutageOperationsBoardAsync(compound.Id);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.ActiveOutageCount.Should().Be(1);
        result.Value.OverdueOutageCount.Should().Be(1);
        result.Value.Items.Single().IsOverdue.Should().BeTrue();
        result.Value.Items.Single().OperationalRisk.Should().Be("Critical");
    }

    [Fact]
    public async Task GetCommunicationResponseIntelligenceAsync_FlagsStalePendingAdminReply()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P6-SLA");
        var resident = await AddResidentAsync(dbContext, compound.Id, "SLA Resident");
        dbContext.Conversations.Add(new Conversation
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            Status = ConversationStatus.PendingAdminReply,
            Priority = ConversationPriority.High,
            Topic = ConversationTopic.Billing,
            IssueType = ConversationIssueType.BillingHighAmount,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-30),
            LastMessageAtUtc = DateTime.UtcNow.AddHours(-18),
            LastResidentMessageAtUtc = DateTime.UtcNow.AddHours(-18)
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetCommunicationResponseIntelligenceAsync(compound.Id);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.PendingAdminReplyCount.Should().Be(1);
        result.Value.StaleConversationCount.Should().Be(1);
        result.Value.StaleItems.Single().SlaRisk.Should().Be("Warning");
    }

    private static ResidentCommunicationOperationsService CreateService(ApplicationDbContext dbContext, params Guid[] allowedCompoundIds)
    {
        return new ResidentCommunicationOperationsService(dbContext, new FakeCompoundAccessService(allowedCompoundIds));
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string codePrefix)
    {
        var compound = new Compound
        {
            Name = codePrefix,
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Operations"
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
