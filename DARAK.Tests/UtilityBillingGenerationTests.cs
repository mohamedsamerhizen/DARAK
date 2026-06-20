using DARAK.Api.DTOs.BillingCycles;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.CompoundServices;
using DARAK.Api.DTOs.UtilityBills;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class UtilityBillingGenerationTests
{
    [Fact]
    public async Task GenerateUtilityBillAsync_AttachesActiveResidentProfileAndCalculatesTotals()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedBillingFoundationAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.GenerateUtilityBillAsync(new GenerateUtilityBillRequest
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            BillingCycleId = seed.CycleId,
            LateFeeAmount = 5m,
            DiscountAmount = 3m,
            Lines =
            [
                new AddUtilityBillLineRequest
                {
                    CompoundServiceId = seed.ServiceId,
                    Quantity = 2m,
                    UnitPrice = 12.50m
                }
            ]
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        var bill = await dbContext.UtilityBills.Include(item => item.Lines).SingleAsync();
        bill.ResidentProfileId.Should().Be(seed.ResidentProfileId);
        bill.SubtotalAmount.Should().Be(25m);
        bill.LateFeeAmount.Should().Be(5m);
        bill.DiscountAmount.Should().Be(3m);
        bill.TotalAmount.Should().Be(27m);
        bill.PaidAmount.Should().Be(0m);
        bill.BillStatus.Should().Be(BillStatus.Overdue);
        bill.Lines.Should().ContainSingle();
    }

    [Fact]
    public async Task GenerateUtilityBillAsync_RejectsDuplicateUnitCycleBill()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedBillingFoundationAsync(dbContext);
        dbContext.UtilityBills.Add(new UtilityBill
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            ResidentProfileId = seed.ResidentProfileId,
            BillingCycleId = seed.CycleId,
            BillNumber = "UB-DUPLICATE",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            SubtotalAmount = 10m,
            TotalAmount = 10m
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.GenerateUtilityBillAsync(new GenerateUtilityBillRequest
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            BillingCycleId = seed.CycleId,
            Lines = [new AddUtilityBillLineRequest { CompoundServiceId = seed.ServiceId, Quantity = 1m }]
        });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
    }

    [Fact]
    public async Task GenerateUtilityBillAsync_RejectsLineServiceFromAnotherCompound()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedBillingFoundationAsync(dbContext);
        var otherCompound = CreateCompound("Other");
        var otherService = new CompoundService
        {
            CompoundId = otherCompound.Id,
            Name = "Other Internet",
            ServiceType = UtilityServiceType.Internet,
            DefaultMonthlyFee = 50m,
            IsActive = true
        };
        dbContext.AddRange(otherCompound, otherService);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.GenerateUtilityBillAsync(new GenerateUtilityBillRequest
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            BillingCycleId = seed.CycleId,
            Lines = [new AddUtilityBillLineRequest { CompoundServiceId = otherService.Id, Quantity = 1m }]
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task GenerateMonthlyUtilityBillsAsync_SkipsUnitsThatAlreadyHaveBillForCycle()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedBillingFoundationAsync(dbContext);
        dbContext.UtilityBills.Add(new UtilityBill
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            ResidentProfileId = seed.ResidentProfileId,
            BillingCycleId = seed.CycleId,
            BillNumber = "UB-EXISTING",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            SubtotalAmount = 10m,
            TotalAmount = 10m
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.GenerateMonthlyUtilityBillsAsync(new GenerateMonthlyUtilityBillsRequest
        {
            CompoundId = seed.CompoundId,
            BillingCycleId = seed.CycleId,
            IncludeOnlyOccupiedUnits = true,
            IncludePreviousBalance = true
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.CreatedCount.Should().Be(0);
        result.Value.SkippedCount.Should().Be(1);
        result.Value.SkippedReasons.Single().Should().Contain("already has a utility bill");
    }

    [Fact]
    public async Task GenerateUtilityBillAsync_UsesTotalPriorOpenBillRemainingBalanceWhenNotProvided()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedBillingFoundationAsync(dbContext, year: 2026, month: 3);
        var january = new BillingCycle
        {
            CompoundId = seed.CompoundId,
            Year = 2026,
            Month = 1,
            PeriodStart = new DateOnly(2026, 1, 1),
            PeriodEnd = new DateOnly(2026, 1, 31),
            DueDate = new DateOnly(2026, 2, 10)
        };
        var february = new BillingCycle
        {
            CompoundId = seed.CompoundId,
            Year = 2026,
            Month = 2,
            PeriodStart = new DateOnly(2026, 2, 1),
            PeriodEnd = new DateOnly(2026, 2, 28),
            DueDate = new DateOnly(2026, 3, 10)
        };
        dbContext.BillingCycles.AddRange(january, february);
        dbContext.UtilityBills.AddRange(
            new UtilityBill
            {
                CompoundId = seed.CompoundId,
                PropertyUnitId = seed.UnitId,
                ResidentProfileId = seed.ResidentProfileId,
                BillingCycleId = january.Id,
                BillNumber = "UB-JAN",
                IssueDate = new DateOnly(2026, 1, 1),
                DueDate = new DateOnly(2026, 2, 10),
                SubtotalAmount = 100m,
                TotalAmount = 100m,
                PaidAmount = 20m
            },
            new UtilityBill
            {
                CompoundId = seed.CompoundId,
                PropertyUnitId = seed.UnitId,
                ResidentProfileId = seed.ResidentProfileId,
                BillingCycleId = february.Id,
                BillNumber = "UB-FEB",
                IssueDate = new DateOnly(2026, 2, 1),
                DueDate = new DateOnly(2026, 3, 10),
                SubtotalAmount = 120m,
                TotalAmount = 120m,
                PaidAmount = 50m
            });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.GenerateUtilityBillAsync(new GenerateUtilityBillRequest
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            BillingCycleId = seed.CycleId,
            Lines = [new AddUtilityBillLineRequest { CompoundServiceId = seed.ServiceId, Quantity = 1m, UnitPrice = 30m }]
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        var marchBill = await dbContext.UtilityBills.SingleAsync(bill => bill.BillingCycleId == seed.CycleId);
        marchBill.PreviousBalanceAmount.Should().Be(150m);
        marchBill.TotalAmount.Should().Be(180m);
    }

    private static UtilityBillingService CreateService(DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        return new UtilityBillingService(
            new CompoundServiceCatalogService(dbContext),
            new BillingCycleService(dbContext),
            new UtilityBillService(dbContext));
    }

    private static async Task<BillingSeed> SeedBillingFoundationAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        int year = 2026,
        int month = 5)
    {
        var compound = CreateCompound("Billing Compound");
        var unit = new PropertyUnit
        {
            CompoundId = compound.Id,
            UnitNumber = "A-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Rented,
            IsActive = true
        };
        var resident = new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            CompoundId = compound.Id,
            FullName = "Billing Resident",
            IsActive = true
        };
        var occupancy = new OccupancyRecord
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            ResidentProfile = resident,
            OccupancyType = OccupancyType.Tenant,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = new DateOnly(2026, 1, 1)
        };
        var cycle = new BillingCycle
        {
            CompoundId = compound.Id,
            Year = year,
            Month = month,
            PeriodStart = new DateOnly(year, month, 1),
            PeriodEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
            DueDate = new DateOnly(year, month, 20)
        };
        var service = new CompoundService
        {
            CompoundId = compound.Id,
            Name = "Security",
            ServiceType = UtilityServiceType.Security,
            DefaultMonthlyFee = 20m,
            IsActive = true,
            IsMeterBased = false
        };

        dbContext.AddRange(compound, unit, resident, occupancy, cycle, service);
        await dbContext.SaveChangesAsync();

        return new BillingSeed(compound.Id, unit.Id, resident.Id, cycle.Id, service.Id);
    }

    private static Compound CreateCompound(string name)
    {
        return new Compound
        {
            Name = name,
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Test"
        };
    }

    private sealed record BillingSeed(
        Guid CompoundId,
        Guid UnitId,
        Guid ResidentProfileId,
        Guid CycleId,
        Guid ServiceId);
}

