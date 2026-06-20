using System.Net;
using System.Net.Http.Json;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class HttpAuthorizationIntegrationTests
{
    [Fact]
    public async Task ResidentToken_CannotCallAdminCommunicationEndpoint()
    {
        using var factory = new DarakApiFactory();
        var resident = await factory.CreateAuthenticatedClientAsync(UserRole.Resident);

        var response = await resident.Client.GetAsync("/api/admin/communication/conversations");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GuardToken_CannotCallAdminBillingEndpoint()
    {
        using var factory = new DarakApiFactory();
        var compoundId = Guid.NewGuid();
        await factory.SeedAsync(dbContext =>
        {
            dbContext.Compounds.Add(CreateCompound(compoundId, "GUARD-COMPOUND"));
            return Task.CompletedTask;
        });
        var guard = await factory.CreateAuthenticatedClientAsync(UserRole.Guard, compoundId);

        var response = await guard.Client.GetAsync("/api/admin/billing/cycles");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CompoundAdminOutsideConversationCompound_GetsNotFoundOverHttp()
    {
        using var factory = new DarakApiFactory();
        var allowedCompoundId = Guid.NewGuid();
        var blockedCompoundId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        await factory.SeedAsync(dbContext =>
        {
            var allowedCompound = CreateCompound(allowedCompoundId, "ALLOWED-COMPOUND");
            var blockedCompound = CreateCompound(blockedCompoundId, "BLOCKED-COMPOUND");
            var unit = CreateUnit(blockedCompoundId, "B-101");
            var resident = CreateResident(Guid.NewGuid(), blockedCompoundId, "Blocked Resident");

            dbContext.Compounds.AddRange(allowedCompound, blockedCompound);
            dbContext.PropertyUnits.Add(unit);
            dbContext.ResidentProfiles.Add(resident);
            dbContext.Conversations.Add(new Conversation
            {
                Id = conversationId,
                CompoundId = blockedCompoundId,
                ResidentProfileId = resident.Id,
                PropertyUnitId = unit.Id,
                Status = ConversationStatus.PendingAdminReply,
                Priority = ConversationPriority.High,
                Topic = ConversationTopic.Billing,
                IssueType = ConversationIssueType.BillingHighAmount,
                CreatedAtUtc = DateTime.UtcNow,
                LastMessageAtUtc = DateTime.UtcNow
            });
            return Task.CompletedTask;
        });
        var admin = await factory.CreateAuthenticatedClientAsync(UserRole.CompoundAdmin, allowedCompoundId);

        var response = await admin.Client.GetAsync($"/api/admin/communication/conversations/{conversationId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResidentConversationDetails_DoesNotExposeInternalMessagesOrAdminMetadataOverHttp()
    {
        using var factory = new DarakApiFactory();
        var resident = await factory.CreateAuthenticatedClientAsync(UserRole.Resident);
        var adminUserId = Guid.NewGuid();
        var compoundId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        await factory.SeedAsync(dbContext =>
        {
            var compound = CreateCompound(compoundId, "RESIDENT-CONVERSATION");
            var unit = CreateUnit(compoundId, "R-101");
            var residentProfile = CreateResident(resident.UserId, compoundId, "Resident HTTP User");

            dbContext.Compounds.Add(compound);
            dbContext.PropertyUnits.Add(unit);
            dbContext.ResidentProfiles.Add(residentProfile);
            dbContext.Conversations.Add(new Conversation
            {
                Id = conversationId,
                CompoundId = compoundId,
                ResidentProfileId = residentProfile.Id,
                PropertyUnitId = unit.Id,
                Status = ConversationStatus.PendingResidentReply,
                Priority = ConversationPriority.High,
                Topic = ConversationTopic.Billing,
                IssueType = ConversationIssueType.BillingHighAmount,
                LinkedEntityType = ConversationLinkedEntityType.UtilityBill,
                LinkedEntityId = Guid.NewGuid(),
                AssignedToUserId = adminUserId,
                AssignedByUserId = adminUserId,
                AssignedAtUtc = DateTime.UtcNow,
                LastAssignmentReason = "Internal assignment reason should stay hidden.",
                EscalationLevel = ConversationEscalationLevel.Critical,
                EscalatedAtUtc = DateTime.UtcNow,
                EscalationReason = "Internal escalation reason should stay hidden.",
                ReopenCount = 1,
                LastReopenReason = "Resident visible reopen reason.",
                CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
                LastMessageAtUtc = DateTime.UtcNow
            });
            dbContext.ConversationMessages.AddRange(
                new ConversationMessage
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    SenderUserId = resident.UserId,
                    MessageType = ConversationMessageType.ResidentMessage,
                    Visibility = ConversationMessageVisibility.ResidentVisible,
                    Body = "Visible resident message.",
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-20)
                },
                new ConversationMessage
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    SenderUserId = adminUserId,
                    MessageType = ConversationMessageType.InternalNote,
                    Visibility = ConversationMessageVisibility.InternalOnly,
                    Body = "SECRET INTERNAL NOTE BODY",
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10)
                });
            return Task.CompletedTask;
        });

        var response = await resident.Client.GetAsync($"/api/resident/communication/conversations/{conversationId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Visible resident message.");
        json.Should().Contain("Resident visible reopen reason.");
        json.Should().NotContain("SECRET INTERNAL NOTE BODY");
        json.Should().NotContain("visibility");
        json.Should().NotContain("assignedToUserId");
        json.Should().NotContain("assignedByUserId");
        json.Should().NotContain("lastAssignmentReason");
        json.Should().NotContain("escalationReason");
        json.Should().NotContain("escalationLevel");
    }

    [Fact]
    public async Task ResidentCannotDisputeAnotherResidentsBillOverHttp()
    {
        using var factory = new DarakApiFactory();
        var resident = await factory.CreateAuthenticatedClientAsync(UserRole.Resident);
        var compoundId = Guid.NewGuid();
        var otherBillId = Guid.NewGuid();

        await factory.SeedAsync(dbContext =>
        {
            var compound = CreateCompound(compoundId, "BILL-DISPUTE");
            var unit = CreateUnit(compoundId, "D-101");
            var currentResidentProfile = CreateResident(resident.UserId, compoundId, "Current Resident");
            var otherResidentProfile = CreateResident(Guid.NewGuid(), compoundId, "Other Resident");
            var cycle = CreateBillingCycle(compoundId);

            dbContext.Compounds.Add(compound);
            dbContext.PropertyUnits.Add(unit);
            dbContext.ResidentProfiles.AddRange(currentResidentProfile, otherResidentProfile);
            dbContext.BillingCycles.Add(cycle);
            dbContext.UtilityBills.Add(new UtilityBill
            {
                Id = otherBillId,
                CompoundId = compoundId,
                PropertyUnitId = unit.Id,
                ResidentProfileId = otherResidentProfile.Id,
                BillingCycleId = cycle.Id,
                BillNumber = "BILL-OTHER-001",
                BillStatus = BillStatus.Unpaid,
                IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-5),
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(10),
                SubtotalAmount = 250_000m,
                TotalAmount = 250_000m,
                PaidAmount = 0m
            });
            return Task.CompletedTask;
        });

        var response = await resident.Client.PostAsJsonAsync(
            $"/api/resident/account/bills/{otherBillId}/dispute",
            new ResidentBillDisputeRequest
            {
                IssueType = ConversationIssueType.BillingHighAmount,
                Message = "This bill does not belong to me."
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static Compound CreateCompound(Guid id, string code)
    {
        return new Compound
        {
            Id = id,
            Name = code,
            Code = code,
            City = "Baghdad",
            Area = "Karrada"
        };
    }

    private static PropertyUnit CreateUnit(Guid compoundId, string unitNumber)
    {
        return new PropertyUnit
        {
            Id = Guid.NewGuid(),
            CompoundId = compoundId,
            UnitNumber = unitNumber,
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied,
            AreaSquareMeters = 120,
            Bedrooms = 2,
            Bathrooms = 1
        };
    }

    private static ResidentProfile CreateResident(Guid userId, Guid compoundId, string fullName)
    {
        return new ResidentProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompoundId = compoundId,
            FullName = fullName
        };
    }

    private static BillingCycle CreateBillingCycle(Guid compoundId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return new BillingCycle
        {
            Id = Guid.NewGuid(),
            CompoundId = compoundId,
            Year = today.Year,
            Month = today.Month,
            PeriodStart = new DateOnly(today.Year, today.Month, 1),
            PeriodEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
            DueDate = today.AddDays(10)
        };
    }
}
