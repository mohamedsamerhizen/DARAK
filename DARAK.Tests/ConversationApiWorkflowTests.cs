using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class ConversationApiWorkflowTests
{
    [Fact]
    public async Task ResidentWorkflow_OpenListSendCloseReopen_UpdatesStatusAndTimeline()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedConversationWorldAsync(database);

        var opened = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.OpenResidentConversationAsync(
                seed.ResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.Maintenance,
                    IssueType = ConversationIssueType.MaintenanceWaterLeak,
                    InitialMessage = "There is a leak in my kitchen."
                }));

        opened.Status.Should().Be(ServiceResultStatus.Success);
        var openedConversation = opened.Value!;
        var openedConversationId = openedConversation.Id;
        openedConversation.PropertyUnitId.Should().Be(seed.UnitId);
        openedConversation.Priority.Should().Be(ConversationPriority.High);
        openedConversation.Status.Should().Be(ConversationStatus.PendingAdminReply);

        var residentList = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.SearchResidentConversationsAsync(
                seed.ResidentUserId,
                new ConversationSearchQuery()));
        residentList.Value!.Items.Should().ContainSingle(item => item.Id == openedConversationId);

        var residentReply = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.AddResidentMessageAsync(
                seed.ResidentUserId,
                openedConversationId,
                new SendConversationMessageRequest { Body = "The water is still running." }));
        residentReply.Status.Should().Be(ServiceResultStatus.Success);
        residentReply.Value!.Status.Should().Be(ConversationStatus.PendingAdminReply);

        var closed = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.CloseConversationAsync(
                seed.AdminUserId,
                openedConversationId,
                new CompleteConversationRequest { Reason = "Handled by support." }));
        closed.Status.Should().Be(ServiceResultStatus.Success);
        closed.Value!.Status.Should().Be(ConversationStatus.Closed);

        var blockedMessage = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.AddResidentMessageAsync(
                seed.ResidentUserId,
                openedConversationId,
                new SendConversationMessageRequest { Body = "I need more help." }));
        blockedMessage.Status.Should().Be(ServiceResultStatus.Conflict);

        var reopened = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.ReopenResidentConversationAsync(
                seed.ResidentUserId,
                openedConversationId,
                new ReopenConversationRequest { Reason = "The leak returned." }));
        reopened.Status.Should().Be(ServiceResultStatus.Success);
        var reopenedConversation = reopened.Value!;
        reopenedConversation.Status.Should().Be(ConversationStatus.Reopened);
        reopenedConversation.ReopenCount.Should().Be(1);
        reopenedConversation.LastReopenReason.Should().Be("The leak returned.");
        reopenedConversation.Messages.Should().Contain(message =>
            message.MessageType == ConversationMessageType.SystemMessage
            && message.Body.Contains("Conversation reopened by resident"));

        await using var verifyContext = TestDb.Create(database);
        var eventTypes = await verifyContext.ActivityEvents
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => item.EventType)
            .ToListAsync();
        eventTypes.Should().Contain(ActivityEventType.ConversationOpened);
        eventTypes.Should().Contain(ActivityEventType.ConversationClosed);
        eventTypes.Should().Contain(ActivityEventType.ConversationReopened);
    }

    [Fact]
    public async Task AdminWorkflow_InternalNotesAndAdvisoryFlags_AreHiddenFromResident()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedConversationWorldAsync(database);
        var opened = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => OpenBillingConversationAsync(service, seed));
        opened.Status.Should().Be(ServiceResultStatus.Success);
        var openedConversationId = opened.Value!.Id;

        var note = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.AddInternalNoteAsync(
                seed.AdminUserId,
                openedConversationId,
                new AddInternalNoteRequest { Body = "Check previous meter photo before replying." }));
        note.Status.Should().Be(ServiceResultStatus.Success);
        var noteConversation = note.Value!;
        noteConversation.Messages.Should().Contain(message =>
            message.MessageType == ConversationMessageType.InternalNote
            && message.Visibility == ConversationMessageVisibility.InternalOnly);
        noteConversation.Messages.Should().Contain(message =>
            message.MessageType == ConversationMessageType.SystemMessage
            && message.Body == "Internal note added.");

        var adminDetails = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.GetAdminConversationDetailsAsync(
                seed.AdminUserId,
                openedConversationId));
        adminDetails.Status.Should().Be(ServiceResultStatus.Success);
        var adminConversationDetails = adminDetails.Value!;
        adminConversationDetails.AdvisoryFlags.Should().Contain(flag => flag.IsBlocking);
        adminConversationDetails.ResidentContext.ResidentName.Should().Be("Resident One");
        adminConversationDetails.Conversation.Messages.Should().Contain(message =>
            message.Visibility == ConversationMessageVisibility.InternalOnly);

        var residentDetails = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.GetResidentConversationAsync(
                seed.ResidentUserId,
                openedConversationId));
        residentDetails.Status.Should().Be(ServiceResultStatus.Success);
        var residentConversationDetails = residentDetails.Value!;
        residentConversationDetails.Messages.Should().NotContain(message =>
            message.MessageType == ConversationMessageType.InternalNote);
        residentConversationDetails.Messages.Should().NotContain(message =>
            message.Body.Contains("Check previous meter photo"));
    }

    [Fact]
    public async Task AdminWorkflow_AssignTransferChangePriorityAndReply_RecordsSystemMessages()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedConversationWorldAsync(database);
        var opened = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => OpenBillingConversationAsync(service, seed));
        opened.Status.Should().Be(ServiceResultStatus.Success);
        var openedConversationId = opened.Value!.Id;

        var assigned = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.AssignConversationAsync(
                seed.AdminUserId,
                openedConversationId,
                new AssignConversationRequest
                {
                    AssignedToUserId = seed.AdminUserId,
                    Reason = "Finance support owner."
                }));
        assigned.Status.Should().Be(ServiceResultStatus.Success);
        var assignedConversation = assigned.Value!;
        assignedConversation.AssignedToUserId.Should().Be(seed.AdminUserId);
        assignedConversation.Messages.Should().Contain(message =>
            message.MessageType == ConversationMessageType.SystemMessage
            && message.Visibility == ConversationMessageVisibility.InternalOnly
            && message.Body.Contains("Conversation assigned"));

        var priority = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.ChangePriorityAsync(
                seed.AdminUserId,
                openedConversationId,
                new ChangeConversationPriorityRequest
                {
                    Priority = ConversationPriority.High,
                    Reason = "Resident is waiting on bill review."
                }));
        priority.Status.Should().Be(ServiceResultStatus.Success);
        var priorityConversation = priority.Value!;
        priorityConversation.Priority.Should().Be(ConversationPriority.High);
        priorityConversation.Messages.Should().Contain(message =>
            message.Body.Contains("Priority changed from Normal to High"));

        var reply = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.AddAdminReplyAsync(
                seed.AdminUserId,
                openedConversationId,
                new SendConversationMessageRequest { Body = "We are reviewing the meter history." }));
        reply.Status.Should().Be(ServiceResultStatus.Success);
        var replyConversation = reply.Value!;
        replyConversation.Status.Should().Be(ConversationStatus.PendingResidentReply);
        replyConversation.Messages.Should().Contain(message => message.MessageType == ConversationMessageType.AdminMessage);

        var assignedToMe = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.SearchAdminConversationsAsync(
                seed.AdminUserId,
                new ConversationSearchQuery { AssignedToUserId = seed.AdminUserId }));
        assignedToMe.Value!.Items.Should().ContainSingle(item => item.Id == openedConversationId);

        var unassigned = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.SearchAdminConversationsAsync(
                seed.AdminUserId,
                new ConversationSearchQuery { IsUnassigned = true }));
        unassigned.Value!.Items.Should().NotContain(item => item.Id == openedConversationId);
    }

    [Fact]
    public async Task AdminWorkflow_CompoundScope_PreventsReadingOutsideAssignedCompound()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedConversationWorldAsync(database);
        var opened = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => OpenBillingConversationAsync(service, seed));
        opened.Status.Should().Be(ServiceResultStatus.Success);
        var openedConversationId = opened.Value!.Id;

        var blockedCompoundId = Guid.NewGuid();

        var list = await ExecuteConversationAsync(
            database,
            blockedCompoundId,
            service => service.SearchAdminConversationsAsync(
                seed.AdminUserId,
                new ConversationSearchQuery()));
        list.Status.Should().Be(ServiceResultStatus.Success);
        list.Value!.Items.Should().BeEmpty();

        var details = await ExecuteConversationAsync(
            database,
            blockedCompoundId,
            service => service.GetAdminConversationDetailsAsync(
                seed.AdminUserId,
                openedConversationId));
        details.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    private static async Task<TResult> ExecuteConversationAsync<TResult>(
        TestDb.TestDatabase database,
        Guid allowedCompoundId,
        Func<ConversationService, Task<TResult>> action)
    {
        await using var dbContext = TestDb.Create(database);
        var service = CreateService(dbContext, allowedCompoundId);
        return await action(service);
    }

    private static ConversationService CreateService(ApplicationDbContext dbContext, Guid allowedCompoundId)
    {
        return new ConversationService(
            dbContext,
            new ConversationAdvisoryService(),
            new ActivityTimelineService(dbContext),
            new FakeCompoundAccessService([allowedCompoundId]));
    }

    private static Task<ServiceResult<ResidentConversationResponse>> OpenBillingConversationAsync(
        ConversationService service,
        ConversationSeed seed)
    {
        return service.OpenResidentConversationAsync(
            seed.ResidentUserId,
            new ResidentOpenConversationRequest
            {
                Topic = ConversationTopic.Billing,
                IssueType = ConversationIssueType.BillingMeterReadingIssue,
                InitialMessage = "The meter reading does not match my bill."
            });
    }

    private static async Task<ConversationSeed> SeedConversationWorldAsync(TestDb.TestDatabase database)
    {
        await using var dbContext = TestDb.Create(database);
        var residentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"resident-{Guid.NewGuid():N}@test.local",
            Email = $"resident-{Guid.NewGuid():N}@test.local",
            FullName = "Resident One"
        };

        var adminUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"admin-{Guid.NewGuid():N}@test.local",
            Email = $"admin-{Guid.NewGuid():N}@test.local",
            FullName = "Admin One"
        };

        var compound = new Compound
        {
            Name = "Darak",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };

        var unit = new PropertyUnit
        {
            CompoundId = compound.Id,
            UnitNumber = "A-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 120,
            Bedrooms = 2,
            Bathrooms = 2
        };

        var resident = new ResidentProfile
        {
            UserId = residentUser.Id,
            CompoundId = compound.Id,
            FullName = "Resident One"
        };

        var occupancy = new OccupancyRecord
        {
            ResidentProfileId = resident.Id,
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddMonths(-2))
        };

        var adminAssignment = new UserCompoundAssignment
        {
            UserId = adminUser.Id,
            CompoundId = compound.Id,
            Role = UserRole.CompoundAdmin,
            IsActive = true
        };

        dbContext.Users.AddRange(residentUser, adminUser);
        dbContext.Compounds.Add(compound);
        dbContext.PropertyUnits.Add(unit);
        dbContext.ResidentProfiles.Add(resident);
        dbContext.OccupancyRecords.Add(occupancy);
        dbContext.UserCompoundAssignments.Add(adminAssignment);
        await dbContext.SaveChangesAsync();

        return new ConversationSeed(
            compound.Id,
            unit.Id,
            resident.Id,
            residentUser.Id,
            adminUser.Id);
    }

    private sealed record ConversationSeed(
        Guid CompoundId,
        Guid UnitId,
        Guid ResidentProfileId,
        Guid ResidentUserId,
        Guid AdminUserId);
}
