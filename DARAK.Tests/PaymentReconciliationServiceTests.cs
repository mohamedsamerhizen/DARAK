using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class PaymentReconciliationServiceTests
{
    [Fact]
    public async Task CreateBatchAsync_MatchesSucceededProviderTransaction()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedSucceededPaymentAsync(dbContext, amount: 100m, providerTransactionId: "TX-1");
        var service = new PaymentReconciliationService(dbContext);

        var result = await service.CreateBatchAsync(
            Guid.NewGuid(),
            CreateRequest(seed.CompoundId, "ZainCashMock", "TX-1", 100m, PaymentStatus.Succeeded));

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.TotalItems.Should().Be(1);
        result.Value.MatchedItems.Should().Be(1);
        result.Value.IssueItems.Should().Be(0);
        var item = result.Value.Items.Single();
        item.MatchStatus.Should().Be(PaymentReconciliationItemStatus.Matched);
        item.MatchedPaymentId.Should().Be(seed.PaymentId);
        item.MatchedPaymentAttemptId.Should().Be(seed.PaymentAttemptId);
    }

    [Fact]
    public async Task CreateBatchAsync_MarksMissingProviderTransaction()
    {
        await using var dbContext = TestDb.Create();
        var compound = await SeedCompoundAsync(dbContext);
        var service = new PaymentReconciliationService(dbContext);

        var result = await service.CreateBatchAsync(
            Guid.NewGuid(),
            CreateRequest(compound.Id, "ZainCashMock", "MISSING-TX", 100m, PaymentStatus.Succeeded));

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.MatchedItems.Should().Be(0);
        var item = result.Value.Items.Single();
        item.MatchStatus.Should().Be(PaymentReconciliationItemStatus.MissingInDarak);
        item.MatchedPaymentId.Should().BeNull();
        item.IssueReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateBatchAsync_MarksAmountMismatch()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedSucceededPaymentAsync(dbContext, amount: 100m, providerTransactionId: "TX-AMOUNT");
        var service = new PaymentReconciliationService(dbContext);

        var result = await service.CreateBatchAsync(
            Guid.NewGuid(),
            CreateRequest(seed.CompoundId, "ZainCashMock", "TX-AMOUNT", 125m, PaymentStatus.Succeeded));

        result.Status.Should().Be(ServiceResultStatus.Success);
        var item = result.Value!.Items.Single();
        item.MatchStatus.Should().Be(PaymentReconciliationItemStatus.AmountMismatch);
        item.DifferenceAmount.Should().Be(25m);
        item.ReviewDecision.Should().Be(PaymentReconciliationReviewDecision.None);
        result.Value.UnreviewedIssueItems.Should().Be(1);
        result.Value.TotalDifferenceAmount.Should().Be(25m);
    }

    [Fact]
    public async Task CreateBatchAsync_RejectsDuplicateProviderTransactionInsideSameBatch()
    {
        await using var dbContext = TestDb.Create();
        var compound = await SeedCompoundAsync(dbContext);
        var service = new PaymentReconciliationService(dbContext);

        var result = await service.CreateBatchAsync(
            Guid.NewGuid(),
            new CreatePaymentReconciliationBatchRequest
            {
                CompoundId = compound.Id,
                Provider = "ZainCashMock",
                StatementReference = "ST-DUP",
                StatementDate = new DateOnly(2026, 6, 17),
                Items =
                [
                    new CreatePaymentReconciliationItemRequest
                    {
                        ProviderTransactionId = "DUP-TX",
                        ProviderAmount = 100m,
                        ProviderStatus = PaymentStatus.Succeeded
                    },
                    new CreatePaymentReconciliationItemRequest
                    {
                        ProviderTransactionId = "dup-tx",
                        ProviderAmount = 100m,
                        ProviderStatus = PaymentStatus.Succeeded
                    }
                ]
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task CreateBatchAsync_ReturnsForbiddenForUnassignedCompound()
    {
        await using var dbContext = TestDb.Create();
        var allowedCompound = await SeedCompoundAsync(dbContext, "Allowed", "ALLOWED");
        var blockedCompound = await SeedCompoundAsync(dbContext, "Blocked", "BLOCKED");
        var service = new PaymentReconciliationService(
            dbContext,
            new FakeCompoundAccessService([allowedCompound.Id]));

        var result = await service.CreateBatchAsync(
            Guid.NewGuid(),
            CreateRequest(blockedCompound.Id, "ZainCashMock", "TX-BLOCKED", 100m, PaymentStatus.Succeeded));

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
    }

    [Fact]
    public async Task CreateBatchAsync_CompoundMismatch_DoesNotExposeCrossCompoundPaymentIds()
    {
        await using var dbContext = TestDb.Create();
        var otherCompoundSeed = await SeedSucceededPaymentAsync(
            dbContext,
            amount: 100m,
            providerTransactionId: "TX-CROSS-COMPOUND");
        var currentCompound = await SeedCompoundAsync(
            dbContext,
            "Current Compound",
            "CURRENT");
        var service = new PaymentReconciliationService(dbContext);

        var result = await service.CreateBatchAsync(
            Guid.NewGuid(),
            CreateRequest(
                currentCompound.Id,
                "ZainCashMock",
                "TX-CROSS-COMPOUND",
                100m,
                PaymentStatus.Succeeded));

        result.Status.Should().Be(ServiceResultStatus.Success);
        var item = result.Value!.Items.Single();
        item.MatchStatus.Should().Be(PaymentReconciliationItemStatus.CompoundMismatch);
        item.MatchedPaymentId.Should().BeNull();
        item.MatchedPaymentAttemptId.Should().BeNull();
        item.IssueReason.Should().NotBeNullOrWhiteSpace();
        otherCompoundSeed.PaymentId.Should().NotBe(Guid.Empty);
        otherCompoundSeed.PaymentAttemptId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ReviewItemAsync_ReviewsExceptionItem()
    {
        await using var dbContext = TestDb.Create();
        var compound = await SeedCompoundAsync(dbContext);
        var service = new PaymentReconciliationService(dbContext);
        var created = await service.CreateBatchAsync(
            Guid.NewGuid(),
            CreateRequest(compound.Id, "ZainCashMock", "TX-REVIEW", 100m, PaymentStatus.Succeeded));
        var item = created.Value!.Items.Single();

        var reviewedByUserId = Guid.NewGuid();
        var reviewed = await service.ReviewItemAsync(
            reviewedByUserId,
            created.Value.Id,
            item.Id,
            new ReviewPaymentReconciliationItemRequest
            {
                Decision = PaymentReconciliationReviewDecision.RequiresProviderCorrection,
                ReviewNotes = "Provider statement has no matching DARAK transaction."
            });

        reviewed.Status.Should().Be(ServiceResultStatus.Success);
        reviewed.Value!.ReviewDecision.Should().Be(PaymentReconciliationReviewDecision.RequiresProviderCorrection);
        reviewed.Value.ReviewedByUserId.Should().Be(reviewedByUserId);
        reviewed.Value.ReviewedAtUtc.Should().NotBeNull();
        reviewed.Value.ReviewNotes.Should().Be("Provider statement has no matching DARAK transaction.");
    }

    [Fact]
    public async Task CloseBatchAsync_RejectsUnreviewedExceptionItems()
    {
        await using var dbContext = TestDb.Create();
        var compound = await SeedCompoundAsync(dbContext);
        var service = new PaymentReconciliationService(dbContext);
        var created = await service.CreateBatchAsync(
            Guid.NewGuid(),
            CreateRequest(compound.Id, "ZainCashMock", "TX-UNREVIEWED", 100m, PaymentStatus.Succeeded));

        var closed = await service.CloseBatchAsync(
            Guid.NewGuid(),
            created.Value!.Id,
            new ClosePaymentReconciliationBatchRequest { Notes = "Reviewed." });

        closed.Status.Should().Be(ServiceResultStatus.Conflict);
    }

    [Theory]
    [InlineData(PaymentReconciliationReviewDecision.RequiresDarakCorrection)]
    [InlineData(PaymentReconciliationReviewDecision.RequiresProviderCorrection)]
    public async Task CloseBatchAsync_RejectsCorrectionRequiredExceptionItems(
        PaymentReconciliationReviewDecision decision)
    {
        await using var dbContext = TestDb.Create();
        var compound = await SeedCompoundAsync(dbContext);
        var service = new PaymentReconciliationService(dbContext);
        var created = await service.CreateBatchAsync(
            Guid.NewGuid(),
            CreateRequest(compound.Id, "ZainCashMock", $"TX-CORRECTION-{decision}", 100m, PaymentStatus.Succeeded));
        var item = created.Value!.Items.Single();

        await service.ReviewItemAsync(
            Guid.NewGuid(),
            created.Value.Id,
            item.Id,
            new ReviewPaymentReconciliationItemRequest
            {
                Decision = decision,
                ReviewNotes = "This item still requires correction before closure."
            });

        var closed = await service.CloseBatchAsync(
            Guid.NewGuid(),
            created.Value.Id,
            new ClosePaymentReconciliationBatchRequest { Notes = "Attempting premature closure." });

        closed.Status.Should().Be(ServiceResultStatus.Conflict);
        closed.Message.Should().Contain("Correction-required");
    }

    [Fact]
    public async Task CloseBatchAsync_ClosesAfterExceptionItemsAreReviewedAndRejectsSecondClose()
    {
        await using var dbContext = TestDb.Create();
        var compound = await SeedCompoundAsync(dbContext);
        var service = new PaymentReconciliationService(dbContext);
        var created = await service.CreateBatchAsync(
            Guid.NewGuid(),
            CreateRequest(compound.Id, "ZainCashMock", "TX-CLOSE", 100m, PaymentStatus.Succeeded));
        var item = created.Value!.Items.Single();

        await service.ReviewItemAsync(
            Guid.NewGuid(),
            created.Value.Id,
            item.Id,
            new ReviewPaymentReconciliationItemRequest
            {
                Decision = PaymentReconciliationReviewDecision.AcceptedAsProviderException,
                ReviewNotes = "Accepted by finance manager after provider statement check."
            });

        var closed = await service.CloseBatchAsync(
            Guid.NewGuid(),
            created.Value.Id,
            new ClosePaymentReconciliationBatchRequest { Notes = "Reviewed." });
        var secondClose = await service.CloseBatchAsync(
            Guid.NewGuid(),
            created.Value.Id,
            new ClosePaymentReconciliationBatchRequest());

        closed.Status.Should().Be(ServiceResultStatus.Success);
        closed.Value!.Status.Should().Be(PaymentReconciliationBatchStatus.Closed);
        closed.Value.ClosedAtUtc.Should().NotBeNull();
        closed.Value.UnreviewedIssueItems.Should().Be(0);
        secondClose.Status.Should().Be(ServiceResultStatus.Conflict);
    }

    private static CreatePaymentReconciliationBatchRequest CreateRequest(
        Guid compoundId,
        string provider,
        string providerTransactionId,
        decimal providerAmount,
        PaymentStatus providerStatus)
    {
        return new CreatePaymentReconciliationBatchRequest
        {
            CompoundId = compoundId,
            Provider = provider,
            StatementReference = $"ST-{Guid.NewGuid():N}",
            StatementDate = new DateOnly(2026, 6, 17),
            Items =
            [
                new CreatePaymentReconciliationItemRequest
                {
                    ProviderTransactionId = providerTransactionId,
                    ProviderAmount = providerAmount,
                    ProviderStatus = providerStatus
                }
            ]
        };
    }

    private static async Task<PaymentSeed> SeedSucceededPaymentAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        decimal amount,
        string providerTransactionId)
    {
        var compound = await SeedCompoundAsync(dbContext);
        var resident = new ResidentProfile
        {
            CompoundId = compound.Id,
            UserId = Guid.NewGuid(),
            FullName = "Resident One"
        };
        var payment = new Payment
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = Guid.NewGuid(),
            PaymentMethod = PaymentMethod.ZainCashMock,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = amount,
            PaymentReference = $"PAY-{Guid.NewGuid():N}"[..20],
            CompletedAt = DateTime.UtcNow
        };
        var attempt = new PaymentAttempt
        {
            PaymentId = payment.Id,
            AttemptStatus = PaymentStatus.Succeeded,
            Provider = "ZainCashMock",
            ProviderTransactionId = providerTransactionId,
            Message = "Confirmed."
        };
        var receipt = new Receipt
        {
            PaymentId = payment.Id,
            ReceiptNumber = $"REC-{Guid.NewGuid():N}"[..20],
            Amount = amount
        };
        var ledgerEntry = new ResidentLedgerEntry
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            Direction = FinancialLedgerEntryDirection.Credit,
            SourceType = FinancialLedgerSourceType.Payment,
            SourceId = payment.Id,
            Amount = amount,
            Reference = payment.PaymentReference,
            Description = "Payment received"
        };

        dbContext.AddRange(resident, payment, attempt, receipt, ledgerEntry);
        await dbContext.SaveChangesAsync();

        return new PaymentSeed(compound.Id, payment.Id, attempt.Id);
    }

    private static async Task<Compound> SeedCompoundAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        string name = "Darak",
        string? code = null)
    {
        var compound = new Compound
        {
            Name = name,
            Code = code ?? Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private sealed record PaymentSeed(Guid CompoundId, Guid PaymentId, Guid PaymentAttemptId);
}
