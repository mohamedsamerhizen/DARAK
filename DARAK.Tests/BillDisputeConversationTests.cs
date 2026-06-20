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

public sealed class BillDisputeConversationTests
{
    [Fact]
    public async Task ResidentBillDispute_CreatesLinkedConversationAndTimelineEvent()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedBillDisputeWorldAsync(database);

        var result = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.OpenResidentBillDisputeAsync(
                seed.ResidentUserId,
                seed.BillId,
                new ResidentBillDisputeRequest
                {
                    IssueType = ConversationIssueType.BillingMeterReadingIssue,
                    Message = "The meter reading is different from the photo I have."
                }));

        result.Status.Should().Be(ServiceResultStatus.Success);
        var dispute = result.Value!;
        dispute.CreatedNew.Should().BeTrue();
        dispute.BillId.Should().Be(seed.BillId);
        dispute.IssueType.Should().Be(ConversationIssueType.BillingMeterReadingIssue);
        dispute.Status.Should().Be(ConversationStatus.PendingAdminReply);

        await using var verifyContext = TestDb.Create(database);
        var conversation = await verifyContext.Conversations
            .Include(item => item.Messages)
            .SingleAsync(item => item.Id == dispute.ConversationId);

        conversation.Topic.Should().Be(ConversationTopic.Billing);
        conversation.LinkedEntityType.Should().Be(ConversationLinkedEntityType.UtilityBill);
        conversation.LinkedEntityId.Should().Be(seed.BillId);
        conversation.ResidentProfileId.Should().Be(seed.ResidentProfileId);
        conversation.Messages.Should().Contain(message =>
            message.MessageType == ConversationMessageType.SystemMessage
            && message.Body == "Conversation opened from bill objection.");

        var events = await verifyContext.ActivityEvents
            .Where(item => item.EntityId == seed.BillId)
            .Select(item => item.EventType)
            .ToListAsync();
        events.Should().Contain(ActivityEventType.BillDisputeOpened);
    }

    [Fact]
    public async Task ResidentBillDispute_DuplicateOpenDispute_ReturnsExistingConversation()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedBillDisputeWorldAsync(database);

        var first = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.OpenResidentBillDisputeAsync(
                seed.ResidentUserId,
                seed.BillId,
                new ResidentBillDisputeRequest
                {
                    IssueType = ConversationIssueType.BillingHighAmount,
                    Message = "This bill is much higher than expected."
                }));

        first.Status.Should().Be(ServiceResultStatus.Success);
        var firstDispute = first.Value!;
        var firstConversationId = firstDispute.ConversationId;

        var second = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.OpenResidentBillDisputeAsync(
                seed.ResidentUserId,
                seed.BillId,
                new ResidentBillDisputeRequest
                {
                    IssueType = ConversationIssueType.BillingHighAmount,
                    Message = "Opening the same dispute again."
                }));

        second.Status.Should().Be(ServiceResultStatus.Success);
        var duplicateDispute = second.Value!;
        duplicateDispute.CreatedNew.Should().BeFalse();
        duplicateDispute.ConversationId.Should().Be(firstConversationId);

        await using var verifyContext = TestDb.Create(database);
        var disputeCount = await verifyContext.Conversations.CountAsync(conversation =>
            conversation.LinkedEntityType == ConversationLinkedEntityType.UtilityBill
            && conversation.LinkedEntityId == seed.BillId);
        disputeCount.Should().Be(1);
    }

    [Fact]
    public async Task ResidentBillDispute_BillOutsideResidentScope_ReturnsNotFound()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedBillDisputeWorldAsync(database);

        var result = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.OpenResidentBillDisputeAsync(
                seed.OtherResidentUserId,
                seed.BillId,
                new ResidentBillDisputeRequest
                {
                    IssueType = ConversationIssueType.BillingHighAmount,
                    Message = "I should not see this bill."
                }));

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task ResidentBillDispute_InvalidIssueType_ReturnsBadRequest()
    {
        var database = TestDb.CreateSharedDatabase();
        var seed = await SeedBillDisputeWorldAsync(database);

        var result = await ExecuteConversationAsync(
            database,
            seed.CompoundId,
            service => service.OpenResidentBillDisputeAsync(
                seed.ResidentUserId,
                seed.BillId,
                new ResidentBillDisputeRequest
                {
                    IssueType = ConversationIssueType.MaintenanceWaterLeak,
                    Message = "Wrong category."
                }));

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
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

    private static async Task<BillDisputeSeed> SeedBillDisputeWorldAsync(TestDb.TestDatabase database)
    {
        await using var dbContext = TestDb.Create(database);

        var residentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"resident-{Guid.NewGuid():N}@test.local",
            Email = $"resident-{Guid.NewGuid():N}@test.local",
            FullName = "Resident One"
        };

        var otherResidentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"resident-{Guid.NewGuid():N}@test.local",
            Email = $"resident-{Guid.NewGuid():N}@test.local",
            FullName = "Other Resident"
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
            UnitNumber = "B-202",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 100,
            Bedrooms = 2,
            Bathrooms = 1
        };

        var resident = new ResidentProfile
        {
            UserId = residentUser.Id,
            CompoundId = compound.Id,
            FullName = "Resident One"
        };

        var otherResident = new ResidentProfile
        {
            UserId = otherResidentUser.Id,
            CompoundId = compound.Id,
            FullName = "Other Resident"
        };

        var occupancy = new OccupancyRecord
        {
            ResidentProfileId = resident.Id,
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddMonths(-3))
        };

        var billingCycle = new BillingCycle
        {
            CompoundId = compound.Id,
            Year = DateTime.UtcNow.Year,
            Month = DateTime.UtcNow.Month,
            PeriodStart = DateOnly.FromDateTime(new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)),
            PeriodEnd = DateOnly.FromDateTime(new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1).AddDays(-1)),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(10)),
            IsClosed = false
        };

        var bill = new UtilityBill
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            BillingCycleId = billingCycle.Id,
            BillNumber = "UTIL-2026-001",
            BillStatus = BillStatus.Unpaid,
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            DueDate = billingCycle.DueDate,
            SubtotalAmount = 125_000m,
            PreviousBalanceAmount = 0m,
            LateFeeAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 125_000m,
            PaidAmount = 0m
        };

        dbContext.Users.AddRange(residentUser, otherResidentUser);
        dbContext.Compounds.Add(compound);
        dbContext.PropertyUnits.Add(unit);
        dbContext.ResidentProfiles.AddRange(resident, otherResident);
        dbContext.OccupancyRecords.Add(occupancy);
        dbContext.BillingCycles.Add(billingCycle);
        dbContext.UtilityBills.Add(bill);
        await dbContext.SaveChangesAsync();

        return new BillDisputeSeed(
            compound.Id,
            unit.Id,
            resident.Id,
            residentUser.Id,
            otherResidentUser.Id,
            bill.Id);
    }

    private sealed record BillDisputeSeed(
        Guid CompoundId,
        Guid UnitId,
        Guid ResidentProfileId,
        Guid ResidentUserId,
        Guid OtherResidentUserId,
        Guid BillId);
}
