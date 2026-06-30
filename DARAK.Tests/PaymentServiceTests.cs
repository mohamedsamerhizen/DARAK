using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task StartResidentPaymentAsync_RejectsAnotherResidentsUtilityBill()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUtilityBillAsync(dbContext);
        var service = new PaymentService(dbContext);

        var result = await service.StartResidentPaymentAsync(
            seed.OtherUserId,
            new StartPaymentRequest
            {
                TargetType = PaymentTargetType.UtilityBill,
                TargetId = seed.UtilityBillId,
                PaymentMethod = PaymentMethod.ZainCashMock,
                Amount = 25m
            });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task ConfirmResidentMockPaymentSuccessAsync_ConfirmsPendingPaymentAndUpdatesBillStatus()
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
            Amount = 40m,
            PaymentReference = "PAY-TEST"
        };
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var service = new PaymentService(dbContext);
        var result = await service.ConfirmResidentMockPaymentSuccessAsync(
            seed.UserId,
            payment.Id,
            PaymentMethod.ZainCashMock,
            new ConfirmMockPaymentRequest { ProviderTransactionId = "TX-1" });

        result.Status.Should().Be(ServiceResultStatus.Success);
        var bill = await dbContext.UtilityBills.SingleAsync(item => item.Id == seed.UtilityBillId);
        bill.PaidAmount.Should().Be(40m);
        bill.BillStatus.Should().Be(BillStatus.PartiallyPaid);
    }

    [Theory]
    [InlineData(PaymentStatus.Succeeded)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Cancelled)]
    [InlineData(PaymentStatus.Refunded)]
    public async Task ConfirmResidentMockPaymentSuccessAsync_RejectsNonPendingPayments(PaymentStatus status)
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
            PaymentStatus = status,
            Amount = 40m,
            PaymentReference = $"PAY-{status}"
        };
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var service = new PaymentService(dbContext);
        var result = await service.ConfirmResidentMockPaymentSuccessAsync(
            seed.UserId,
            payment.Id,
            PaymentMethod.ZainCashMock,
            new ConfirmMockPaymentRequest());

        result.Status.Should().Be(ServiceResultStatus.Conflict);
    }

    [Fact]
    public async Task ConfirmResidentMockPaymentSuccessAsync_RejectsPaymentMethodMismatch()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUtilityBillAsync(dbContext);
        var payment = new Payment
        {
            CompoundId = seed.CompoundId,
            ResidentProfileId = seed.ResidentProfileId,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = seed.UtilityBillId,
            PaymentMethod = PaymentMethod.MasterCardMock,
            PaymentStatus = PaymentStatus.Pending,
            Amount = 40m,
            PaymentReference = "PAY-MISMATCH"
        };
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var service = new PaymentService(dbContext);
        var result = await service.ConfirmResidentMockPaymentSuccessAsync(
            seed.UserId,
            payment.Id,
            PaymentMethod.ZainCashMock,
            new ConfirmMockPaymentRequest());

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task ConfirmResidentMockPaymentSuccessAsync_RejectsManualPaymentMethods()
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
            PaymentStatus = PaymentStatus.Pending,
            Amount = 40m,
            PaymentReference = "PAY-CASH"
        };
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var service = new PaymentService(dbContext);
        var result = await service.ConfirmResidentMockPaymentSuccessAsync(
            seed.UserId,
            payment.Id,
            PaymentMethod.ZainCashMock,
            new ConfirmMockPaymentRequest());

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Theory]
    [InlineData(PaymentTargetType.RentInvoice)]
    [InlineData(PaymentTargetType.PropertyInstallment)]
    [InlineData(PaymentTargetType.ViolationFine)]
    public async Task RecordManualPaymentAsync_SetsFinancialTargetToPartialThenPaid(PaymentTargetType targetType)
    {
        await using var dbContext = TestDb.Create();
        var target = await SeedFinancialTargetAsync(dbContext, targetType);
        var service = new PaymentService(dbContext);

        var first = await service.RecordManualPaymentAsync(new ManualPaymentRequest
        {
            TargetType = targetType,
            TargetId = target.TargetId,
            PaymentMethod = PaymentMethod.Cash,
            Amount = 40m
        });
        var second = await service.RecordManualPaymentAsync(new ManualPaymentRequest
        {
            TargetType = targetType,
            TargetId = target.TargetId,
            PaymentMethod = PaymentMethod.Cash,
            Amount = 60m
        });

        first.Status.Should().Be(ServiceResultStatus.Success);
        second.Status.Should().Be(ServiceResultStatus.Success);
        await AssertTargetPaidAsync(dbContext, targetType, target.TargetId);
    }

    [Fact]
    public async Task SearchPaymentsAsync_ReturnsOnlyPaymentsInAllowedCompounds()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedPaymentsInTwoCompoundsAsync(dbContext);
        var service = new PaymentService(dbContext, new FakeCompoundAccessService([seed.AllowedCompoundId]));

        var result = await service.SearchPaymentsAsync(new PaymentSearchQuery());

        result.TotalCount.Should().Be(1);
        result.Items.Single().CompoundId.Should().Be(seed.AllowedCompoundId);
    }

    [Fact]
    public async Task SearchPaymentsAsync_ReturnsAllPaymentsForSuperAdmin()
    {
        await using var dbContext = TestDb.Create();
        await SeedPaymentsInTwoCompoundsAsync(dbContext);
        var service = new PaymentService(
            dbContext,
            new FakeCompoundAccessService(isSuperAdmin: true));

        var result = await service.SearchPaymentsAsync(new PaymentSearchQuery());

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPaymentAsync_ReturnsNotFoundForUnassignedCompound()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedPaymentsInTwoCompoundsAsync(dbContext);
        var service = new PaymentService(dbContext, new FakeCompoundAccessService([seed.AllowedCompoundId]));

        var result = await service.GetPaymentAsync(seed.BlockedPaymentId);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task RefundPaymentAsync_ReturnsNotFoundForUnassignedCompound()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedPaymentsInTwoCompoundsAsync(dbContext);
        var service = new PaymentService(dbContext, new FakeCompoundAccessService([seed.AllowedCompoundId]));

        var result = await service.RefundPaymentAsync(
            seed.BlockedPaymentId,
            new RefundPaymentRequest { Reason = "Wrong payment" });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task RecordManualPaymentAsync_ReturnsForbiddenForUnassignedPaymentTarget()
    {
        await using var dbContext = TestDb.Create();
        var target = await SeedFinancialTargetAsync(dbContext, PaymentTargetType.RentInvoice);
        var service = new PaymentService(dbContext, new FakeCompoundAccessService());

        var result = await service.RecordManualPaymentAsync(new ManualPaymentRequest
        {
            TargetType = PaymentTargetType.RentInvoice,
            TargetId = target.TargetId,
            PaymentMethod = PaymentMethod.Cash,
            Amount = 25m
        });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
    }

    [Fact]
    public async Task RecordManualPaymentAsync_AllowsAssignedPaymentTarget()
    {
        await using var dbContext = TestDb.Create();
        var target = await SeedFinancialTargetAsync(dbContext, PaymentTargetType.RentInvoice);
        var service = new PaymentService(
            dbContext,
            new FakeCompoundAccessService([target.CompoundId]));

        var result = await service.RecordManualPaymentAsync(new ManualPaymentRequest
        {
            TargetType = PaymentTargetType.RentInvoice,
            TargetId = target.TargetId,
            PaymentMethod = PaymentMethod.Cash,
            Amount = 25m
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
    }

    [Fact]
    public async Task RecordManualPaymentAsync_ReusesSameIdempotencyKeyWithoutDuplicatingPayment()
    {
        await using var dbContext = TestDb.Create();
        var target = await SeedFinancialTargetAsync(dbContext, PaymentTargetType.RentInvoice);
        var service = new PaymentService(dbContext, new FakeCompoundAccessService([target.CompoundId]));
        var request = new ManualPaymentRequest
        {
            TargetType = PaymentTargetType.RentInvoice,
            TargetId = target.TargetId,
            PaymentMethod = PaymentMethod.Cash,
            Amount = 25m,
            IdempotencyKey = "manual-idempotency-key-1"
        };

        var first = await service.RecordManualPaymentAsync(request);
        var second = await service.RecordManualPaymentAsync(request);

        first.Status.Should().Be(ServiceResultStatus.Success);
        second.Status.Should().Be(ServiceResultStatus.Success);
        second.Value!.Id.Should().Be(first.Value!.Id);
        dbContext.Payments.Count(payment => payment.IdempotencyKey == request.IdempotencyKey).Should().Be(1);
        (await dbContext.RentInvoices.SingleAsync(item => item.Id == target.TargetId)).PaidAmount.Should().Be(25m);
    }

    [Fact]
    public async Task RecordManualPaymentAsync_RejectsIdempotencyKeyReusedForDifferentPayment()
    {
        await using var dbContext = TestDb.Create();
        var target = await SeedFinancialTargetAsync(dbContext, PaymentTargetType.RentInvoice);
        var service = new PaymentService(dbContext, new FakeCompoundAccessService([target.CompoundId]));
        await service.RecordManualPaymentAsync(new ManualPaymentRequest
        {
            TargetType = PaymentTargetType.RentInvoice,
            TargetId = target.TargetId,
            PaymentMethod = PaymentMethod.Cash,
            Amount = 25m,
            IdempotencyKey = "manual-idempotency-key-2"
        });

        var result = await service.RecordManualPaymentAsync(new ManualPaymentRequest
        {
            TargetType = PaymentTargetType.RentInvoice,
            TargetId = target.TargetId,
            PaymentMethod = PaymentMethod.Cash,
            Amount = 30m,
            IdempotencyKey = "manual-idempotency-key-2"
        });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        dbContext.Payments.Count(payment => payment.IdempotencyKey == "manual-idempotency-key-2").Should().Be(1);
    }

    [Fact]
    public async Task ConfirmResidentMockPaymentSuccessAsync_GeneratesDeterministicProviderTransactionId()
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
            Amount = 40m,
            PaymentReference = "PAY-MOCK-GEN"
        };
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        var service = new PaymentService(dbContext);

        var result = await service.ConfirmResidentMockPaymentSuccessAsync(
            seed.UserId,
            payment.Id,
            PaymentMethod.ZainCashMock,
            new ConfirmMockPaymentRequest());

        result.Status.Should().Be(ServiceResultStatus.Success);
        dbContext.PaymentAttempts.Should().ContainSingle(attempt =>
            attempt.PaymentId == payment.Id
            && attempt.ProviderTransactionId == $"MOCK-ZainCashMock-{payment.Id:N}");
    }

    [Fact]
    public async Task ConfirmResidentMockPaymentSuccessAsync_RejectsDuplicateProviderTransactionIdAcrossPayments()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUtilityBillAsync(dbContext);
        var firstPayment = new Payment
        {
            CompoundId = seed.CompoundId,
            ResidentProfileId = seed.ResidentProfileId,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = seed.UtilityBillId,
            PaymentMethod = PaymentMethod.ZainCashMock,
            PaymentStatus = PaymentStatus.Pending,
            Amount = 10m,
            PaymentReference = "PAY-MOCK-DUP-1"
        };
        var secondPayment = new Payment
        {
            CompoundId = seed.CompoundId,
            ResidentProfileId = seed.ResidentProfileId,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = seed.UtilityBillId,
            PaymentMethod = PaymentMethod.ZainCashMock,
            PaymentStatus = PaymentStatus.Pending,
            Amount = 10m,
            PaymentReference = "PAY-MOCK-DUP-2"
        };
        dbContext.Payments.AddRange(firstPayment, secondPayment);
        await dbContext.SaveChangesAsync();
        var service = new PaymentService(dbContext);
        await service.ConfirmResidentMockPaymentSuccessAsync(
            seed.UserId,
            firstPayment.Id,
            PaymentMethod.ZainCashMock,
            new ConfirmMockPaymentRequest { ProviderTransactionId = "PROVIDER-DUP-1" });

        var result = await service.ConfirmResidentMockPaymentSuccessAsync(
            seed.UserId,
            secondPayment.Id,
            PaymentMethod.ZainCashMock,
            new ConfirmMockPaymentRequest { ProviderTransactionId = "PROVIDER-DUP-1" });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        dbContext.PaymentAttempts.Count(attempt => attempt.ProviderTransactionId == "PROVIDER-DUP-1").Should().Be(1);
    }

    private static async Task<UtilitySeed> SeedUtilityBillAsync(DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        var compound = new Compound { Name = "Darak", Code = "D1", City = "Baghdad", Area = "Karrada" };
        var unit = new PropertyUnit
        {
            CompoundId = compound.Id,
            UnitNumber = "A-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied
        };
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var resident = new ResidentProfile
        {
            UserId = userId,
            CompoundId = compound.Id,
            FullName = "Resident One"
        };
        var otherResident = new ResidentProfile
        {
            UserId = otherUserId,
            CompoundId = compound.Id,
            FullName = "Resident Two"
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
            BillNumber = "UB-1",
            IssueDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 7, 10),
            SubtotalAmount = 100m,
            TotalAmount = 100m,
            BillStatus = BillStatus.Unpaid
        };

        dbContext.AddRange(compound, unit, resident, otherResident, cycle, bill);
        await dbContext.SaveChangesAsync();

        return new UtilitySeed(compound.Id, unit.Id, resident.Id, userId, otherUserId, bill.Id);
    }

    private static async Task<TargetSeed> SeedFinancialTargetAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        PaymentTargetType targetType)
    {
        var compound = new Compound { Name = "Darak", Code = "D1", City = "Baghdad", Area = "Karrada" };
        var unit = new PropertyUnit
        {
            CompoundId = compound.Id,
            UnitNumber = "A-101",
            PropertyType = PropertyType.Apartment
        };
        var resident = new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            CompoundId = compound.Id,
            FullName = "Resident One"
        };
        dbContext.AddRange(compound, unit, resident);

        Guid targetId;
        switch (targetType)
        {
            case PaymentTargetType.RentInvoice:
                var rentContract = new RentContract
                {
                    CompoundId = compound.Id,
                    PropertyUnitId = unit.Id,
                    ResidentProfileId = resident.Id,
                    ContractNumber = "RC-1",
                    StartDate = new DateOnly(2026, 1, 1),
                    EndDate = new DateOnly(2026, 12, 31),
                    MonthlyRentAmount = 100m
                };
                var invoice = new RentInvoice
                {
                    RentContractId = rentContract.Id,
                    CompoundId = compound.Id,
                    PropertyUnitId = unit.Id,
                    ResidentProfileId = resident.Id,
                    InvoiceNumber = "RINV-1",
                    Year = 2026,
                    Month = 6,
                    IssueDate = new DateOnly(2026, 6, 1),
                    DueDate = new DateOnly(2026, 7, 1),
                    RentAmount = 100m,
                    TotalAmount = 100m,
                    RentInvoiceStatus = RentInvoiceStatus.Unpaid
                };
                dbContext.AddRange(rentContract, invoice);
                targetId = invoice.Id;
                break;
            case PaymentTargetType.PropertyInstallment:
                var saleContract = new PropertySaleContract
                {
                    CompoundId = compound.Id,
                    PropertyUnitId = unit.Id,
                    ResidentProfileId = resident.Id,
                    SaleType = SaleType.Installment,
                    ContractNumber = "SC-1",
                    ContractDate = new DateOnly(2026, 1, 1),
                    PropertyPrice = 100m,
                    InstallmentCount = 1
                };
                var installment = new InstallmentScheduleItem
                {
                    PropertySaleContractId = saleContract.Id,
                    CompoundId = compound.Id,
                    PropertyUnitId = unit.Id,
                    ResidentProfileId = resident.Id,
                    InstallmentNumber = 1,
                    DueDate = new DateOnly(2026, 7, 1),
                    Amount = 100m,
                    InstallmentStatus = InstallmentStatus.Pending
                };
                dbContext.AddRange(saleContract, installment);
                targetId = installment.Id;
                break;
            case PaymentTargetType.ViolationFine:
                var violation = new Violation
                {
                    CompoundId = compound.Id,
                    ResidentProfileId = resident.Id,
                    Title = "Noise",
                    Description = "Noise"
                };
                var fine = new ViolationFine
                {
                    ViolationId = violation.Id,
                    CompoundId = compound.Id,
                    ResidentProfileId = resident.Id,
                    Amount = 100m,
                    Reason = "Noise",
                    DueDate = new DateOnly(2026, 7, 1),
                    Status = ViolationFineStatus.Unpaid
                };
                dbContext.AddRange(violation, fine);
                targetId = fine.Id;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(targetType), targetType, null);
        }

        await dbContext.SaveChangesAsync();
        return new TargetSeed(compound.Id, targetId);
    }

    private static async Task<PaymentScopeSeed> SeedPaymentsInTwoCompoundsAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        var allowedCompound = new Compound
        {
            Name = "Allowed",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };
        var blockedCompound = new Compound
        {
            Name = "Blocked",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Mansour"
        };
        var allowedPayment = new Payment
        {
            CompoundId = allowedCompound.Id,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = Guid.NewGuid(),
            PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = 10m,
            PaymentReference = "PAY-ALLOWED"
        };
        var blockedPayment = new Payment
        {
            CompoundId = blockedCompound.Id,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = Guid.NewGuid(),
            PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = 20m,
            PaymentReference = "PAY-BLOCKED"
        };

        dbContext.AddRange(allowedCompound, blockedCompound, allowedPayment, blockedPayment);
        await dbContext.SaveChangesAsync();

        return new PaymentScopeSeed(allowedCompound.Id, blockedPayment.Id);
    }

    private static async Task AssertTargetPaidAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        PaymentTargetType targetType,
        Guid targetId)
    {
        switch (targetType)
        {
            case PaymentTargetType.RentInvoice:
                var invoice = await dbContext.RentInvoices.SingleAsync(item => item.Id == targetId);
                invoice.PaidAmount.Should().Be(100m);
                invoice.RentInvoiceStatus.Should().Be(RentInvoiceStatus.Paid);
                break;
            case PaymentTargetType.PropertyInstallment:
                var installment = await dbContext.InstallmentScheduleItems.SingleAsync(item => item.Id == targetId);
                installment.PaidAmount.Should().Be(100m);
                installment.InstallmentStatus.Should().Be(InstallmentStatus.Paid);
                break;
            case PaymentTargetType.ViolationFine:
                var fine = await dbContext.ViolationFines.SingleAsync(item => item.Id == targetId);
                fine.PaidAmount.Should().Be(100m);
                fine.Status.Should().Be(ViolationFineStatus.Paid);
                break;
        }
    }

    private sealed record UtilitySeed(
        Guid CompoundId,
        Guid UnitId,
        Guid ResidentProfileId,
        Guid UserId,
        Guid OtherUserId,
        Guid UtilityBillId);

    private sealed record TargetSeed(Guid CompoundId, Guid TargetId);

    private sealed record PaymentScopeSeed(Guid AllowedCompoundId, Guid BlockedPaymentId);
}
