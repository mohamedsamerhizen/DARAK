using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.DTOs.Rents;
using DARAK.Api.DTOs.UtilityBills;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class Phase2BPaymentSafetyBillingCorrectnessTests
{
    [Fact]
    public async Task StartResidentPaymentAsync_ReusesVisiblePaymentWithSameIdempotencyKey()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUtilityBillAsync(dbContext, totalAmount: 100m);
        var service = new PaymentService(dbContext);
        var request = new StartPaymentRequest
        {
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = seed.UtilityBillId,
            PaymentMethod = PaymentMethod.ZainCashMock,
            Amount = 25m,
            IdempotencyKey = "resident-start-key-2b"
        };

        var first = await service.StartResidentPaymentAsync(seed.UserId, request);
        var second = await service.StartResidentPaymentAsync(seed.UserId, request);

        first.Status.Should().Be(ServiceResultStatus.Success);
        second.Status.Should().Be(ServiceResultStatus.Success);
        second.Value!.Id.Should().Be(first.Value!.Id);
        first.Value.IdempotencyKey.Should().BeNull();
        second.Value.IdempotencyKey.Should().BeNull();
        (await dbContext.Payments.CountAsync(payment => payment.IdempotencyKey == request.IdempotencyKey)).Should().Be(1);
    }

    [Fact]
    public async Task GenerateUtilityBillAsync_PreviousBalanceSumsAllPriorOpenBills()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUtilityBillGenerationBaseAsync(dbContext);
        await SeedPriorUtilityBillAsync(dbContext, seed, 2026, 3, totalAmount: 100m, paidAmount: 40m, status: BillStatus.PartiallyPaid);
        await SeedPriorUtilityBillAsync(dbContext, seed, 2026, 4, totalAmount: 80m, paidAmount: 10m, status: BillStatus.PartiallyPaid);
        await SeedPriorUtilityBillAsync(dbContext, seed, 2026, 5, totalAmount: 20m, paidAmount: 20m, status: BillStatus.Paid);
        var service = new UtilityBillService(dbContext);

        var result = await service.GenerateUtilityBillAsync(new GenerateUtilityBillRequest
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.PropertyUnitId,
            BillingCycleId = seed.CurrentBillingCycleId,
            Lines =
            [
                new AddUtilityBillLineRequest
                {
                    CompoundServiceId = seed.CompoundServiceId,
                    Quantity = 1m,
                    UnitPrice = 30m
                }
            ]
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.PreviousBalanceAmount.Should().Be(130m);
        result.Value.TotalAmount.Should().Be(160m);
    }

    [Fact]
    public async Task GenerateUtilityBillAsync_PreviousBalanceDoesNotCascadeAlreadyCarriedForwardAmounts()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUtilityBillGenerationBaseAsync(dbContext);
        await SeedPriorUtilityBillAsync(dbContext, seed, 2026, 3, totalAmount: 100m, paidAmount: 0m, status: BillStatus.Unpaid);
        await SeedPriorUtilityBillAsync(
            dbContext,
            seed,
            2026,
            4,
            totalAmount: 130m,
            paidAmount: 0m,
            status: BillStatus.Unpaid,
            previousBalanceAmount: 100m);
        var service = new UtilityBillService(dbContext);

        var result = await service.GenerateUtilityBillAsync(new GenerateUtilityBillRequest
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.PropertyUnitId,
            BillingCycleId = seed.CurrentBillingCycleId,
            Lines =
            [
                new AddUtilityBillLineRequest
                {
                    CompoundServiceId = seed.CompoundServiceId,
                    Quantity = 1m,
                    UnitPrice = 30m
                }
            ]
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.PreviousBalanceAmount.Should().Be(130m);
        result.Value.TotalAmount.Should().Be(160m);
    }

    [Fact]
    public async Task GenerateMonthlyRentInvoicesAsync_PreviousBalanceSumsAllPriorOpenInvoices()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedRentContractAsync(dbContext);
        await SeedPriorRentInvoiceAsync(dbContext, seed, 2026, 3, totalAmount: 100m, paidAmount: 40m, status: RentInvoiceStatus.PartiallyPaid);
        await SeedPriorRentInvoiceAsync(dbContext, seed, 2026, 4, totalAmount: 80m, paidAmount: 10m, status: RentInvoiceStatus.PartiallyPaid);
        await SeedPriorRentInvoiceAsync(dbContext, seed, 2026, 5, totalAmount: 20m, paidAmount: 20m, status: RentInvoiceStatus.Paid);
        var service = new RentInvoiceService(dbContext);

        var result = await service.GenerateMonthlyRentInvoicesAsync(new GenerateMonthlyRentInvoicesRequest
        {
            CompoundId = seed.CompoundId,
            Year = 2026,
            Month = 6,
            IssueDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 6, 30),
            IncludePreviousBalance = true
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.CreatedCount.Should().Be(1);
        var invoice = await dbContext.RentInvoices.SingleAsync(item => item.Year == 2026 && item.Month == 6);
        invoice.PreviousBalanceAmount.Should().Be(130m);
        invoice.TotalAmount.Should().Be(seed.MonthlyRentAmount + 130m);
    }

    [Fact]
    public async Task GenerateMonthlyRentInvoicesAsync_PreviousBalanceDoesNotCascadeAlreadyCarriedForwardAmounts()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedRentContractAsync(dbContext);
        await SeedPriorRentInvoiceAsync(dbContext, seed, 2026, 3, totalAmount: 100m, paidAmount: 0m, status: RentInvoiceStatus.Unpaid);
        await SeedPriorRentInvoiceAsync(
            dbContext,
            seed,
            2026,
            4,
            totalAmount: 300m,
            paidAmount: 0m,
            status: RentInvoiceStatus.Unpaid,
            previousBalanceAmount: 100m);
        var service = new RentInvoiceService(dbContext);

        var result = await service.GenerateMonthlyRentInvoicesAsync(new GenerateMonthlyRentInvoicesRequest
        {
            CompoundId = seed.CompoundId,
            Year = 2026,
            Month = 6,
            IssueDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 6, 30),
            IncludePreviousBalance = true
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        var invoice = await dbContext.RentInvoices.SingleAsync(item => item.Year == 2026 && item.Month == 6);
        invoice.PreviousBalanceAmount.Should().Be(300m);
        invoice.TotalAmount.Should().Be(seed.MonthlyRentAmount + 300m);
    }

    [Fact]
    public async Task GenerateMonthlyRentInvoicesAsync_SkipsExistingInvoiceWithinTransactionScope()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedRentContractAsync(dbContext);
        await SeedPriorRentInvoiceAsync(dbContext, seed, 2026, 6, totalAmount: seed.MonthlyRentAmount, paidAmount: 0m, status: RentInvoiceStatus.Unpaid);
        var service = new RentInvoiceService(dbContext);

        var result = await service.GenerateMonthlyRentInvoicesAsync(new GenerateMonthlyRentInvoicesRequest
        {
            CompoundId = seed.CompoundId,
            Year = 2026,
            Month = 6,
            IssueDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 6, 30)
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.CreatedCount.Should().Be(0);
        result.Value.SkippedCount.Should().Be(1);
        (await dbContext.RentInvoices.CountAsync(item => item.RentContractId == seed.RentContractId && item.Year == 2026 && item.Month == 6)).Should().Be(1);
    }

    [Fact]
    public async Task GetRevenueSummaryAsync_UsesRefundedAtForRefundedAmount()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedBasicResidentAsync(dbContext);
        dbContext.Payments.Add(new Payment
        {
            CompoundId = seed.CompoundId,
            ResidentProfileId = seed.ResidentProfileId,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = Guid.NewGuid(),
            PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Refunded,
            Amount = 75m,
            Currency = "IQD",
            PaymentReference = "PAY-REF-DATE",
            CompletedAt = new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc),
            RefundedAt = new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc)
        });
        await dbContext.SaveChangesAsync();
        var service = CreateFinancialControlService(dbContext, seed.CompoundId);

        var result = await service.GetRevenueSummaryAsync(new RevenueSummaryQuery
        {
            CompoundId = seed.CompoundId,
            FromDate = new DateOnly(2026, 6, 1),
            ToDate = new DateOnly(2026, 6, 30)
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.CollectedAmount.Should().Be(0m);
        result.Value.RefundedAmount.Should().Be(75m);
        result.Value.NetCollectedAmount.Should().Be(-75m);
    }

    [Fact]
    public async Task GetDashboardAsync_IncludesAppliedDebitAdjustmentInTotalOutstanding()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedBasicResidentAsync(dbContext);
        dbContext.FinancialAdjustments.Add(new FinancialAdjustment
        {
            CompoundId = seed.CompoundId,
            ResidentProfileId = seed.ResidentProfileId,
            AdjustmentType = FinancialAdjustmentType.Debit,
            Status = FinancialAdjustmentStatus.Applied,
            Amount = 45m,
            Currency = "IQD",
            Reason = "Manual debit correction",
            RequestedByUserId = Guid.NewGuid(),
            AppliedByUserId = Guid.NewGuid(),
            CreatedAtUtc = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
            AppliedAtUtc = new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc)
        });
        await dbContext.SaveChangesAsync();
        var service = CreateFinancialControlService(dbContext, seed.CompoundId);

        var result = await service.GetDashboardAsync(new FinancialDashboardQuery
        {
            CompoundId = seed.CompoundId,
            FromDate = new DateOnly(2026, 6, 1),
            ToDate = new DateOnly(2026, 6, 30)
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.TotalOutstandingAmount.Should().Be(45m);
        result.Value.DebitAdjustmentsAppliedAmount.Should().Be(45m);
    }

    private static FinancialControlService CreateFinancialControlService(DARAK.Api.Data.ApplicationDbContext dbContext, Guid compoundId)
    {
        var access = new FakeCompoundAccessService([compoundId]);
        return new FinancialControlService(
            dbContext,
            access,
            new AuditLogService(dbContext, access, new HttpContextAccessor()));
    }

    private static async Task<BasicResidentSeed> SeedBasicResidentAsync(DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        var compound = new Compound
        {
            Name = "Phase 2B Compound",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };
        var resident = new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            CompoundId = compound.Id,
            FullName = "Phase 2B Resident"
        };

        dbContext.AddRange(compound, resident);
        await dbContext.SaveChangesAsync();
        return new BasicResidentSeed(compound.Id, resident.Id, resident.UserId);
    }

    private static async Task<UtilityPaymentSeed> SeedUtilityBillAsync(DARAK.Api.Data.ApplicationDbContext dbContext, decimal totalAmount)
    {
        var baseSeed = await SeedUtilityBillGenerationBaseAsync(dbContext);
        var bill = new UtilityBill
        {
            CompoundId = baseSeed.CompoundId,
            PropertyUnitId = baseSeed.PropertyUnitId,
            ResidentProfileId = baseSeed.ResidentProfileId,
            BillingCycleId = baseSeed.CurrentBillingCycleId,
            BillNumber = "PAY-UB-2B",
            IssueDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 6, 30),
            SubtotalAmount = totalAmount,
            TotalAmount = totalAmount,
            BillStatus = BillStatus.Unpaid
        };
        dbContext.UtilityBills.Add(bill);
        await dbContext.SaveChangesAsync();
        return new UtilityPaymentSeed(baseSeed.CompoundId, baseSeed.ResidentProfileId, baseSeed.UserId, bill.Id);
    }

    private static async Task<UtilityBillGenerationSeed> SeedUtilityBillGenerationBaseAsync(DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        var compound = new Compound
        {
            Name = "Utility Compound",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };
        var unit = new PropertyUnit
        {
            CompoundId = compound.Id,
            UnitNumber = "U-2B",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied,
            IsActive = true
        };
        var resident = new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            CompoundId = compound.Id,
            FullName = "Utility Resident"
        };
        var currentCycle = new BillingCycle
        {
            CompoundId = compound.Id,
            Year = 2026,
            Month = 6,
            PeriodStart = new DateOnly(2026, 6, 1),
            PeriodEnd = new DateOnly(2026, 6, 30),
            DueDate = new DateOnly(2026, 7, 10)
        };
        var service = new CompoundService
        {
            CompoundId = compound.Id,
            ServiceType = UtilityServiceType.Maintenance,
            Name = "Maintenance",
            DefaultMonthlyFee = 30m,
            IsMeterBased = false,
            IsActive = true
        };
        var occupancy = new OccupancyRecord
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = new DateOnly(2026, 1, 1)
        };

        dbContext.AddRange(compound, unit, resident, currentCycle, service, occupancy);
        await dbContext.SaveChangesAsync();
        return new UtilityBillGenerationSeed(compound.Id, unit.Id, resident.Id, resident.UserId, currentCycle.Id, service.Id);
    }

    private static async Task SeedPriorUtilityBillAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        UtilityBillGenerationSeed seed,
        int year,
        int month,
        decimal totalAmount,
        decimal paidAmount,
        BillStatus status,
        decimal previousBalanceAmount = 0m)
    {
        var cycle = new BillingCycle
        {
            CompoundId = seed.CompoundId,
            Year = year,
            Month = month,
            PeriodStart = new DateOnly(year, month, 1),
            PeriodEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
            DueDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month))
        };
        var bill = new UtilityBill
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.PropertyUnitId,
            ResidentProfileId = seed.ResidentProfileId,
            BillingCycleId = cycle.Id,
            BillNumber = $"UB-{year}-{month}",
            IssueDate = cycle.PeriodStart,
            DueDate = cycle.DueDate,
            SubtotalAmount = totalAmount,
            PreviousBalanceAmount = previousBalanceAmount,
            TotalAmount = totalAmount,
            PaidAmount = paidAmount,
            BillStatus = status
        };

        dbContext.AddRange(cycle, bill);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<RentSeed> SeedRentContractAsync(DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        var compound = new Compound
        {
            Name = "Rent Compound",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };
        var unit = new PropertyUnit
        {
            CompoundId = compound.Id,
            UnitNumber = "R-2B",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Rented,
            IsActive = true
        };
        var resident = new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            CompoundId = compound.Id,
            FullName = "Rent Resident"
        };
        var contract = new RentContract
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            ContractNumber = "RC-2B",
            ContractStatus = RentContractStatus.Active,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            MonthlyRentAmount = 200m
        };

        dbContext.AddRange(compound, unit, resident, contract);
        await dbContext.SaveChangesAsync();
        return new RentSeed(compound.Id, contract.Id, resident.Id, unit.Id, contract.MonthlyRentAmount);
    }

    private static async Task SeedPriorRentInvoiceAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        RentSeed seed,
        int year,
        int month,
        decimal totalAmount,
        decimal paidAmount,
        RentInvoiceStatus status,
        decimal previousBalanceAmount = 0m)
    {
        dbContext.RentInvoices.Add(new RentInvoice
        {
            RentContractId = seed.RentContractId,
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.PropertyUnitId,
            ResidentProfileId = seed.ResidentProfileId,
            InvoiceNumber = $"RI-{year}-{month}",
            Year = year,
            Month = month,
            IssueDate = new DateOnly(year, month, 1),
            DueDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
            RentAmount = totalAmount,
            PreviousBalanceAmount = previousBalanceAmount,
            TotalAmount = totalAmount,
            PaidAmount = paidAmount,
            RentInvoiceStatus = status
        });
        await dbContext.SaveChangesAsync();
    }

    private sealed record BasicResidentSeed(Guid CompoundId, Guid ResidentProfileId, Guid UserId);
    private sealed record UtilityPaymentSeed(Guid CompoundId, Guid ResidentProfileId, Guid UserId, Guid UtilityBillId);
    private sealed record UtilityBillGenerationSeed(Guid CompoundId, Guid PropertyUnitId, Guid ResidentProfileId, Guid UserId, Guid CurrentBillingCycleId, Guid CompoundServiceId);
    private sealed record RentSeed(Guid CompoundId, Guid RentContractId, Guid ResidentProfileId, Guid PropertyUnitId, decimal MonthlyRentAmount);
}
