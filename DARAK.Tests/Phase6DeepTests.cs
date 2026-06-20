using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.DTOs.Financial;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class Phase6DeepTests
{
    [Fact]
    public async Task ResidentConversationAccess_OtherResidentCannotReadOrSendMessages()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedPhase6WorldAsync(database);

        var opened = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentConversationAsync(
                seed.PrimaryResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.General,
                    IssueType = ConversationIssueType.GeneralInquiry,
                    InitialMessage = "I need help with my account."
                }));

        opened.Status.Should().Be(ServiceResultStatus.Success);
        var conversationId = opened.Value!.Id;

        var otherResidentRead = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.GetResidentConversationAsync(seed.OtherResidentUserId, conversationId));

        otherResidentRead.Status.Should().Be(ServiceResultStatus.NotFound);

        var otherResidentMessage = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.AddResidentMessageAsync(
                seed.OtherResidentUserId,
                conversationId,
                new SendConversationMessageRequest { Body = "I should not be able to reply here." }));

        otherResidentMessage.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task ClosedConversation_ResidentMustUseReopenWorkflow_AndSecondReopenEscalates()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedPhase6WorldAsync(database);

        var opened = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentConversationAsync(
                seed.PrimaryResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.Maintenance,
                    IssueType = ConversationIssueType.MaintenanceWaterLeak,
                    InitialMessage = "There is a water leak."
                }));
        opened.Status.Should().Be(ServiceResultStatus.Success);
        var conversationId = opened.Value!.Id;

        var firstClose = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.CloseConversationAsync(
                seed.AdminUserId,
                conversationId,
                new CompleteConversationRequest { Reason = "Handled by maintenance." }));
        firstClose.Status.Should().Be(ServiceResultStatus.Success);

        var blockedMessage = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.AddResidentMessageAsync(
                seed.PrimaryResidentUserId,
                conversationId,
                new SendConversationMessageRequest { Body = "The issue returned." }));
        blockedMessage.Status.Should().Be(ServiceResultStatus.Conflict);

        var firstReopen = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.ReopenResidentConversationAsync(
                seed.PrimaryResidentUserId,
                conversationId,
                new ReopenConversationRequest { Reason = "The issue returned once." }));
        firstReopen.Status.Should().Be(ServiceResultStatus.Success);
        firstReopen.Value!.ReopenCount.Should().Be(1);

        var secondClose = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.CloseConversationAsync(
                seed.AdminUserId,
                conversationId,
                new CompleteConversationRequest { Reason = "Handled again." }));
        secondClose.Status.Should().Be(ServiceResultStatus.Success);

        var secondReopen = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.ReopenResidentConversationAsync(
                seed.PrimaryResidentUserId,
                conversationId,
                new ReopenConversationRequest { Reason = "The issue returned again." }));

        secondReopen.Status.Should().Be(ServiceResultStatus.Success);
        secondReopen.Value!.ReopenCount.Should().Be(2);

        await using var verifyContext = TestDb.Create(database);
        var escalatedConversation = await verifyContext.Conversations
            .AsNoTracking()
            .SingleAsync(conversation => conversation.Id == conversationId);
        escalatedConversation.EscalationLevel.Should().Be(ConversationEscalationLevel.NeedsAttention);
        escalatedConversation.EscalationReason.Should().Be("Conversation reopened more than once.");

        var reopenEventsCount = await verifyContext.ActivityEvents.CountAsync(activityEvent =>
            activityEvent.EntityId == conversationId
            && activityEvent.EventType == ActivityEventType.ConversationReopened);
        reopenEventsCount.Should().Be(2);
    }

    [Fact]
    public async Task AdminScopedConversationQueries_DoNotLeakOutsideCompound()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedPhase6WorldAsync(database);

        var visible = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentConversationAsync(
                seed.PrimaryResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.Billing,
                    IssueType = ConversationIssueType.BillingHighAmount,
                    InitialMessage = "Primary compound issue."
                }));
        visible.Status.Should().Be(ServiceResultStatus.Success);

        var hidden = await ExecuteConversationAsync(
            database,
            seed.BlockedCompoundId,
            service => service.OpenResidentConversationAsync(
                seed.BlockedResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.Visitor,
                    IssueType = ConversationIssueType.VisitorDeniedEntry,
                    InitialMessage = "Blocked compound issue."
                }));
        hidden.Status.Should().Be(ServiceResultStatus.Success);

        var adminList = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.SearchAdminConversationsAsync(
                seed.AdminUserId,
                new ConversationSearchQuery { PageSize = 20 }));

        adminList.Status.Should().Be(ServiceResultStatus.Success);
        adminList.Value!.Items.Should().ContainSingle(item => item.Id == visible.Value!.Id);
        adminList.Value.Items.Should().NotContain(item => item.Id == hidden.Value!.Id);

        var hiddenDetails = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.GetAdminConversationDetailsAsync(seed.AdminUserId, hidden.Value!.Id));
        hiddenDetails.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task ResidentConversationView_HidesInternalNotesAndInternalSystemMessages()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedPhase6WorldAsync(database);

        var opened = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentConversationAsync(
                seed.PrimaryResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.Billing,
                    IssueType = ConversationIssueType.PaymentProofIssue,
                    InitialMessage = "My payment proof needs review."
                }));
        opened.Status.Should().Be(ServiceResultStatus.Success);
        var conversationId = opened.Value!.Id;

        var note = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.AddInternalNoteAsync(
                seed.AdminUserId,
                conversationId,
                new AddInternalNoteRequest { Body = "Finance must verify the reference number before replying." }));
        note.Status.Should().Be(ServiceResultStatus.Success);
        note.Value!.Messages.Should().Contain(message => message.MessageType == ConversationMessageType.InternalNote);

        var assignment = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.AssignConversationAsync(
                seed.AdminUserId,
                conversationId,
                new AssignConversationRequest
                {
                    AssignedToUserId = seed.AdminUserId,
                    Reason = "Finance verification."
                }));
        assignment.Status.Should().Be(ServiceResultStatus.Success);
        assignment.Value!.Messages.Should().Contain(message =>
            message.MessageType == ConversationMessageType.SystemMessage
            && message.Visibility == ConversationMessageVisibility.InternalOnly);

        var residentView = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.GetResidentConversationAsync(seed.PrimaryResidentUserId, conversationId));

        residentView.Status.Should().Be(ServiceResultStatus.Success);
        residentView.Value!.Messages.Should().NotContain(message => message.MessageType == ConversationMessageType.InternalNote);
        residentView.Value.Messages.Should().NotContain(message => message.Body.Contains("Finance verification."));
    }

    [Fact]
    public async Task BillDispute_DuplicateHandling_OnlyBlocksOpenDisputesForSameBill()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedPhase6WorldAsync(database);

        var first = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentBillDisputeAsync(
                seed.PrimaryResidentUserId,
                seed.PrimaryBillId,
                new ResidentBillDisputeRequest
                {
                    IssueType = ConversationIssueType.BillingHighAmount,
                    Message = "This bill is higher than usual."
                }));
        first.Status.Should().Be(ServiceResultStatus.Success);
        first.Value!.CreatedNew.Should().BeTrue();
        var firstConversationId = first.Value.ConversationId;

        var duplicate = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentBillDisputeAsync(
                seed.PrimaryResidentUserId,
                seed.PrimaryBillId,
                new ResidentBillDisputeRequest
                {
                    IssueType = ConversationIssueType.BillingMeterReadingIssue,
                    Message = "I am trying to open the same dispute again."
                }));
        duplicate.Status.Should().Be(ServiceResultStatus.Success);
        duplicate.Value!.CreatedNew.Should().BeFalse();
        duplicate.Value.ConversationId.Should().Be(firstConversationId);

        var closed = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.CloseConversationAsync(
                seed.AdminUserId,
                firstConversationId,
                new CompleteConversationRequest { Reason = "Finance review completed." }));
        closed.Status.Should().Be(ServiceResultStatus.Success);

        var afterClose = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentBillDisputeAsync(
                seed.PrimaryResidentUserId,
                seed.PrimaryBillId,
                new ResidentBillDisputeRequest
                {
                    IssueType = ConversationIssueType.BillingMeterReadingIssue,
                    Message = "The issue appeared again after closure."
                }));
        afterClose.Status.Should().Be(ServiceResultStatus.Success);
        afterClose.Value!.CreatedNew.Should().BeTrue();
        afterClose.Value.ConversationId.Should().NotBe(firstConversationId);

        await using var verifyContext = TestDb.Create(database);
        var disputeCount = await verifyContext.Conversations.CountAsync(conversation =>
            conversation.LinkedEntityType == ConversationLinkedEntityType.UtilityBill
            && conversation.LinkedEntityId == seed.PrimaryBillId);
        disputeCount.Should().Be(2);
    }

    [Fact]
    public async Task BillDispute_CannotUseAnotherResidentBill()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedPhase6WorldAsync(database);

        var result = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentBillDisputeAsync(
                seed.OtherResidentUserId,
                seed.PrimaryBillId,
                new ResidentBillDisputeRequest
                {
                    IssueType = ConversationIssueType.BillingHighAmount,
                    Message = "This bill does not belong to me."
                }));

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task TimelineFilters_RespectCompoundScopeAndSearchCriteria()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedPhase6WorldAsync(database);
        var now = DateTime.UtcNow;

        await using (var dbContext = TestDb.Create(database))
        {
            dbContext.ActivityEvents.AddRange(
                new ActivityEvent
                {
                    CompoundId = seed.PrimaryCompoundId,
                    ResidentProfileId = seed.PrimaryResidentProfileId,
                    PropertyUnitId = seed.PrimaryUnitId,
                    EventType = ActivityEventType.ConversationEscalated,
                    Title = "Visible escalation event",
                    Description = "Finance escalation visible to scoped admins.",
                    EntityType = ActivityEntityType.Conversation,
                    EntityId = seed.PrimaryConversationEntityId,
                    CreatedAtUtc = now.AddMinutes(-5)
                },
                new ActivityEvent
                {
                    CompoundId = seed.PrimaryCompoundId,
                    ResidentProfileId = seed.PrimaryResidentProfileId,
                    PropertyUnitId = seed.PrimaryUnitId,
                    EventType = ActivityEventType.PaymentCompleted,
                    Title = "Visible payment event",
                    Description = "Different event type should be filterable.",
                    EntityType = ActivityEntityType.Payment,
                    EntityId = Guid.NewGuid(),
                    CreatedAtUtc = now.AddMinutes(-4)
                },
                new ActivityEvent
                {
                    CompoundId = seed.BlockedCompoundId,
                    ResidentProfileId = seed.BlockedResidentProfileId,
                    PropertyUnitId = seed.BlockedUnitId,
                    EventType = ActivityEventType.ConversationEscalated,
                    Title = "Blocked escalation event",
                    Description = "This event must not leak.",
                    EntityType = ActivityEntityType.Conversation,
                    EntityId = Guid.NewGuid(),
                    CreatedAtUtc = now.AddMinutes(-3)
                });
            await dbContext.SaveChangesAsync();
        }

        await using var timelineContext = TestDb.Create(database);
        var timeline = new ActivityTimelineService(
            timelineContext,
            new FakeCompoundAccessService([seed.PrimaryCompoundId]));

        var result = await timeline.SearchRecentActivityAsync(new ActivityTimelineQuery
        {
            EventType = ActivityEventType.ConversationEscalated,
            EntityType = ActivityEntityType.Conversation,
            SearchTerm = "Finance escalation",
            FromUtc = now.AddMinutes(-10),
            ToUtc = now.AddMinutes(1),
            PageSize = 10
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.Items.Should().ContainSingle(item =>
            item.CompoundId == seed.PrimaryCompoundId
            && item.EntityId == seed.PrimaryConversationEntityId
            && item.EventType == ActivityEventType.ConversationEscalated);
        result.Value.Items.Should().NotContain(item => item.CompoundId == seed.BlockedCompoundId);
    }

    [Fact]
    public async Task FinancialHealth_AdminScopeBlocksOtherCompoundAndDashboardAccess()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedPhase6WorldAsync(database);

        await using var dbContext = TestDb.Create(database);
        var service = new ResidentFinancialHealthService(
            dbContext,
            new FakeCompoundAccessService([seed.PrimaryCompoundId]));

        var blockedResidentHealth = await service.GetAdminResidentFinancialHealthAsync(
            seed.AdminUserId,
            seed.BlockedResidentProfileId);
        blockedResidentHealth.Status.Should().Be(ServiceResultStatus.NotFound);

        var blockedDashboard = await service.GetDashboardSummaryAsync(
            seed.AdminUserId,
            new FinancialHealthDashboardQuery { CompoundId = seed.BlockedCompoundId });
        blockedDashboard.Status.Should().Be(ServiceResultStatus.Forbidden);
    }

    [Fact]
    public async Task AdminConversationDetails_ReturnsFinancialContextPanelAndRecentActivity()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedPhase6WorldAsync(database);

        var opened = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentConversationAsync(
                seed.PrimaryResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.Billing,
                    IssueType = ConversationIssueType.BillingHighAmount,
                    InitialMessage = "My bill needs review."
                }),
            includeFinancialHealth: true);

        opened.Status.Should().Be(ServiceResultStatus.Success);

        var details = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.GetAdminConversationDetailsAsync(seed.AdminUserId, opened.Value!.Id),
            includeFinancialHealth: true);

        details.Status.Should().Be(ServiceResultStatus.Success);
        var context = details.Value!.ResidentContext;
        context.ResidentProfileId.Should().Be(seed.PrimaryResidentProfileId);
        context.CurrentUnitId.Should().Be(seed.PrimaryUnitId);
        context.OutstandingAmount.Should().Be(1_250_000m);
        context.OverdueAmount.Should().Be(1_250_000m);
        context.FinancialHealthStatus.Should().Be(ResidentFinancialHealthStatus.Critical);
        context.FinancialHealthRiskReasons.Should().NotBeEmpty();
        context.RecentActivityEvents.Should().NotBeEmpty();
    }

    private static async Task<TResult> ExecuteConversationAsync<TResult>(
        TestDb.TestDatabase database,
        Guid allowedCompoundId,
        Func<ConversationService, Task<TResult>> action,
        bool includeFinancialHealth = false)
    {
        await using var dbContext = TestDb.Create(database);
        var access = new FakeCompoundAccessService([allowedCompoundId]);
        var financialHealthService = includeFinancialHealth
            ? new ResidentFinancialHealthService(dbContext, access)
            : null;
        var service = new ConversationService(
            dbContext,
            new ConversationAdvisoryService(),
            new ActivityTimelineService(dbContext, access),
            access,
            financialHealthService);

        return await action(service);
    }

    private static async Task<Phase6Seed> SeedPhase6WorldAsync(TestDb.TestDatabase database)
    {
        await using var dbContext = TestDb.Create(database);
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.Date);

        var primaryResidentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"primary-resident-{Guid.NewGuid():N}@test.local",
            Email = $"primary-resident-{Guid.NewGuid():N}@test.local",
            FullName = "Primary Resident"
        };

        var otherResidentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"other-resident-{Guid.NewGuid():N}@test.local",
            Email = $"other-resident-{Guid.NewGuid():N}@test.local",
            FullName = "Other Resident"
        };

        var blockedResidentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"blocked-resident-{Guid.NewGuid():N}@test.local",
            Email = $"blocked-resident-{Guid.NewGuid():N}@test.local",
            FullName = "Blocked Resident"
        };

        var adminUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"admin-{Guid.NewGuid():N}@test.local",
            Email = $"admin-{Guid.NewGuid():N}@test.local",
            FullName = "Admin User"
        };

        var primaryCompound = new Compound
        {
            Id = Guid.NewGuid(),
            Name = "Primary Darak",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };

        var blockedCompound = new Compound
        {
            Id = Guid.NewGuid(),
            Name = "Blocked Darak",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Mansour"
        };

        var primaryUnit = new PropertyUnit
        {
            Id = Guid.NewGuid(),
            CompoundId = primaryCompound.Id,
            UnitNumber = "A-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 120,
            Bedrooms = 2,
            Bathrooms = 2
        };

        var otherUnit = new PropertyUnit
        {
            Id = Guid.NewGuid(),
            CompoundId = primaryCompound.Id,
            UnitNumber = "A-102",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 110,
            Bedrooms = 2,
            Bathrooms = 1
        };

        var blockedUnit = new PropertyUnit
        {
            Id = Guid.NewGuid(),
            CompoundId = blockedCompound.Id,
            UnitNumber = "B-201",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 100,
            Bedrooms = 2,
            Bathrooms = 1
        };

        var primaryResident = new ResidentProfile
        {
            Id = Guid.NewGuid(),
            UserId = primaryResidentUser.Id,
            CompoundId = primaryCompound.Id,
            FullName = "Primary Resident"
        };

        var otherResident = new ResidentProfile
        {
            Id = Guid.NewGuid(),
            UserId = otherResidentUser.Id,
            CompoundId = primaryCompound.Id,
            FullName = "Other Resident"
        };

        var blockedResident = new ResidentProfile
        {
            Id = Guid.NewGuid(),
            UserId = blockedResidentUser.Id,
            CompoundId = blockedCompound.Id,
            FullName = "Blocked Resident"
        };

        var primaryOccupancy = new OccupancyRecord
        {
            Id = Guid.NewGuid(),
            ResidentProfileId = primaryResident.Id,
            CompoundId = primaryCompound.Id,
            PropertyUnitId = primaryUnit.Id,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = today.AddMonths(-6)
        };

        var otherOccupancy = new OccupancyRecord
        {
            Id = Guid.NewGuid(),
            ResidentProfileId = otherResident.Id,
            CompoundId = primaryCompound.Id,
            PropertyUnitId = otherUnit.Id,
            OccupancyType = OccupancyType.OwnerCash,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = today.AddMonths(-4)
        };

        var blockedOccupancy = new OccupancyRecord
        {
            Id = Guid.NewGuid(),
            ResidentProfileId = blockedResident.Id,
            CompoundId = blockedCompound.Id,
            PropertyUnitId = blockedUnit.Id,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = today.AddMonths(-2)
        };

        var primaryBillingCycle = new BillingCycle
        {
            Id = Guid.NewGuid(),
            CompoundId = primaryCompound.Id,
            Year = today.Year,
            Month = today.Month,
            PeriodStart = new DateOnly(today.Year, today.Month, 1),
            PeriodEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
            DueDate = today.AddDays(-20),
            IsClosed = false
        };

        var blockedBillingCycle = new BillingCycle
        {
            Id = Guid.NewGuid(),
            CompoundId = blockedCompound.Id,
            Year = today.Year,
            Month = today.Month,
            PeriodStart = new DateOnly(today.Year, today.Month, 1),
            PeriodEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
            DueDate = today.AddDays(15),
            IsClosed = false
        };

        var primaryBill = new UtilityBill
        {
            Id = Guid.NewGuid(),
            CompoundId = primaryCompound.Id,
            PropertyUnitId = primaryUnit.Id,
            ResidentProfileId = primaryResident.Id,
            BillingCycleId = primaryBillingCycle.Id,
            BillNumber = $"UTIL-{Guid.NewGuid():N}"[..18],
            BillStatus = BillStatus.Overdue,
            IssueDate = today.AddDays(-45),
            DueDate = today.AddDays(-35),
            SubtotalAmount = 1_250_000m,
            PreviousBalanceAmount = 0m,
            LateFeeAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 1_250_000m,
            PaidAmount = 0m
        };

        var otherResidentBill = new UtilityBill
        {
            Id = Guid.NewGuid(),
            CompoundId = primaryCompound.Id,
            PropertyUnitId = otherUnit.Id,
            ResidentProfileId = otherResident.Id,
            BillingCycleId = primaryBillingCycle.Id,
            BillNumber = $"UTIL-{Guid.NewGuid():N}"[..18],
            BillStatus = BillStatus.Unpaid,
            IssueDate = today.AddDays(-5),
            DueDate = today.AddDays(25),
            SubtotalAmount = 150_000m,
            PreviousBalanceAmount = 0m,
            LateFeeAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 150_000m,
            PaidAmount = 0m
        };

        var blockedBill = new UtilityBill
        {
            Id = Guid.NewGuid(),
            CompoundId = blockedCompound.Id,
            PropertyUnitId = blockedUnit.Id,
            ResidentProfileId = blockedResident.Id,
            BillingCycleId = blockedBillingCycle.Id,
            BillNumber = $"UTIL-{Guid.NewGuid():N}"[..18],
            BillStatus = BillStatus.Unpaid,
            IssueDate = today.AddDays(-3),
            DueDate = today.AddDays(20),
            SubtotalAmount = 200_000m,
            PreviousBalanceAmount = 0m,
            LateFeeAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 200_000m,
            PaidAmount = 0m
        };

        var adminAssignment = new UserCompoundAssignment
        {
            UserId = adminUser.Id,
            CompoundId = primaryCompound.Id,
            Role = UserRole.CompoundAdmin,
            IsActive = true
        };

        dbContext.Users.AddRange(primaryResidentUser, otherResidentUser, blockedResidentUser, adminUser);
        dbContext.Compounds.AddRange(primaryCompound, blockedCompound);
        dbContext.PropertyUnits.AddRange(primaryUnit, otherUnit, blockedUnit);
        dbContext.ResidentProfiles.AddRange(primaryResident, otherResident, blockedResident);
        dbContext.UserCompoundAssignments.Add(adminAssignment);
        dbContext.OccupancyRecords.AddRange(primaryOccupancy, otherOccupancy, blockedOccupancy);
        dbContext.BillingCycles.AddRange(primaryBillingCycle, blockedBillingCycle);
        dbContext.UtilityBills.AddRange(primaryBill, otherResidentBill, blockedBill);
        await dbContext.SaveChangesAsync();

        return new Phase6Seed(
            primaryCompound.Id,
            blockedCompound.Id,
            primaryUnit.Id,
            otherUnit.Id,
            blockedUnit.Id,
            primaryResident.Id,
            otherResident.Id,
            blockedResident.Id,
            primaryResidentUser.Id,
            otherResidentUser.Id,
            blockedResidentUser.Id,
            adminUser.Id,
            primaryBill.Id,
            otherResidentBill.Id,
            blockedBill.Id,
            Guid.NewGuid());
    }

    private sealed record Phase6Seed(
        Guid PrimaryCompoundId,
        Guid BlockedCompoundId,
        Guid PrimaryUnitId,
        Guid OtherUnitId,
        Guid BlockedUnitId,
        Guid PrimaryResidentProfileId,
        Guid OtherResidentProfileId,
        Guid BlockedResidentProfileId,
        Guid PrimaryResidentUserId,
        Guid OtherResidentUserId,
        Guid BlockedResidentUserId,
        Guid AdminUserId,
        Guid PrimaryBillId,
        Guid OtherResidentBillId,
        Guid BlockedBillId,
        Guid PrimaryConversationEntityId);
}
