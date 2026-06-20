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

public sealed class TimelineSupportDashboardTests
{
    [Fact]
    public async Task SupportDashboard_CountsOpenAssignedEscalatedBillingAndHighRiskConversations()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedSupportWorldAsync(database);

        var opened = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.OpenResidentConversationAsync(
                seed.ResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.Billing,
                    IssueType = ConversationIssueType.BillingHighAmount,
                    LinkedEntityType = ConversationLinkedEntityType.UtilityBill,
                    LinkedEntityId = seed.BillId,
                    InitialMessage = "This bill is too high."
                }));

        opened.Status.Should().Be(ServiceResultStatus.Success);
        var conversationId = opened.Value!.Id;

        var assigned = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.AssignConversationAsync(
                seed.AdminUserId,
                conversationId,
                new AssignConversationRequest
                {
                    AssignedToUserId = seed.AdminUserId,
                    Reason = "Finance owner."
                }));
        assigned.Status.Should().Be(ServiceResultStatus.Success);

        var priority = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.ChangePriorityAsync(
                seed.AdminUserId,
                conversationId,
                new ChangeConversationPriorityRequest
                {
                    Priority = ConversationPriority.Urgent,
                    Reason = "Potential financial dispute escalation."
                }));
        priority.Status.Should().Be(ServiceResultStatus.Success);

        var escalated = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.EscalateConversationAsync(
                seed.AdminUserId,
                conversationId,
                new EscalateConversationRequest
                {
                    EscalationLevel = ConversationEscalationLevel.EscalatedToSupervisor,
                    Reason = "Resident has an urgent billing dispute."
                }));
        escalated.Status.Should().Be(ServiceResultStatus.Success);

        var dashboard = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.GetSupportDashboardAsync(
                seed.AdminUserId,
                new SupportDashboardQuery { CompoundId = seed.CompoundId }));

        dashboard.Status.Should().Be(ServiceResultStatus.Success);
        var response = dashboard.Value!;
        response.OpenConversationsCount.Should().Be(1);
        response.UrgentConversationsCount.Should().Be(1);
        response.AssignedToMeCount.Should().Be(1);
        response.EscalatedConversationsCount.Should().Be(1);
        response.BillingDisputesCount.Should().Be(1);
        response.OldestOpenConversation!.Id.Should().Be(conversationId);
        response.HighRiskResidentsWithOpenConversations.Should().ContainSingle(item =>
            item.ResidentProfileId == seed.ResidentProfileId
            && item.RiskReasons.Any(reason => reason.Contains("urgent", StringComparison.OrdinalIgnoreCase))
            && item.RiskReasons.Any(reason => reason.Contains("escalated", StringComparison.OrdinalIgnoreCase)));

        var escalatedList = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.SearchAdminConversationsAsync(
                seed.AdminUserId,
                new ConversationSearchQuery { IsEscalated = true }));
        escalatedList.Value!.Items.Should().ContainSingle(item => item.Id == conversationId);

        await using var verifyContext = TestDb.Create(database);
        var eventTypes = await verifyContext.ActivityEvents
            .Where(item => item.EntityId == conversationId)
            .Select(item => item.EventType)
            .ToListAsync();
        eventTypes.Should().Contain(ActivityEventType.ConversationEscalated);
    }

    [Fact]
    public async Task ActivityTimeline_RecentResidentAndUnitTimelineRespectCompoundScope()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedSupportWorldAsync(database);
        var blockedCompoundId = Guid.NewGuid();
        var blockedResidentId = Guid.NewGuid();
        var blockedUnitId = Guid.NewGuid();

        await using (var dbContext = TestDb.Create(database))
        {
            var blockedCompound = new Compound
            {
                Id = blockedCompoundId,
                Name = "Other",
                Code = Guid.NewGuid().ToString("N")[..8],
                City = "Baghdad",
                Area = "Other"
            };
            var blockedUnit = new PropertyUnit
            {
                Id = blockedUnitId,
                CompoundId = blockedCompoundId,
                UnitNumber = "X-404",
                PropertyType = PropertyType.Apartment,
                UnitStatus = UnitStatus.Available,
                AreaSquareMeters = 80,
                Bedrooms = 1,
                Bathrooms = 1
            };
            var blockedUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"blocked-{Guid.NewGuid():N}@test.local",
                Email = $"blocked-{Guid.NewGuid():N}@test.local",
                FullName = "Blocked Resident"
            };
            var blockedResident = new ResidentProfile
            {
                Id = blockedResidentId,
                UserId = blockedUser.Id,
                CompoundId = blockedCompoundId,
                FullName = "Blocked Resident"
            };

            dbContext.Users.Add(blockedUser);
            dbContext.Compounds.Add(blockedCompound);
            dbContext.PropertyUnits.Add(blockedUnit);
            dbContext.ResidentProfiles.Add(blockedResident);
            dbContext.ActivityEvents.AddRange(
                new ActivityEvent
                {
                    CompoundId = seed.CompoundId,
                    ResidentProfileId = seed.ResidentProfileId,
                    PropertyUnitId = seed.UnitId,
                    EventType = ActivityEventType.ConversationOpened,
                    Title = "Visible resident event",
                    Description = "Visible unit activity.",
                    EntityType = ActivityEntityType.Conversation,
                    EntityId = Guid.NewGuid()
                },
                new ActivityEvent
                {
                    CompoundId = blockedCompoundId,
                    ResidentProfileId = blockedResidentId,
                    PropertyUnitId = blockedUnitId,
                    EventType = ActivityEventType.ConversationOpened,
                    Title = "Blocked event",
                    Description = "This should not leak.",
                    EntityType = ActivityEntityType.Conversation,
                    EntityId = Guid.NewGuid()
                });
            await dbContext.SaveChangesAsync();
        }

        await using var timelineContext = TestDb.Create(database);
        var service = new ActivityTimelineService(
            timelineContext,
            new FakeCompoundAccessService([seed.CompoundId]));

        var recent = await service.SearchRecentActivityAsync(new ActivityTimelineQuery { PageSize = 10 });
        recent.Status.Should().Be(ServiceResultStatus.Success);
        recent.Value!.Items.Should().ContainSingle(item => item.CompoundId == seed.CompoundId);
        recent.Value.Items.Should().NotContain(item => item.CompoundId == blockedCompoundId);

        var residentTimeline = await service.GetResidentTimelineAsync(
            seed.ResidentProfileId,
            new ActivityTimelineQuery { PageSize = 10 });
        residentTimeline.Status.Should().Be(ServiceResultStatus.Success);
        residentTimeline.Value!.Items.Should().ContainSingle(item => item.ResidentProfileId == seed.ResidentProfileId);

        var unitTimeline = await service.GetUnitTimelineAsync(
            seed.UnitId,
            new ActivityTimelineQuery { PageSize = 10 });
        unitTimeline.Status.Should().Be(ServiceResultStatus.Success);
        unitTimeline.Value!.Items.Should().ContainSingle(item => item.PropertyUnitId == seed.UnitId);

        var blockedResidentTimeline = await service.GetResidentTimelineAsync(
            blockedResidentId,
            new ActivityTimelineQuery { PageSize = 10 });
        blockedResidentTimeline.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    private static async Task<TResult> ExecuteConversationAsync<TResult>(
        TestDb.TestDatabase database,
        Guid allowedCompoundId,
        Func<ConversationService, Task<TResult>> action)
    {
        await using var dbContext = TestDb.Create(database);
        var service = new ConversationService(
            dbContext,
            new ConversationAdvisoryService(),
            new ActivityTimelineService(dbContext),
            new FakeCompoundAccessService([allowedCompoundId]));

        return await action(service);
    }

    private static async Task<SupportSeed> SeedSupportWorldAsync(TestDb.TestDatabase database)
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
            UnitNumber = "C-303",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 110,
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

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var billingCycle = new BillingCycle
        {
            CompoundId = compound.Id,
            Year = today.Year,
            Month = today.Month,
            PeriodStart = new DateOnly(today.Year, today.Month, 1),
            PeriodEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
            DueDate = today.AddDays(20),
            IsClosed = false
        };

        var bill = new UtilityBill
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            BillingCycleId = billingCycle.Id,
            BillNumber = $"UTIL-{Guid.NewGuid():N}"[..18],
            BillStatus = BillStatus.Unpaid,
            IssueDate = today,
            DueDate = today.AddDays(20),
            SubtotalAmount = 500_000m,
            PreviousBalanceAmount = 0m,
            LateFeeAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 500_000m,
            PaidAmount = 0m
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
        dbContext.BillingCycles.Add(billingCycle);
        dbContext.UtilityBills.Add(bill);
        dbContext.UserCompoundAssignments.Add(adminAssignment);
        await dbContext.SaveChangesAsync();

        return new SupportSeed(
            compound.Id,
            unit.Id,
            resident.Id,
            residentUser.Id,
            adminUser.Id,
            bill.Id);
    }

    private sealed record SupportSeed(
        Guid CompoundId,
        Guid UnitId,
        Guid ResidentProfileId,
        Guid ResidentUserId,
        Guid AdminUserId,
        Guid BillId);
}
