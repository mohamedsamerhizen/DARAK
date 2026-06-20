using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class CommunicationFoundationTests
{
    [Fact]
    public void AdvisoryService_ReturnsOperationalFlagsForWaterLeak()
    {
        var service = new ConversationAdvisoryService();

        var priority = service.GetDefaultPriority(ConversationIssueType.MaintenanceWaterLeak);
        var flags = service.GetAdvisoryFlags(
            ConversationIssueType.MaintenanceWaterLeak,
            ConversationLinkedEntityType.MaintenanceRequest);

        priority.Should().Be(ConversationPriority.High);
        flags.Should().Contain(flag => flag.Severity == AdvisoryFlagSeverity.Critical);
        flags.Should().Contain(flag => flag.IsBlocking);
        flags.Should().Contain(flag => flag.Title == "Linked entity context");
    }

    [Fact]
    public async Task ActivityTimelineService_RecordAsync_PersistsActivityEvent()
    {
        await using var dbContext = TestDb.Create();
        var compound = new Compound
        {
            Name = "Darak",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();

        var service = new ActivityTimelineService(dbContext);

        var result = await service.RecordAsync(new RecordActivityEventRequest(
            compound.Id,
            ResidentProfileId: null,
            PropertyUnitId: null,
            ActorUserId: null,
            ActivityEventType.ConversationOpened,
            "Conversation opened",
            "A resident opened a support conversation.",
            ActivityEntityType.None,
            null));

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.CompoundId.Should().Be(compound.Id);
        result.Value.EventType.Should().Be(ActivityEventType.ConversationOpened);

        var stored = await dbContext.ActivityEvents.SingleAsync();
        stored.Title.Should().Be("Conversation opened");
    }

    [Fact]
    public async Task ConversationService_CreateConversationAsync_CreatesMessagesAndTimelineEvent()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedResidentAsync(dbContext);
        var service = new ConversationService(
            dbContext,
            new ConversationAdvisoryService(),
            new ActivityTimelineService(dbContext));

        var result = await service.CreateConversationAsync(new CreateConversationRequest(
            seed.CompoundId,
            seed.ResidentProfileId,
            PropertyUnitId: null,
            ConversationTopic.Billing,
            ConversationIssueType.BillingMeterReadingIssue,
            ConversationLinkedEntityType.None,
            null,
            "My meter reading looks wrong.",
            seed.UserId));

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.Status.Should().Be(ConversationStatus.PendingAdminReply);
        result.Value.Priority.Should().Be(ConversationPriority.Normal);
        result.Value.Messages.Should().HaveCount(2);
        result.Value.Messages.Should().Contain(message =>
            message.MessageType == ConversationMessageType.ResidentMessage
            && message.Visibility == ConversationMessageVisibility.ResidentVisible);
        result.Value.Messages.Should().Contain(message =>
            message.MessageType == ConversationMessageType.SystemMessage
            && message.Body == "Conversation opened.");

        (await dbContext.Conversations.CountAsync()).Should().Be(1);
        (await dbContext.ConversationMessages.CountAsync()).Should().Be(2);
        (await dbContext.ActivityEvents.CountAsync()).Should().Be(1);
        (await dbContext.ActivityEvents.SingleAsync()).EventType.Should().Be(ActivityEventType.ConversationOpened);
    }

    [Fact]
    public async Task ConversationService_CreateConversationAsync_RejectsLinkedEntityIdWithoutType()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedResidentAsync(dbContext);
        var service = new ConversationService(
            dbContext,
            new ConversationAdvisoryService(),
            new ActivityTimelineService(dbContext));

        var result = await service.CreateConversationAsync(new CreateConversationRequest(
            seed.CompoundId,
            seed.ResidentProfileId,
            PropertyUnitId: null,
            ConversationTopic.General,
            ConversationIssueType.GeneralInquiry,
            ConversationLinkedEntityType.None,
            Guid.NewGuid(),
            "Hello.",
            seed.UserId));

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task CreateConversationAsync_AdminActorOutsideCompound_ReturnsForbidden()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedResidentAsync(dbContext);
        var adminUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "admin@darak.test",
            Email = "admin@darak.test",
            FullName = "Admin User"
        };
        var adminRole = new IdentityRole<Guid>
        {
            Id = Guid.NewGuid(),
            Name = "CompoundAdmin",
            NormalizedName = "COMPOUNDADMIN"
        };
        dbContext.AddRange(
            adminUser,
            adminRole,
            new IdentityUserRole<Guid>
            {
                UserId = adminUser.Id,
                RoleId = adminRole.Id
            });
        await dbContext.SaveChangesAsync();

        var service = new ConversationService(
            dbContext,
            new ConversationAdvisoryService(),
            new ActivityTimelineService(dbContext),
            new FakeCompoundAccessService([]));

        var result = await service.CreateConversationAsync(new CreateConversationRequest(
            seed.CompoundId,
            seed.ResidentProfileId,
            PropertyUnitId: null,
            ConversationTopic.General,
            ConversationIssueType.GeneralInquiry,
            ConversationLinkedEntityType.None,
            null,
            "Admin-created conversation.",
            adminUser.Id));

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
    }

    private static async Task<ResidentSeed> SeedResidentAsync(DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        var compound = new Compound
        {
            Name = "Darak",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };

        var resident = new ResidentProfile
        {
            UserId = userId,
            CompoundId = compound.Id,
            FullName = "Resident One"
        };

        dbContext.Compounds.Add(compound);
        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();

        return new ResidentSeed(compound.Id, resident.Id, userId);
    }

    private sealed record ResidentSeed(Guid CompoundId, Guid ResidentProfileId, Guid UserId);
}
