using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.PropertySales;
using DARAK.Api.DTOs.Rents;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class PropertyContractsWorkflowTests
{
    [Fact]
    public async Task CreateRentContractAsync_SetsUnitRentedAndCreatesActiveContract()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedContractFoundationAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.CreateRentContractAsync(new CreateRentContractRequest
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            ResidentProfileId = seed.ResidentProfileId,
            ContractNumber = "RENT-001",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            MonthlyRentAmount = 500m,
            DepositAmount = 250m
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        var unit = await dbContext.PropertyUnits.SingleAsync(item => item.Id == seed.UnitId);
        var contract = await dbContext.RentContracts.SingleAsync();
        unit.UnitStatus.Should().Be(UnitStatus.Rented);
        contract.ContractStatus.Should().Be(RentContractStatus.Active);
        contract.MonthlyRentAmount.Should().Be(500m);
    }

    [Fact]
    public async Task CreateRentContractAsync_RejectsSecondActiveContractForSameUnit()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedContractFoundationAsync(dbContext);
        dbContext.RentContracts.Add(new RentContract
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            ResidentProfileId = seed.ResidentProfileId,
            ContractNumber = "RENT-EXISTING",
            ContractStatus = RentContractStatus.Active,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            MonthlyRentAmount = 400m
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.CreateRentContractAsync(new CreateRentContractRequest
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            ResidentProfileId = seed.ResidentProfileId,
            ContractNumber = "RENT-NEW",
            StartDate = new DateOnly(2026, 2, 1),
            EndDate = new DateOnly(2026, 12, 31),
            MonthlyRentAmount = 500m,
            DepositAmount = 0m
        });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
    }

    [Fact]
    public async Task GenerateRentInvoiceAsync_RejectsDuplicateMonthForContract()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedContractFoundationAsync(dbContext);
        var contract = SeedActiveRentContract(seed);
        dbContext.RentContracts.Add(contract);
        dbContext.RentInvoices.Add(new RentInvoice
        {
            RentContractId = contract.Id,
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            ResidentProfileId = seed.ResidentProfileId,
            InvoiceNumber = "RINV-EXISTING",
            Year = 2026,
            Month = 3,
            IssueDate = new DateOnly(2026, 3, 1),
            DueDate = new DateOnly(2026, 3, 15),
            RentAmount = 500m,
            TotalAmount = 500m
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.GenerateRentInvoiceAsync(new GenerateRentInvoiceRequest
        {
            RentContractId = contract.Id,
            Year = 2026,
            Month = 3,
            IssueDate = new DateOnly(2026, 3, 1),
            DueDate = new DateOnly(2026, 3, 15)
        });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
    }

    [Fact]
    public async Task CreateInstallmentSaleContractAsync_CreatesBalancedInstallmentSchedule()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedContractFoundationAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.CreateInstallmentSaleContractAsync(new CreateInstallmentSaleContractRequest
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            ResidentProfileId = seed.ResidentProfileId,
            ContractNumber = "SALE-001",
            ContractDate = new DateOnly(2026, 1, 1),
            PropertyPrice = 1000m,
            DownPaymentAmount = 100m,
            InstallmentCount = 3,
            FirstInstallmentDueDate = new DateOnly(2026, 2, 1)
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        var installments = await dbContext.InstallmentScheduleItems
            .OrderBy(item => item.InstallmentNumber)
            .ToArrayAsync();
        installments.Should().HaveCount(3);
        installments.Sum(item => item.Amount).Should().Be(900m);
        installments[0].DueDate.Should().Be(new DateOnly(2026, 2, 1));
        installments[1].DueDate.Should().Be(new DateOnly(2026, 3, 1));
        installments[2].DueDate.Should().Be(new DateOnly(2026, 4, 1));
        (await dbContext.PropertyUnits.SingleAsync(item => item.Id == seed.UnitId)).UnitStatus
            .Should().Be(UnitStatus.SoldInstallment);
    }

    [Fact]
    public async Task CreateCashSaleContractAsync_RecordsPaymentReceiptAndLedgerEntries()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedContractFoundationAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.CreateCashSaleContractAsync(new CreateCashSaleContractRequest
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            ResidentProfileId = seed.ResidentProfileId,
            ContractNumber = "SALE-CASH-001",
            ContractDate = new DateOnly(2026, 1, 1),
            PropertyPrice = 1000m,
            IdempotencyKey = "cash-sale-key-1"
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        var contract = await dbContext.PropertySaleContracts.SingleAsync();
        var payment = await dbContext.Payments.SingleAsync();
        payment.TargetType.Should().Be(PaymentTargetType.PropertySaleContract);
        payment.TargetId.Should().Be(contract.Id);
        payment.Amount.Should().Be(1000m);
        payment.IdempotencyKey.Should().Be("cash-sale-key-1");
        dbContext.Receipts.Should().ContainSingle(receipt => receipt.PaymentId == payment.Id);
        dbContext.ResidentLedgerEntries.Should().Contain(entry =>
            entry.Direction == FinancialLedgerEntryDirection.Debit
            && entry.SourceType == FinancialLedgerSourceType.PropertySaleContract
            && entry.SourceId == contract.Id
            && entry.Amount == 1000m);
        dbContext.ResidentLedgerEntries.Should().Contain(entry =>
            entry.Direction == FinancialLedgerEntryDirection.Credit
            && entry.SourceType == FinancialLedgerSourceType.Payment
            && entry.SourceId == payment.Id
            && entry.Amount == 1000m);
    }

    [Fact]
    public async Task CreateInstallmentSaleContractAsync_RecordsDownPaymentAndRejectsDuplicateIdempotencyKey()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedContractFoundationAsync(dbContext);
        var secondUnit = await AddAvailableUnitAsync(dbContext, seed.CompoundId, "C-102");
        var service = CreateService(dbContext);

        var first = await service.CreateInstallmentSaleContractAsync(new CreateInstallmentSaleContractRequest
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            ResidentProfileId = seed.ResidentProfileId,
            ContractNumber = "SALE-DP-001",
            ContractDate = new DateOnly(2026, 1, 1),
            PropertyPrice = 1000m,
            DownPaymentAmount = 100m,
            DownPaymentIdempotencyKey = "down-payment-key-1",
            InstallmentCount = 3,
            FirstInstallmentDueDate = new DateOnly(2026, 2, 1)
        });
        var duplicateKey = await service.CreateInstallmentSaleContractAsync(new CreateInstallmentSaleContractRequest
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = secondUnit.Id,
            ResidentProfileId = seed.ResidentProfileId,
            ContractNumber = "SALE-DP-002",
            ContractDate = new DateOnly(2026, 1, 1),
            PropertyPrice = 1000m,
            DownPaymentAmount = 100m,
            DownPaymentIdempotencyKey = "down-payment-key-1",
            InstallmentCount = 3,
            FirstInstallmentDueDate = new DateOnly(2026, 2, 1)
        });

        first.Status.Should().Be(ServiceResultStatus.Success);
        duplicateKey.Status.Should().Be(ServiceResultStatus.Conflict);
        var payment = await dbContext.Payments.SingleAsync();
        payment.TargetType.Should().Be(PaymentTargetType.PropertySaleContract);
        payment.Amount.Should().Be(100m);
        payment.IdempotencyKey.Should().Be("down-payment-key-1");
        dbContext.Receipts.Should().ContainSingle(receipt => receipt.PaymentId == payment.Id);
        dbContext.ResidentLedgerEntries.Should().ContainSingle(entry =>
            entry.Direction == FinancialLedgerEntryDirection.Credit
            && entry.SourceType == FinancialLedgerSourceType.Payment
            && entry.SourceId == payment.Id
            && entry.Amount == 100m);
        dbContext.ResidentLedgerEntries.Should().ContainSingle(entry =>
            entry.Direction == FinancialLedgerEntryDirection.Debit
            && entry.SourceType == FinancialLedgerSourceType.PropertySaleContract
            && entry.Amount == 100m);
    }

    [Fact]
    public async Task CreateInstallmentSaleContractAsync_RejectsFirstInstallmentDueDateBeforeContractDate()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedContractFoundationAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.CreateInstallmentSaleContractAsync(new CreateInstallmentSaleContractRequest
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            ResidentProfileId = seed.ResidentProfileId,
            ContractNumber = "SALE-DATE-INVALID",
            ContractDate = new DateOnly(2026, 3, 1),
            PropertyPrice = 1000m,
            DownPaymentAmount = 100m,
            InstallmentCount = 3,
            FirstInstallmentDueDate = new DateOnly(2026, 2, 28)
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        dbContext.PropertySaleContracts.Should().BeEmpty();
        dbContext.InstallmentScheduleItems.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateMonthlyRentInvoicesAsync_RejectsDueDateBeforeIssueDateAndCreatesNoInvoices()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedContractFoundationAsync(dbContext);
        dbContext.RentContracts.Add(SeedActiveRentContract(seed));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.GenerateMonthlyRentInvoicesAsync(new GenerateMonthlyRentInvoicesRequest
        {
            CompoundId = seed.CompoundId,
            Year = 2026,
            Month = 3,
            IssueDate = new DateOnly(2026, 3, 10),
            DueDate = new DateOnly(2026, 3, 1)
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        dbContext.RentInvoices.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateInstallmentSaleContractAsync_RejectsDownPaymentEqualToPrice()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedContractFoundationAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.CreateInstallmentSaleContractAsync(new CreateInstallmentSaleContractRequest
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            ResidentProfileId = seed.ResidentProfileId,
            ContractNumber = "SALE-INVALID",
            ContractDate = new DateOnly(2026, 1, 1),
            PropertyPrice = 1000m,
            DownPaymentAmount = 1000m,
            InstallmentCount = 3,
            FirstInstallmentDueDate = new DateOnly(2026, 2, 1)
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    private static PropertyContractsService CreateService(DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        return new PropertyContractsService(
            new PropertySaleService(dbContext),
            new RentContractService(dbContext),
            new RentInvoiceService(dbContext));
    }

    private static async Task<ContractSeed> SeedContractFoundationAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        var compound = new Compound
        {
            Name = "Contract Compound",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Test"
        };
        var unit = new PropertyUnit
        {
            CompoundId = compound.Id,
            UnitNumber = "C-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Available,
            IsActive = true
        };
        var resident = new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            CompoundId = compound.Id,
            FullName = "Contract Resident",
            IsActive = true
        };

        dbContext.AddRange(compound, unit, resident);
        await dbContext.SaveChangesAsync();

        return new ContractSeed(compound.Id, unit.Id, resident.Id);
    }

    private static async Task<PropertyUnit> AddAvailableUnitAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        Guid compoundId,
        string unitNumber)
    {
        var unit = new PropertyUnit
        {
            CompoundId = compoundId,
            UnitNumber = unitNumber,
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Available,
            IsActive = true
        };

        dbContext.PropertyUnits.Add(unit);
        await dbContext.SaveChangesAsync();
        return unit;
    }

    private static RentContract SeedActiveRentContract(ContractSeed seed)
    {
        return new RentContract
        {
            CompoundId = seed.CompoundId,
            PropertyUnitId = seed.UnitId,
            ResidentProfileId = seed.ResidentProfileId,
            ContractNumber = "RENT-SEED",
            ContractStatus = RentContractStatus.Active,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            MonthlyRentAmount = 500m,
            DepositAmount = 0m
        };
    }

    private sealed record ContractSeed(Guid CompoundId, Guid UnitId, Guid ResidentProfileId);
}
