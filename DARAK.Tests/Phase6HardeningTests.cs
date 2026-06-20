using DARAK.Api.Controllers;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class Phase6HardeningTests
{
    [Fact]
    public async Task ResidentConversationSearch_DoesNotMatchInternalOnlyMessages()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedHardeningWorldAsync(database);

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
                seed.PrimaryAdminUserId,
                conversationId,
                new AddInternalNoteRequest { Body = "hidden-finance-risk-term" }));
        note.Status.Should().Be(ServiceResultStatus.Success);

        var residentSearch = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.SearchResidentConversationsAsync(
                seed.PrimaryResidentUserId,
                new ConversationSearchQuery { SearchTerm = "hidden-finance-risk-term" }));
        residentSearch.Status.Should().Be(ServiceResultStatus.Success);
        residentSearch.Value!.TotalCount.Should().Be(0);

        var adminSearch = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.SearchAdminConversationsAsync(
                seed.PrimaryAdminUserId,
                new ConversationSearchQuery { SearchTerm = "hidden-finance-risk-term" }));
        adminSearch.Status.Should().Be(ServiceResultStatus.Success);
        adminSearch.Value!.Items.Should().ContainSingle(item => item.Id == conversationId);
    }

    [Fact]
    public async Task ResidentConversationSearch_IgnoresAdminOnlyAssignmentAndEscalationFilters()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedHardeningWorldAsync(database);

        var opened = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentConversationAsync(
                seed.PrimaryResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.Billing,
                    IssueType = ConversationIssueType.BillingHighAmount,
                    InitialMessage = "Please review this bill."
                }));
        opened.Status.Should().Be(ServiceResultStatus.Success);
        var conversationId = opened.Value!.Id;

        var assigned = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.AssignConversationAsync(
                seed.PrimaryAdminUserId,
                conversationId,
                new AssignConversationRequest { AssignedToUserId = seed.PrimaryStaffUserId }));
        assigned.Status.Should().Be(ServiceResultStatus.Success);

        var escalated = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.EscalateConversationAsync(
                seed.PrimaryAdminUserId,
                conversationId,
                new EscalateConversationRequest
                {
                    EscalationLevel = ConversationEscalationLevel.NeedsAttention,
                    Reason = "Internal escalation reason."
                }));
        escalated.Status.Should().Be(ServiceResultStatus.Success);

        var normalSearch = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.SearchResidentConversationsAsync(
                seed.PrimaryResidentUserId,
                new ConversationSearchQuery()));

        var adminOnlyFilterSearch = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.SearchResidentConversationsAsync(
                seed.PrimaryResidentUserId,
                new ConversationSearchQuery
                {
                    AssignedToUserId = Guid.NewGuid(),
                    IsUnassigned = true,
                    IsEscalated = false,
                    EscalationLevel = ConversationEscalationLevel.None
                }));

        normalSearch.Status.Should().Be(ServiceResultStatus.Success);
        adminOnlyFilterSearch.Status.Should().Be(ServiceResultStatus.Success);
        normalSearch.Value!.Items.Should().ContainSingle(item => item.Id == conversationId);
        adminOnlyFilterSearch.Value!.Items.Should().ContainSingle(item => item.Id == conversationId);
    }

    [Fact]
    public async Task ResidentOpenConversation_ValidatesLinkedEntityOwnership()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedHardeningWorldAsync(database);

        var otherResidentBill = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentConversationAsync(
                seed.PrimaryResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.Billing,
                    IssueType = ConversationIssueType.BillingHighAmount,
                    LinkedEntityType = ConversationLinkedEntityType.UtilityBill,
                    LinkedEntityId = seed.OtherResidentBillId,
                    InitialMessage = "I should not link another resident bill."
                }));
        otherResidentBill.Status.Should().Be(ServiceResultStatus.NotFound);

        var blockedCompoundBill = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentConversationAsync(
                seed.PrimaryResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.Billing,
                    IssueType = ConversationIssueType.BillingHighAmount,
                    LinkedEntityType = ConversationLinkedEntityType.UtilityBill,
                    LinkedEntityId = seed.BlockedCompoundBillId,
                    InitialMessage = "I should not link a bill from another compound."
                }));
        blockedCompoundBill.Status.Should().Be(ServiceResultStatus.NotFound);

        var ownBill = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentConversationAsync(
                seed.PrimaryResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.Billing,
                    IssueType = ConversationIssueType.BillingHighAmount,
                    LinkedEntityType = ConversationLinkedEntityType.UtilityBill,
                    LinkedEntityId = seed.PrimaryBillId,
                    InitialMessage = "This is my bill."
                }));
        ownBill.Status.Should().Be(ServiceResultStatus.Success);
        ownBill.Value!.LinkedEntityId.Should().Be(seed.PrimaryBillId);
    }

    [Fact]
    public async Task AssignConversation_RequiresInternalStaffWithCompoundAccess()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedHardeningWorldAsync(database);

        var opened = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentConversationAsync(
                seed.PrimaryResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.General,
                    IssueType = ConversationIssueType.GeneralInquiry,
                    InitialMessage = "I need help."
                }));
        opened.Status.Should().Be(ServiceResultStatus.Success);
        var conversationId = opened.Value!.Id;

        var assignToResident = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.AssignConversationAsync(
                seed.PrimaryAdminUserId,
                conversationId,
                new AssignConversationRequest { AssignedToUserId = seed.PrimaryResidentUserId }));
        assignToResident.Status.Should().Be(ServiceResultStatus.BadRequest);

        var assignToOutOfScopeStaff = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.AssignConversationAsync(
                seed.PrimaryAdminUserId,
                conversationId,
                new AssignConversationRequest { AssignedToUserId = seed.BlockedStaffUserId }));
        assignToOutOfScopeStaff.Status.Should().Be(ServiceResultStatus.Forbidden);

        var assignToScopedStaff = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.AssignConversationAsync(
                seed.PrimaryAdminUserId,
                conversationId,
                new AssignConversationRequest { AssignedToUserId = seed.PrimaryStaffUserId }));
        assignToScopedStaff.Status.Should().Be(ServiceResultStatus.Success);
        assignToScopedStaff.Value!.AssignedToUserId.Should().Be(seed.PrimaryStaffUserId);
    }

    [Fact]
    public async Task ActivityTimeline_RecordAsync_RejectsCrossCompoundResidentUnitAndEntity()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedHardeningWorldAsync(database);

        await using var dbContext = TestDb.Create(database);
        var service = new ActivityTimelineService(
            dbContext,
            new FakeCompoundAccessService([seed.PrimaryCompoundId]));

        var wrongResident = await service.RecordAsync(new RecordActivityEventRequest(
            seed.PrimaryCompoundId,
            seed.BlockedResidentProfileId,
            null,
            seed.PrimaryAdminUserId,
            ActivityEventType.ConversationOpened,
            "Wrong resident",
            "This resident belongs to another compound.",
            ActivityEntityType.None,
            null));
        wrongResident.Status.Should().Be(ServiceResultStatus.BadRequest);

        var wrongUnit = await service.RecordAsync(new RecordActivityEventRequest(
            seed.PrimaryCompoundId,
            null,
            seed.BlockedUnitId,
            seed.PrimaryAdminUserId,
            ActivityEventType.ConversationOpened,
            "Wrong unit",
            "This unit belongs to another compound.",
            ActivityEntityType.None,
            null));
        wrongUnit.Status.Should().Be(ServiceResultStatus.BadRequest);

        var wrongEntity = await service.RecordAsync(new RecordActivityEventRequest(
            seed.PrimaryCompoundId,
            null,
            null,
            seed.PrimaryAdminUserId,
            ActivityEventType.BillDisputeOpened,
            "Wrong bill",
            "This bill belongs to another compound.",
            ActivityEntityType.UtilityBill,
            seed.BlockedCompoundBillId));
        wrongEntity.Status.Should().Be(ServiceResultStatus.BadRequest);

        var noneWithEntityId = await service.RecordAsync(new RecordActivityEventRequest(
            seed.PrimaryCompoundId,
            null,
            null,
            seed.PrimaryAdminUserId,
            ActivityEventType.ConversationOpened,
            "Invalid entity metadata",
            "Entity id cannot be paired with None entity type.",
            ActivityEntityType.None,
            seed.PrimaryBillId));
        noneWithEntityId.Status.Should().Be(ServiceResultStatus.BadRequest);

        var correct = await service.RecordAsync(new RecordActivityEventRequest(
            seed.PrimaryCompoundId,
            seed.PrimaryResidentProfileId,
            seed.PrimaryUnitId,
            seed.PrimaryAdminUserId,
            ActivityEventType.BillDisputeOpened,
            "Correct bill",
            "This activity is compound-consistent.",
            ActivityEntityType.UtilityBill,
            seed.PrimaryBillId));
        correct.Status.Should().Be(ServiceResultStatus.Success);
    }

    [Fact]
    public async Task ResidentConversationResponse_DoesNotExposeAdminInternalMetadata()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedHardeningWorldAsync(database);

        var opened = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.OpenResidentConversationAsync(
                seed.PrimaryResidentUserId,
                new ResidentOpenConversationRequest
                {
                    Topic = ConversationTopic.Billing,
                    IssueType = ConversationIssueType.BillingHighAmount,
                    InitialMessage = "Please review my bill."
                }));
        opened.Status.Should().Be(ServiceResultStatus.Success);
        var conversationId = opened.Value!.Id;

        var assignment = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.AssignConversationAsync(
                seed.PrimaryAdminUserId,
                conversationId,
                new AssignConversationRequest
                {
                    AssignedToUserId = seed.PrimaryStaffUserId,
                    Reason = "manager-only assignment reason"
                }));
        assignment.Status.Should().Be(ServiceResultStatus.Success);

        var escalation = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.EscalateConversationAsync(
                seed.PrimaryAdminUserId,
                conversationId,
                new EscalateConversationRequest
                {
                    EscalationLevel = ConversationEscalationLevel.NeedsAttention,
                    Reason = "manager-only escalation reason"
                }));
        escalation.Status.Should().Be(ServiceResultStatus.Success);

        var internalNote = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.AddInternalNoteAsync(
                seed.PrimaryAdminUserId,
                conversationId,
                new AddInternalNoteRequest { Body = "manager-only internal note" }));
        internalNote.Status.Should().Be(ServiceResultStatus.Success);

        var residentDetails = await ExecuteConversationAsync(
            database,
            seed.PrimaryCompoundId,
            service => service.GetResidentConversationAsync(seed.PrimaryResidentUserId, conversationId));

        residentDetails.Status.Should().Be(ServiceResultStatus.Success);
        residentDetails.Value!.Messages.Should().NotContain(message => message.MessageType == ConversationMessageType.InternalNote);
        residentDetails.Value.Messages.Should().NotContain(message => message.Body.Contains("manager-only"));

        typeof(ResidentConversationResponse).GetProperty("AssignedByUserId").Should().BeNull();
        typeof(ResidentConversationResponse).GetProperty("AssignedToUserId").Should().BeNull();
        typeof(ResidentConversationResponse).GetProperty("AssignedAtUtc").Should().BeNull();
        typeof(ResidentConversationResponse).GetProperty("LastAssignmentReason").Should().BeNull();
        typeof(ResidentConversationResponse).GetProperty("EscalationLevel").Should().BeNull();
        typeof(ResidentConversationResponse).GetProperty("EscalatedAtUtc").Should().BeNull();
        typeof(ResidentConversationResponse).GetProperty("EscalationReason").Should().BeNull();
        typeof(ResidentConversationMessageResponse).GetProperty("Visibility").Should().BeNull();
    }

    [Fact]
    public void CommunicationControllers_KeepStrictRoleSeparation()
    {
        typeof(AdminCommunicationController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .ContainSingle(attribute => attribute.Roles == RoleNames.CommunicationManagers);

        typeof(ResidentCommunicationController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .ContainSingle(attribute => attribute.Roles == RoleNames.Resident);
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

    private static async Task<HardeningSeed> SeedHardeningWorldAsync(TestDb.TestDatabase database)
    {
        await using var dbContext = TestDb.Create(database);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var primaryResidentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"resident-{Guid.NewGuid():N}@test.local",
            Email = $"resident-{Guid.NewGuid():N}@test.local",
            FullName = "Primary Resident"
        };

        var otherResidentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"other-resident-{Guid.NewGuid():N}@test.local",
            Email = $"other-resident-{Guid.NewGuid():N}@test.local",
            FullName = "Other Resident"
        };

        var primaryAdminUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"admin-{Guid.NewGuid():N}@test.local",
            Email = $"admin-{Guid.NewGuid():N}@test.local",
            FullName = "Primary Admin"
        };

        var primaryStaffUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"staff-{Guid.NewGuid():N}@test.local",
            Email = $"staff-{Guid.NewGuid():N}@test.local",
            FullName = "Primary Staff"
        };

        var blockedStaffUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"blocked-staff-{Guid.NewGuid():N}@test.local",
            Email = $"blocked-staff-{Guid.NewGuid():N}@test.local",
            FullName = "Blocked Staff"
        };

        var blockedResidentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"blocked-resident-{Guid.NewGuid():N}@test.local",
            Email = $"blocked-resident-{Guid.NewGuid():N}@test.local",
            FullName = "Blocked Resident"
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
            AreaSquareMeters = 90,
            Bedrooms = 1,
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
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = today.AddMonths(-3)
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
            DueDate = today.AddDays(20),
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
            DueDate = today.AddDays(20),
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
            BillStatus = BillStatus.Unpaid,
            IssueDate = today,
            DueDate = today.AddDays(20),
            SubtotalAmount = 200_000m,
            PreviousBalanceAmount = 0m,
            LateFeeAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 200_000m,
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
            IssueDate = today,
            DueDate = today.AddDays(20),
            SubtotalAmount = 250_000m,
            PreviousBalanceAmount = 0m,
            LateFeeAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 250_000m,
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
            IssueDate = today,
            DueDate = today.AddDays(20),
            SubtotalAmount = 300_000m,
            PreviousBalanceAmount = 0m,
            LateFeeAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 300_000m,
            PaidAmount = 0m
        };

        dbContext.Users.AddRange(
            primaryResidentUser,
            otherResidentUser,
            primaryAdminUser,
            primaryStaffUser,
            blockedStaffUser,
            blockedResidentUser);
        dbContext.Compounds.AddRange(primaryCompound, blockedCompound);
        dbContext.PropertyUnits.AddRange(primaryUnit, otherUnit, blockedUnit);
        dbContext.ResidentProfiles.AddRange(primaryResident, otherResident, blockedResident);
        dbContext.OccupancyRecords.AddRange(primaryOccupancy, otherOccupancy, blockedOccupancy);
        dbContext.BillingCycles.AddRange(primaryBillingCycle, blockedBillingCycle);
        dbContext.UtilityBills.AddRange(primaryBill, otherResidentBill, blockedBill);
        dbContext.UserCompoundAssignments.AddRange(
            new UserCompoundAssignment
            {
                UserId = primaryAdminUser.Id,
                CompoundId = primaryCompound.Id,
                Role = UserRole.CompoundAdmin,
                IsActive = true
            },
            new UserCompoundAssignment
            {
                UserId = primaryStaffUser.Id,
                CompoundId = primaryCompound.Id,
                Role = UserRole.CompoundAdmin,
                IsActive = true
            },
            new UserCompoundAssignment
            {
                UserId = blockedStaffUser.Id,
                CompoundId = blockedCompound.Id,
                Role = UserRole.CompoundAdmin,
                IsActive = true
            });

        await dbContext.SaveChangesAsync();

        return new HardeningSeed(
            primaryCompound.Id,
            blockedCompound.Id,
            primaryUnit.Id,
            blockedUnit.Id,
            primaryResident.Id,
            blockedResident.Id,
            primaryResidentUser.Id,
            primaryAdminUser.Id,
            primaryStaffUser.Id,
            blockedStaffUser.Id,
            primaryBill.Id,
            otherResidentBill.Id,
            blockedBill.Id);
    }

    private sealed record HardeningSeed(
        Guid PrimaryCompoundId,
        Guid BlockedCompoundId,
        Guid PrimaryUnitId,
        Guid BlockedUnitId,
        Guid PrimaryResidentProfileId,
        Guid BlockedResidentProfileId,
        Guid PrimaryResidentUserId,
        Guid PrimaryAdminUserId,
        Guid PrimaryStaffUserId,
        Guid BlockedStaffUserId,
        Guid PrimaryBillId,
        Guid OtherResidentBillId,
        Guid BlockedCompoundBillId);
}
