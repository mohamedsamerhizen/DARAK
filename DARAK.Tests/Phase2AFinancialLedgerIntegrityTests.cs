using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Analytics;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class Phase2AFinancialLedgerIntegrityTests
{
    [Fact]
    public async Task RecordManualPaymentAsync_CreatesCreditLedgerEntryAndAuditRecord()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUtilityBillAsync(dbContext);
        var audit = new CapturingAuditLogService();
        var service = new PaymentService(dbContext, auditLogService: audit);

        var result = await service.RecordManualPaymentAsync(new ManualPaymentRequest
        {
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = seed.UtilityBillId,
            PaymentMethod = PaymentMethod.Cash,
            Amount = 40m,
            Notes = "Cash received"
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        var paymentId = result.Value!.Id;
        var ledger = await dbContext.ResidentLedgerEntries.SingleAsync();
        ledger.SourceType.Should().Be(FinancialLedgerSourceType.Payment);
        ledger.SourceId.Should().Be(paymentId);
        ledger.Direction.Should().Be(FinancialLedgerEntryDirection.Credit);
        ledger.Amount.Should().Be(40m);
        ledger.CompoundId.Should().Be(seed.CompoundId);
        ledger.ResidentProfileId.Should().Be(seed.ResidentProfileId);
        audit.Records.Should().ContainSingle(record =>
            record.ActionType == AuditActionType.LedgerEntryCreated
            && record.EntityType == AuditEntityType.ResidentLedgerEntry
            && record.EntityId == ledger.Id);
    }

    [Fact]
    public async Task ConfirmResidentMockPaymentSuccessAsync_CreatesCreditLedgerEntry()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUtilityBillAsync(dbContext);
        var payment = new Payment
        {
            CompoundId = seed.CompoundId,
            ResidentProfileId = seed.ResidentProfileId,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = seed.UtilityBillId,
            PaymentMethod = PaymentMethod.ZainCashMock,
            PaymentStatus = PaymentStatus.Pending,
            Amount = 35m,
            PaymentReference = "PAY-PENDING"
        };
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        var service = new PaymentService(dbContext);

        var result = await service.ConfirmResidentMockPaymentSuccessAsync(
            seed.UserId,
            payment.Id,
            PaymentMethod.ZainCashMock,
            new ConfirmMockPaymentRequest { ProviderTransactionId = "TX-OK" });

        result.Status.Should().Be(ServiceResultStatus.Success);
        var ledger = await dbContext.ResidentLedgerEntries.SingleAsync();
        ledger.SourceType.Should().Be(FinancialLedgerSourceType.Payment);
        ledger.SourceId.Should().Be(payment.Id);
        ledger.Direction.Should().Be(FinancialLedgerEntryDirection.Credit);
        ledger.Amount.Should().Be(35m);
    }

    [Fact]
    public async Task ConfirmResidentMockPaymentSuccessAsync_DoesNotDuplicateExistingPaymentLedgerEntry()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUtilityBillAsync(dbContext);
        var payment = new Payment
        {
            CompoundId = seed.CompoundId,
            ResidentProfileId = seed.ResidentProfileId,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = seed.UtilityBillId,
            PaymentMethod = PaymentMethod.ZainCashMock,
            PaymentStatus = PaymentStatus.Pending,
            Amount = 35m,
            PaymentReference = "PAY-PENDING"
        };
        dbContext.Payments.Add(payment);
        dbContext.ResidentLedgerEntries.Add(new ResidentLedgerEntry
        {
            CompoundId = seed.CompoundId,
            ResidentProfileId = seed.ResidentProfileId,
            Direction = FinancialLedgerEntryDirection.Credit,
            SourceType = FinancialLedgerSourceType.Payment,
            SourceId = payment.Id,
            Amount = 35m,
            Currency = "IQD",
            Reference = "PAY-PENDING",
            Description = "Existing payment ledger entry"
        });
        await dbContext.SaveChangesAsync();
        var service = new PaymentService(dbContext);

        var result = await service.ConfirmResidentMockPaymentSuccessAsync(
            seed.UserId,
            payment.Id,
            PaymentMethod.ZainCashMock,
            new ConfirmMockPaymentRequest());

        result.Status.Should().Be(ServiceResultStatus.Success);
        var count = await dbContext.ResidentLedgerEntries
            .CountAsync(entry => entry.SourceType == FinancialLedgerSourceType.Payment && entry.SourceId == payment.Id);
        count.Should().Be(1);
    }

    [Fact]
    public async Task RefundPaymentAsync_CreatesDebitLedgerEntryAndAuditRecord()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedPaidUtilityBillWithPaymentAsync(dbContext);
        var audit = new CapturingAuditLogService();
        var service = new PaymentService(dbContext, auditLogService: audit);

        var result = await service.RefundPaymentAsync(
            seed.PaymentId,
            new RefundPaymentRequest { Reason = "Duplicate receipt" });

        result.Status.Should().Be(ServiceResultStatus.Success);
        var ledger = await dbContext.ResidentLedgerEntries
            .SingleAsync(entry => entry.SourceType == FinancialLedgerSourceType.Refund && entry.SourceId == seed.PaymentId);
        ledger.Direction.Should().Be(FinancialLedgerEntryDirection.Debit);
        ledger.Amount.Should().Be(40m);
        ledger.Description.Should().Be("Duplicate receipt");
        audit.Records.Should().ContainSingle(record =>
            record.ActionType == AuditActionType.LedgerEntryCreated
            && record.EntityType == AuditEntityType.ResidentLedgerEntry
            && record.EntityId == ledger.Id);
    }

    [Fact]
    public async Task RefundPaymentAsync_DoesNotDuplicateExistingRefundLedgerEntry()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedPaidUtilityBillWithPaymentAsync(dbContext);
        dbContext.ResidentLedgerEntries.Add(new ResidentLedgerEntry
        {
            CompoundId = seed.CompoundId,
            ResidentProfileId = seed.ResidentProfileId,
            Direction = FinancialLedgerEntryDirection.Debit,
            SourceType = FinancialLedgerSourceType.Refund,
            SourceId = seed.PaymentId,
            Amount = 40m,
            Currency = "IQD",
            Reference = seed.PaymentReference,
            Description = "Existing refund ledger entry"
        });
        await dbContext.SaveChangesAsync();
        var service = new PaymentService(dbContext);

        var result = await service.RefundPaymentAsync(
            seed.PaymentId,
            new RefundPaymentRequest { Reason = "Duplicate refund" });

        result.Status.Should().Be(ServiceResultStatus.Success);
        var count = await dbContext.ResidentLedgerEntries
            .CountAsync(entry => entry.SourceType == FinancialLedgerSourceType.Refund && entry.SourceId == seed.PaymentId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task CommunityReport_IncludesAnnouncementsPollVotesAndNotificationsForCompound()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedCommunityAnalyticsAsync(dbContext);
        var service = new AnalyticsService(dbContext, new FakeCompoundAccessService([seed.CompoundId]));

        var result = await service.GetCommunityReportAsync(new DateRangeQueryRequest
        {
            CompoundId = seed.CompoundId
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.TotalAnnouncements.Should().Be(1);
        result.Value.PublishedAnnouncements.Should().Be(1);
        result.Value.ActivePolls.Should().Be(1);
        result.Value.TotalPollVotes.Should().Be(1);
        result.Value.TotalNotifications.Should().Be(1);
        result.Value.UnreadNotifications.Should().Be(1);
    }


    [Fact]
    public async Task ResidentStatement_DoesNotDoubleCountLedgerBackedPaymentRows()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUtilityBillAsync(dbContext);
        var payment = new Payment
        {
            CompoundId = seed.CompoundId,
            ResidentProfileId = seed.ResidentProfileId,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = seed.UtilityBillId,
            PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = 40m,
            Currency = "IQD",
            PaymentReference = "PAY-STAT-2A",
            CompletedAt = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.Payments.Add(payment);
        dbContext.ResidentLedgerEntries.Add(new ResidentLedgerEntry
        {
            CompoundId = seed.CompoundId,
            ResidentProfileId = seed.ResidentProfileId,
            Direction = FinancialLedgerEntryDirection.Credit,
            SourceType = FinancialLedgerSourceType.Payment,
            SourceId = payment.Id,
            Amount = 40m,
            Currency = "IQD",
            Reference = payment.PaymentReference,
            Description = "Payment ledger entry",
            OccurredAtUtc = payment.CompletedAt!.Value
        });
        await dbContext.SaveChangesAsync();
        var service = new FinancialControlService(
            dbContext,
            new FakeCompoundAccessService([seed.CompoundId]),
            new AuditLogService(dbContext, new FakeCompoundAccessService([seed.CompoundId]), new HttpContextAccessor()));

        var result = await service.GetResidentStatementAsync(seed.ResidentProfileId, new ResidentStatementQuery());

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.TotalDebits.Should().Be(100m);
        result.Value.TotalCredits.Should().Be(40m);
        result.Value.ClosingBalance.Should().Be(60m);
        result.Value.Lines.Count(line => line.SourceType == FinancialLedgerSourceType.Payment && line.SourceId == payment.Id).Should().Be(1);
    }

    private static async Task<UtilitySeed> SeedUtilityBillAsync(DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        var compound = new Compound { Name = "Darak", Code = Guid.NewGuid().ToString("N")[..8], City = "Baghdad", Area = "Karrada" };
        var unit = new PropertyUnit
        {
            CompoundId = compound.Id,
            UnitNumber = "A-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied
        };
        var userId = Guid.NewGuid();
        var resident = new ResidentProfile
        {
            UserId = userId,
            CompoundId = compound.Id,
            FullName = "Resident One"
        };
        var cycle = new BillingCycle
        {
            CompoundId = compound.Id,
            Year = 2026,
            Month = 6,
            PeriodStart = new DateOnly(2026, 6, 1),
            PeriodEnd = new DateOnly(2026, 6, 30),
            DueDate = new DateOnly(2026, 7, 10)
        };
        var bill = new UtilityBill
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            BillingCycleId = cycle.Id,
            BillNumber = "UB-2A",
            IssueDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 7, 10),
            SubtotalAmount = 100m,
            TotalAmount = 100m,
            BillStatus = BillStatus.Unpaid
        };

        dbContext.AddRange(compound, unit, resident, cycle, bill);
        await dbContext.SaveChangesAsync();
        return new UtilitySeed(compound.Id, resident.Id, userId, bill.Id);
    }

    private static async Task<PaidPaymentSeed> SeedPaidUtilityBillWithPaymentAsync(DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        var seed = await SeedUtilityBillAsync(dbContext);
        var bill = await dbContext.UtilityBills.SingleAsync(item => item.Id == seed.UtilityBillId);
        bill.PaidAmount = 40m;
        bill.BillStatus = BillStatus.PartiallyPaid;
        var payment = new Payment
        {
            CompoundId = seed.CompoundId,
            ResidentProfileId = seed.ResidentProfileId,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = seed.UtilityBillId,
            PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = 40m,
            PaymentReference = "PAY-REFUND",
            CompletedAt = DateTime.UtcNow
        };
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        return new PaidPaymentSeed(seed.CompoundId, seed.ResidentProfileId, payment.Id, payment.PaymentReference);
    }

    private static async Task<CommunityAnalyticsSeed> SeedCommunityAnalyticsAsync(DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        var compound = new Compound { Name = "Community", Code = Guid.NewGuid().ToString("N")[..8], City = "Baghdad", Area = "Karrada" };
        var otherCompound = new Compound { Name = "Other", Code = Guid.NewGuid().ToString("N")[..8], City = "Baghdad", Area = "Mansour" };
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var resident = new ResidentProfile { UserId = userId, CompoundId = compound.Id, FullName = "Community Resident" };
        var otherResident = new ResidentProfile { UserId = otherUserId, CompoundId = otherCompound.Id, FullName = "Other Resident" };
        var announcement = new Announcement
        {
            CompoundId = compound.Id,
            Title = "Announcement",
            Body = "Body",
            Status = AnnouncementStatus.Published,
            PublishedAt = DateTime.UtcNow
        };
        var otherAnnouncement = new Announcement
        {
            CompoundId = otherCompound.Id,
            Title = "Other Announcement",
            Body = "Body",
            Status = AnnouncementStatus.Published,
            PublishedAt = DateTime.UtcNow
        };
        var poll = new CommunityPoll
        {
            CompoundId = compound.Id,
            Question = "Poll?",
            Status = CommunityPollStatus.Open,
            StartsAt = DateTime.UtcNow.AddDays(-1),
            EndsAt = DateTime.UtcNow.AddDays(1)
        };
        var pollOption = new CommunityPollOption
        {
            PollId = poll.Id,
            Text = "Yes",
            DisplayOrder = 1
        };
        var vote = new CommunityPollVote
        {
            PollId = poll.Id,
            PollOptionId = pollOption.Id,
            UserId = userId
        };
        var notification = new ResidentNotification
        {
            UserId = userId,
            Title = "Notice",
            Message = "Message",
            IsRead = false
        };
        var otherNotification = new ResidentNotification
        {
            UserId = otherUserId,
            Title = "Other Notice",
            Message = "Message",
            IsRead = false
        };

        dbContext.AddRange(
            compound,
            otherCompound,
            resident,
            otherResident,
            announcement,
            otherAnnouncement,
            poll,
            pollOption,
            vote,
            notification,
            otherNotification);
        await dbContext.SaveChangesAsync();
        return new CommunityAnalyticsSeed(compound.Id);
    }

    private sealed class CapturingAuditLogService : IAuditLogService
    {
        public List<AuditLogRecord> Records { get; } = [];

        public Task<Guid> AppendEntryAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<ServiceResult<PagedResult<AuditLogResponse>>> SearchAsync(AuditSearchQuery query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<AuditLogDetailsResponse>> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<PagedResult<AuditLogResponse>>> GetEntityTrailAsync(AuditEntityType entityType, Guid entityId, AuditEntityTrailQuery query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<PagedResult<AuditLogResponse>>> GetResidentTrailAsync(Guid residentProfileId, AuditEntityTrailQuery query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<AuditDashboardResponse>> GetDashboardAsync(AuditDashboardQuery query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed record UtilitySeed(Guid CompoundId, Guid ResidentProfileId, Guid UserId, Guid UtilityBillId);

    private sealed record PaidPaymentSeed(Guid CompoundId, Guid ResidentProfileId, Guid PaymentId, string PaymentReference);

    private sealed record CommunityAnalyticsSeed(Guid CompoundId);
}
