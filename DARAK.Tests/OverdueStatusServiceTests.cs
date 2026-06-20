using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Financial;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class OverdueStatusServiceTests
{
    [Fact]
    public async Task ProcessAsync_MarksEligibleFinancialItemsOverdueAndReturnsCounts()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedFinancialItemsAsync(dbContext);
        var service = new OverdueStatusService(
            dbContext,
            new FakeCompoundAccessService([seed.CompoundId]));

        var result = await service.ProcessAsync(new ProcessOverdueStatusRequest
        {
            CompoundId = seed.CompoundId,
            AsOfDate = new DateOnly(2026, 6, 10)
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.UtilityBillsUpdated.Should().Be(1);
        result.Value.RentInvoicesUpdated.Should().Be(1);
        result.Value.InstallmentsUpdated.Should().Be(1);

        (await dbContext.UtilityBills.SingleAsync(item => item.Id == seed.UtilityBillId))
            .BillStatus.Should().Be(BillStatus.Overdue);
        (await dbContext.RentInvoices.SingleAsync(item => item.Id == seed.RentInvoiceId))
            .RentInvoiceStatus.Should().Be(RentInvoiceStatus.Overdue);
        (await dbContext.InstallmentScheduleItems.SingleAsync(item => item.Id == seed.InstallmentId))
            .InstallmentStatus.Should().Be(InstallmentStatus.Overdue);
    }

    [Theory]
    [InlineData(BillStatus.PartiallyPaid)]
    [InlineData(BillStatus.Paid)]
    [InlineData(BillStatus.Overdue)]
    [InlineData(BillStatus.Cancelled)]
    public async Task ProcessAsync_DoesNotTouchUtilityBillsOutsideUnpaidStatus(BillStatus status)
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedFinancialItemsAsync(dbContext);
        var bill = await dbContext.UtilityBills.SingleAsync(item => item.Id == seed.UtilityBillId);
        bill.BillStatus = status;
        await dbContext.SaveChangesAsync();

        var service = new OverdueStatusService(dbContext, new FakeCompoundAccessService([seed.CompoundId]));
        await service.ProcessAsync(new ProcessOverdueStatusRequest
        {
            CompoundId = seed.CompoundId,
            AsOfDate = new DateOnly(2026, 6, 10)
        });

        (await dbContext.UtilityBills.SingleAsync(item => item.Id == seed.UtilityBillId))
            .BillStatus.Should().Be(status);
    }

    [Theory]
    [InlineData(RentInvoiceStatus.PartiallyPaid)]
    [InlineData(RentInvoiceStatus.Paid)]
    [InlineData(RentInvoiceStatus.Overdue)]
    [InlineData(RentInvoiceStatus.Cancelled)]
    public async Task ProcessAsync_DoesNotTouchRentInvoicesOutsideUnpaidStatus(RentInvoiceStatus status)
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedFinancialItemsAsync(dbContext);
        var invoice = await dbContext.RentInvoices.SingleAsync(item => item.Id == seed.RentInvoiceId);
        invoice.RentInvoiceStatus = status;
        await dbContext.SaveChangesAsync();

        var service = new OverdueStatusService(dbContext, new FakeCompoundAccessService([seed.CompoundId]));
        await service.ProcessAsync(new ProcessOverdueStatusRequest
        {
            CompoundId = seed.CompoundId,
            AsOfDate = new DateOnly(2026, 6, 10)
        });

        (await dbContext.RentInvoices.SingleAsync(item => item.Id == seed.RentInvoiceId))
            .RentInvoiceStatus.Should().Be(status);
    }

    [Theory]
    [InlineData(InstallmentStatus.PartiallyPaid)]
    [InlineData(InstallmentStatus.Paid)]
    [InlineData(InstallmentStatus.Overdue)]
    [InlineData(InstallmentStatus.Cancelled)]
    public async Task ProcessAsync_DoesNotTouchInstallmentsOutsidePendingStatus(InstallmentStatus status)
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedFinancialItemsAsync(dbContext);
        var installment = await dbContext.InstallmentScheduleItems.SingleAsync(item => item.Id == seed.InstallmentId);
        installment.InstallmentStatus = status;
        await dbContext.SaveChangesAsync();

        var service = new OverdueStatusService(dbContext, new FakeCompoundAccessService([seed.CompoundId]));
        await service.ProcessAsync(new ProcessOverdueStatusRequest
        {
            CompoundId = seed.CompoundId,
            AsOfDate = new DateOnly(2026, 6, 10)
        });

        (await dbContext.InstallmentScheduleItems.SingleAsync(item => item.Id == seed.InstallmentId))
            .InstallmentStatus.Should().Be(status);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotTouchPartiallyPaidItems()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedFinancialItemsAsync(dbContext);
        var bill = await dbContext.UtilityBills.SingleAsync(item => item.Id == seed.UtilityBillId);
        var invoice = await dbContext.RentInvoices.SingleAsync(item => item.Id == seed.RentInvoiceId);
        var installment = await dbContext.InstallmentScheduleItems.SingleAsync(item => item.Id == seed.InstallmentId);
        bill.PaidAmount = 1m;
        invoice.PaidAmount = 1m;
        installment.PaidAmount = 1m;
        await dbContext.SaveChangesAsync();

        var service = new OverdueStatusService(dbContext, new FakeCompoundAccessService([seed.CompoundId]));
        var result = await service.ProcessAsync(new ProcessOverdueStatusRequest
        {
            CompoundId = seed.CompoundId,
            AsOfDate = new DateOnly(2026, 6, 10)
        });

        result.Value!.UtilityBillsUpdated.Should().Be(0);
        result.Value.RentInvoicesUpdated.Should().Be(0);
        result.Value.InstallmentsUpdated.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotTouchFutureDueItems()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedFinancialItemsAsync(dbContext);
        var bill = await dbContext.UtilityBills.SingleAsync(item => item.Id == seed.UtilityBillId);
        var invoice = await dbContext.RentInvoices.SingleAsync(item => item.Id == seed.RentInvoiceId);
        var installment = await dbContext.InstallmentScheduleItems.SingleAsync(item => item.Id == seed.InstallmentId);
        bill.DueDate = new DateOnly(2026, 6, 10);
        invoice.DueDate = new DateOnly(2026, 6, 10);
        installment.DueDate = new DateOnly(2026, 6, 10);
        await dbContext.SaveChangesAsync();

        var service = new OverdueStatusService(dbContext, new FakeCompoundAccessService([seed.CompoundId]));
        var result = await service.ProcessAsync(new ProcessOverdueStatusRequest
        {
            CompoundId = seed.CompoundId,
            AsOfDate = new DateOnly(2026, 6, 10)
        });

        result.Value!.UtilityBillsUpdated.Should().Be(0);
        result.Value.RentInvoicesUpdated.Should().Be(0);
        result.Value.InstallmentsUpdated.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsBadRequestForEmptyCompoundId()
    {
        await using var dbContext = TestDb.Create();
        var service = new OverdueStatusService(dbContext);

        var result = await service.ProcessAsync(new ProcessOverdueStatusRequest
        {
            CompoundId = Guid.Empty
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsNotFoundForUnknownCompound()
    {
        await using var dbContext = TestDb.Create();
        var service = new OverdueStatusService(dbContext);

        var result = await service.ProcessAsync(new ProcessOverdueStatusRequest
        {
            CompoundId = Guid.NewGuid()
        });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsForbiddenForUnassignedCompound()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedFinancialItemsAsync(dbContext);
        var service = new OverdueStatusService(dbContext, new FakeCompoundAccessService());

        var result = await service.ProcessAsync(new ProcessOverdueStatusRequest
        {
            CompoundId = seed.CompoundId
        });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
    }

    private static async Task<FinancialSeed> SeedFinancialItemsAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext)
    {
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
            UnitNumber = "A-101",
            PropertyType = PropertyType.Apartment
        };
        var resident = new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            CompoundId = compound.Id,
            FullName = "Resident"
        };
        var cycle = new BillingCycle
        {
            CompoundId = compound.Id,
            Year = 2026,
            Month = 5,
            PeriodStart = new DateOnly(2026, 5, 1),
            PeriodEnd = new DateOnly(2026, 5, 31),
            DueDate = new DateOnly(2026, 6, 1)
        };
        var bill = new UtilityBill
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            BillingCycleId = cycle.Id,
            BillNumber = "UB-OVERDUE",
            IssueDate = new DateOnly(2026, 5, 1),
            DueDate = new DateOnly(2026, 6, 1),
            SubtotalAmount = 100m,
            TotalAmount = 100m,
            BillStatus = BillStatus.Unpaid
        };
        var rentContract = new RentContract
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            ContractNumber = "RC-OVERDUE",
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
            InvoiceNumber = "RI-OVERDUE",
            Year = 2026,
            Month = 5,
            IssueDate = new DateOnly(2026, 5, 1),
            DueDate = new DateOnly(2026, 6, 1),
            RentAmount = 100m,
            TotalAmount = 100m,
            RentInvoiceStatus = RentInvoiceStatus.Unpaid
        };
        var saleContract = new PropertySaleContract
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            SaleType = SaleType.Installment,
            ContractNumber = "SC-OVERDUE",
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
            DueDate = new DateOnly(2026, 6, 1),
            Amount = 100m,
            InstallmentStatus = InstallmentStatus.Pending
        };

        dbContext.AddRange(
            compound,
            unit,
            resident,
            cycle,
            bill,
            rentContract,
            invoice,
            saleContract,
            installment);
        await dbContext.SaveChangesAsync();

        return new FinancialSeed(compound.Id, bill.Id, invoice.Id, installment.Id);
    }

    private sealed record FinancialSeed(
        Guid CompoundId,
        Guid UtilityBillId,
        Guid RentInvoiceId,
        Guid InstallmentId);
}
