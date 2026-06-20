using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class CollectionsLegalCompliancePass11Tests
{
    [Fact]
    public async Task Pass11_CreatePenaltyRuleAsync_RejectsFixedRuleWithPercentageRate()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P11-PEN-1");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreatePenaltyRuleAsync(Guid.NewGuid(), new CreatePenaltyRuleRequest
        {
            CompoundId = compound.Id,
            Name = "Invalid fixed rule",
            TargetType = PenaltyRuleTargetType.UtilityBill,
            CalculationType = PenaltyCalculationType.FixedAmount,
            GracePeriodDays = 3,
            Amount = 10_000m,
            PercentageRate = 5m
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        dbContext.PenaltyRules.Should().BeEmpty();
    }

    [Fact]
    public async Task Pass11_CreatePenaltyRuleAsync_RejectsPercentageRuleWithoutRate()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P11-PEN-2");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreatePenaltyRuleAsync(Guid.NewGuid(), new CreatePenaltyRuleRequest
        {
            CompoundId = compound.Id,
            Name = "Invalid percentage rule",
            TargetType = PenaltyRuleTargetType.RentInvoice,
            CalculationType = PenaltyCalculationType.Percentage,
            GracePeriodDays = 5,
            Amount = 1_000m
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        dbContext.PenaltyRules.Should().BeEmpty();
    }

    [Fact]
    public async Task Pass11_CreateCollectionCaseAsync_RejectsNonManualCaseWithoutSourceId()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P11-SRC-1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Missing Source Resident");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateCollectionCaseAsync(Guid.NewGuid(), new CreateCollectionCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            SourceType = FinancialCollectionSourceType.UtilityBill,
            AmountDue = 100_000m,
            Reason = "Missing source id"
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        dbContext.CollectionCases.Should().BeEmpty();
    }

    [Fact]
    public async Task Pass11_CreateCollectionCaseAsync_RejectsSourceFromAnotherResident()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P11-SRC-2");
        var requestingResident = await AddResidentAsync(dbContext, compound.Id, "Requesting Resident");
        var otherResident = await AddResidentAsync(dbContext, compound.Id, "Other Resident");
        var bill = await AddUtilityBillAsync(dbContext, compound.Id, otherResident.Id, 250_000m, 0m);
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateCollectionCaseAsync(Guid.NewGuid(), new CreateCollectionCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = requestingResident.Id,
            SourceType = FinancialCollectionSourceType.UtilityBill,
            SourceId = bill.Id,
            AmountDue = 250_000m,
            Reason = "Wrong resident source"
        });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        dbContext.CollectionCases.Should().BeEmpty();
    }

    [Fact]
    public async Task Pass11_CreateCollectionCaseAsync_RejectsAmountMismatchForOutstandingSource()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P11-SRC-3");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Amount Mismatch Resident");
        var bill = await AddUtilityBillAsync(dbContext, compound.Id, resident.Id, 500_000m, 100_000m);
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateCollectionCaseAsync(Guid.NewGuid(), new CreateCollectionCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            SourceType = FinancialCollectionSourceType.UtilityBill,
            SourceId = bill.Id,
            AmountDue = 500_000m,
            Reason = "Wrong outstanding amount"
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        dbContext.CollectionCases.Should().BeEmpty();
    }

    [Fact]
    public async Task Pass11_CreateCollectionCaseAsync_AllowsValidOutstandingSource()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P11-SRC-4");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Valid Source Resident");
        var bill = await AddUtilityBillAsync(dbContext, compound.Id, resident.Id, 500_000m, 125_000m);
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateCollectionCaseAsync(Guid.NewGuid(), new CreateCollectionCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            SourceType = FinancialCollectionSourceType.UtilityBill,
            SourceId = bill.Id,
            AmountDue = 375_000m,
            Reason = "Valid outstanding source"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        dbContext.CollectionCases.Should().ContainSingle(item =>
            item.SourceType == FinancialCollectionSourceType.UtilityBill
            && item.SourceId == bill.Id
            && item.AmountDue == 375_000m);
    }

    [Fact]
    public async Task Pass11_PayPaymentPlanInstallmentAsync_RejectsOverpaymentInsteadOfTruncating()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P11-PLAN-1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Overpayment Resident");
        var service = CreateService(dbContext, compound.Id);

        var collectionCase = await service.CreateCollectionCaseAsync(Guid.NewGuid(), new CreateCollectionCaseRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            SourceType = FinancialCollectionSourceType.ManualBalance,
            AmountDue = 100_000m,
            Reason = "Manual balance for plan"
        });

        var plan = await service.CreatePaymentPlanAsync(Guid.NewGuid(), new CreatePaymentPlanRequest
        {
            CollectionCaseId = collectionCase.Value!.Id,
            TotalAmount = 100_000m,
            InstallmentCount = 1,
            StartDate = new DateOnly(2026, 7, 1)
        });

        var installment = plan.Value!.Installments.Single();
        var result = await service.PayPaymentPlanInstallmentAsync(
            plan.Value.Id,
            installment.Id,
            new PayPaymentPlanInstallmentRequest { Amount = 100_001m });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        var persistedInstallment = dbContext.PaymentPlanInstallments.Single();
        persistedInstallment.PaidAmount.Should().Be(0m);
        persistedInstallment.Status.Should().Be(PaymentPlanInstallmentStatus.Pending);
        dbContext.PaymentPlans.Single().Status.Should().Be(PaymentPlanStatus.Active);
        dbContext.CollectionCases.Single().Status.Should().Be(CollectionCaseStatus.PaymentPlanActive);
    }

    private static CollectionsLegalComplianceService CreateService(ApplicationDbContext dbContext, params Guid[] allowedCompoundIds)
    {
        return new CollectionsLegalComplianceService(dbContext, new FakeCompoundAccessService(allowedCompoundIds));
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = new Compound
        {
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Karrada",
            Address = "Baghdad"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<ResidentProfile> AddResidentAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        string fullName)
    {
        var resident = new ResidentProfile
        {
            CompoundId = compoundId,
            FullName = fullName,
            UserId = Guid.NewGuid(),
            IsActive = true
        };

        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }

    private static async Task<UtilityBill> AddUtilityBillAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        Guid residentProfileId,
        decimal totalAmount,
        decimal paidAmount)
    {
        var bill = new UtilityBill
        {
            CompoundId = compoundId,
            PropertyUnitId = Guid.NewGuid(),
            ResidentProfileId = residentProfileId,
            BillingCycleId = Guid.NewGuid(),
            BillNumber = $"UB-P11-{Guid.NewGuid():N}"[..18],
            IssueDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 6, 10),
            TotalAmount = totalAmount,
            PaidAmount = paidAmount,
            BillStatus = paidAmount <= 0 ? BillStatus.Overdue : BillStatus.PartiallyPaid
        };

        dbContext.UtilityBills.Add(bill);
        await dbContext.SaveChangesAsync();
        return bill;
    }
}
