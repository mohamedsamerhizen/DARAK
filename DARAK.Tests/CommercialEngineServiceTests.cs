using DARAK.Api.Data;
using DARAK.Api.DTOs.Commercial;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DARAK.Tests;

public sealed class CommercialEngineServiceTests
{
    [Fact]
    public async Task CreateBillingRuleAsync_CreatesRuleAndAudit()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P20-BR1");
        var service = CreateService(dbContext, compound.Id);
        var userId = Guid.NewGuid();

        var result = await service.CreateBillingRuleAsync(userId, new CreateBillingRuleRequest
        {
            CompoundId = compound.Id,
            Name = "Water tiered rule",
            Status = BillingRuleStatus.Active,
            ChargeMode = BillingChargeMode.Tiered,
            MinimumChargeAmount = 5000m,
            LateFeeFlatAmount = 1000m,
            GracePeriodDays = 5,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow)
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        dbContext.BillingRules.Should().ContainSingle(rule => rule.Name == "Water tiered rule");
        dbContext.AuditLogEntries.Should().ContainSingle(entry => entry.ActionType == AuditActionType.BillingRuleCreated);
    }

    [Fact]
    public async Task AddBillingRuleTierAsync_AppendsTierInsideScope()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P20-BT1");
        var rule = new BillingRule
        {
            CompoundId = compound.Id,
            Name = "Electricity tiers",
            ChargeMode = BillingChargeMode.Tiered,
            Status = BillingRuleStatus.Active,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        dbContext.BillingRules.Add(rule);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.AddBillingRuleTierAsync(Guid.NewGuid(), rule.Id, new AddBillingRuleTierRequest
        {
            FromQuantity = 0m,
            ToQuantity = 100m,
            RatePerUnit = 150m,
            SortOrder = 1
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Tiers.Should().ContainSingle(tier => tier.RatePerUnit == 150m);
        dbContext.AuditLogEntries.Should().ContainSingle(entry => entry.ActionType == AuditActionType.BillingRuleTierAdded);
    }

    [Fact]
    public async Task CreateMeterCorrectionAsync_RejectsBilledReading()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedMeterReadingAsync(dbContext, "P20-MC1", isBilled: true);
        var service = CreateService(dbContext, seed.Compound.Id);

        var result = await service.CreateMeterCorrectionAsync(Guid.NewGuid(), new CreateMeterReadingCorrectionRequest
        {
            MeterReadingId = seed.Reading.Id,
            CorrectedPreviousReading = 10m,
            CorrectedCurrentReading = 20m,
            Reason = "Billed correction should use bill adjustment."
        });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        dbContext.MeterReadingCorrections.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveMeterCorrectionAsync_AppliesCorrectionToReadingAndAudits()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedMeterReadingAsync(dbContext, "P20-MC2", isBilled: false);
        var service = CreateService(dbContext, seed.Compound.Id);
        var created = await service.CreateMeterCorrectionAsync(Guid.NewGuid(), new CreateMeterReadingCorrectionRequest
        {
            MeterReadingId = seed.Reading.Id,
            CorrectedPreviousReading = 11m,
            CorrectedCurrentReading = 31m,
            Reason = "Reader typo"
        });

        var result = await service.ApproveMeterCorrectionAsync(Guid.NewGuid(), created.Value!.Id, new DecideMeterReadingCorrectionRequest
        {
            Reason = "Verified photo evidence"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(MeterReadingCorrectionStatus.Applied);
        var reading = await dbContext.MeterReadings.FindAsync(seed.Reading.Id);
        reading!.PreviousReading.Should().Be(11m);
        reading.CurrentReading.Should().Be(31m);
        reading.Consumption.Should().Be(20m);
        dbContext.AuditLogEntries.Should().Contain(entry => entry.ActionType == AuditActionType.MeterReadingCorrectionApproved);
    }

    [Fact]
    public async Task CreateContractLifecycleEventAsync_RecordsRentTimelineEvent()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedRentContractAsync(dbContext, "P20-CL1");
        var service = CreateService(dbContext, seed.Compound.Id);

        var result = await service.CreateContractLifecycleEventAsync(Guid.NewGuid(), new CreateContractLifecycleEventRequest
        {
            CompoundId = seed.Compound.Id,
            ContractType = CommercialContractType.Rent,
            ContractId = seed.RentContract.Id,
            EventType = ContractLifecycleEventType.Renewed,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Reason = "Annual renewal"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.PropertyUnitId.Should().Be(seed.Unit.Id);
        result.Value.ResidentProfileId.Should().Be(seed.Resident.Id);
        dbContext.AuditLogEntries.Should().ContainSingle(entry => entry.ActionType == AuditActionType.ContractLifecycleEventRecorded);
    }

    [Fact]
    public async Task CreateUnitHandoverChecklistAsync_AddsDefaultChecklistItems()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUnitResidentAsync(dbContext, "P20-HO1");
        var service = CreateService(dbContext, seed.Compound.Id);

        var result = await service.CreateUnitHandoverChecklistAsync(Guid.NewGuid(), new CreateUnitHandoverChecklistRequest
        {
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            HandoverType = UnitHandoverType.MoveIn,
            ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Items.Should().HaveCount(3);
        result.Value.Status.Should().Be(UnitHandoverStatus.InProgress);
    }

    [Fact]
    public async Task CompleteUnitHandoverChecklistAsync_MarksPendingItemsAsPassed()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUnitResidentAsync(dbContext, "P20-HO2");
        var checklist = new UnitHandoverChecklist
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            HandoverType = UnitHandoverType.MoveOut,
            Status = UnitHandoverStatus.InProgress,
            ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Items =
            {
                new UnitHandoverChecklistItem { Title = "Keys returned", SortOrder = 1 }
            }
        };
        dbContext.UnitHandoverChecklists.Add(checklist);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, seed.Compound.Id);

        var result = await service.CompleteUnitHandoverChecklistAsync(Guid.NewGuid(), checklist.Id, new CompleteUnitHandoverChecklistRequest
        {
            CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Notes = "Completed cleanly"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(UnitHandoverStatus.Completed);
        result.Value.Items.Single().Status.Should().Be(UnitHandoverItemStatus.Passed);
    }

    [Fact]
    public async Task CreateOwnershipTransferAsync_RequiresSameCompoundResidentsAndUnit()
    {
        await using var dbContext = TestDb.Create();
        var compoundA = await AddCompoundAsync(dbContext, "P20-OT-A");
        var compoundB = await AddCompoundAsync(dbContext, "P20-OT-B");
        var unit = await AddUnitAsync(dbContext, compoundA.Id, "A-1");
        var owner = await AddResidentAsync(dbContext, compoundA.Id, "Owner A");
        var outsider = await AddResidentAsync(dbContext, compoundB.Id, "Outsider B");
        var service = CreateService(dbContext, compoundA.Id);

        var result = await service.CreateOwnershipTransferAsync(Guid.NewGuid(), new CreateOwnershipTransferRequest
        {
            PropertyUnitId = unit.Id,
            CurrentOwnerResidentProfileId = owner.Id,
            NewOwnerResidentProfileId = outsider.Id,
            RequestedTransferDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Reason = "Invalid cross-compound transfer"
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        dbContext.OwnershipTransferRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveOwnershipTransferAsync_CompletesTransferAndRecordsLifecycleEvent()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedInstallmentAsync(dbContext, "P20-OT2");
        var newOwner = await AddResidentAsync(dbContext, seed.Compound.Id, "New Owner");
        var service = CreateService(dbContext, seed.Compound.Id);
        var created = await service.CreateOwnershipTransferAsync(Guid.NewGuid(), new CreateOwnershipTransferRequest
        {
            PropertyUnitId = seed.Unit.Id,
            CurrentOwnerResidentProfileId = seed.Resident.Id,
            NewOwnerResidentProfileId = newOwner.Id,
            RequestedTransferDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Reason = "Legal sale transfer"
        });

        var result = await service.ApproveOwnershipTransferAsync(Guid.NewGuid(), created.Value!.Id, new DecideOwnershipTransferRequest
        {
            Reason = "Documents verified"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(OwnershipTransferStatus.Completed);
        var saleContract = await dbContext.PropertySaleContracts.FindAsync(seed.SaleContract.Id);
        saleContract!.ResidentProfileId.Should().Be(newOwner.Id);
        var installment = await dbContext.InstallmentScheduleItems.FindAsync(seed.Installment.Id);
        installment!.ResidentProfileId.Should().Be(newOwner.Id);
        dbContext.ContractLifecycleEvents.Should().ContainSingle(item =>
            item.EventType == ContractLifecycleEventType.OwnershipTransferred
            && item.ContractId == seed.SaleContract.Id);
    }

    [Fact]
    public async Task ApproveInstallmentRescheduleAsync_AppliesNewDueDateOnly()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedInstallmentAsync(dbContext, "P20-IR1");
        var service = CreateService(dbContext, seed.Compound.Id);
        var newDate = seed.Installment.DueDate.AddDays(15);
        var created = await service.CreateInstallmentRescheduleAsync(Guid.NewGuid(), new CreateInstallmentRescheduleRequest
        {
            InstallmentScheduleItemId = seed.Installment.Id,
            RequestedDueDate = newDate,
            Reason = "Resident-approved date-only payment plan"
        });

        var result = await service.ApproveInstallmentRescheduleAsync(Guid.NewGuid(), created.Value!.Id, new DecideInstallmentRescheduleRequest
        {
            Reason = "Approved by finance"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(InstallmentRescheduleStatus.Applied);
        var installment = await dbContext.InstallmentScheduleItems.FindAsync(seed.Installment.Id);
        installment!.DueDate.Should().Be(newDate);
        installment.Amount.Should().Be(1000m);
    }

    [Fact]
    public async Task ApproveInstallmentRescheduleAsync_RejectsAmountChangeThatBreaksContractTotal()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedInstallmentAsync(dbContext, "P12-IR-AMT");
        var service = CreateService(dbContext, seed.Compound.Id);
        var created = await service.CreateInstallmentRescheduleAsync(Guid.NewGuid(), new CreateInstallmentRescheduleRequest
        {
            InstallmentScheduleItemId = seed.Installment.Id,
            RequestedDueDate = seed.Installment.DueDate.AddDays(15),
            RequestedAmount = 1250m,
            Reason = "Unsafe amount-only change"
        });

        var result = await service.ApproveInstallmentRescheduleAsync(Guid.NewGuid(), created.Value!.Id, new DecideInstallmentRescheduleRequest
        {
            Reason = "Approved by finance"
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        var installment = await dbContext.InstallmentScheduleItems.FindAsync(seed.Installment.Id);
        installment!.Amount.Should().Be(1000m);
        var reschedule = await dbContext.InstallmentRescheduleRequests.FindAsync(created.Value!.Id);
        reschedule!.Status.Should().Be(InstallmentRescheduleStatus.PendingApproval);
    }

    private static CommercialEngineService CreateService(ApplicationDbContext dbContext, params Guid[] allowedCompoundIds)
    {
        var compoundAccess = new FakeCompoundAccessService(allowedCompoundIds);
        return new CommercialEngineService(
            dbContext,
            compoundAccess,
            new AuditLogService(dbContext, compoundAccess, new HttpContextAccessor()));
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = new Compound
        {
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Karrada"
        };
        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<PropertyUnit> AddUnitAsync(ApplicationDbContext dbContext, Guid compoundId, string unitNumber)
    {
        var unit = new PropertyUnit
        {
            CompoundId = compoundId,
            UnitNumber = unitNumber,
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Available,
            AreaSquareMeters = 100m,
            Bedrooms = 2,
            Bathrooms = 1
        };
        dbContext.PropertyUnits.Add(unit);
        await dbContext.SaveChangesAsync();
        return unit;
    }

    private static async Task<ResidentProfile> AddResidentAsync(ApplicationDbContext dbContext, Guid compoundId, string fullName)
    {
        var resident = new ResidentProfile
        {
            CompoundId = compoundId,
            UserId = Guid.NewGuid(),
            FullName = fullName,
            PhoneNumber = "07700000000"
        };
        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }

    private static async Task<(Compound Compound, PropertyUnit Unit, ResidentProfile Resident)> SeedUnitResidentAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = await AddCompoundAsync(dbContext, code);
        var unit = await AddUnitAsync(dbContext, compound.Id, $"U-{code}");
        var resident = await AddResidentAsync(dbContext, compound.Id, $"Resident {code}");
        return (compound, unit, resident);
    }

    private static async Task<(Compound Compound, PropertyUnit Unit, Meter Meter, MeterReading Reading)> SeedMeterReadingAsync(ApplicationDbContext dbContext, string code, bool isBilled)
    {
        var seed = await SeedUnitResidentAsync(dbContext, code);
        var meter = new Meter
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            MeterNumber = $"M-{code}",
            MeterType = MeterType.Electricity,
            IsActive = true
        };
        dbContext.Meters.Add(meter);
        await dbContext.SaveChangesAsync();
        var reading = new MeterReading
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            MeterId = meter.Id,
            Year = 2026,
            Month = 6,
            PreviousReading = 10m,
            CurrentReading = 25m,
            Consumption = 15m,
            RatePerUnit = 100m,
            Amount = 1500m,
            IsBilled = isBilled
        };
        dbContext.MeterReadings.Add(reading);
        await dbContext.SaveChangesAsync();
        return (seed.Compound, seed.Unit, meter, reading);
    }

    private static async Task<(Compound Compound, PropertyUnit Unit, ResidentProfile Resident, RentContract RentContract)> SeedRentContractAsync(ApplicationDbContext dbContext, string code)
    {
        var seed = await SeedUnitResidentAsync(dbContext, code);
        var contract = new RentContract
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            ContractNumber = $"RC-{code}",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            MonthlyRentAmount = 500000m,
            DepositAmount = 100000m
        };
        dbContext.RentContracts.Add(contract);
        await dbContext.SaveChangesAsync();
        return (seed.Compound, seed.Unit, seed.Resident, contract);
    }

    private static async Task<(Compound Compound, PropertyUnit Unit, ResidentProfile Resident, PropertySaleContract SaleContract, InstallmentScheduleItem Installment)> SeedInstallmentAsync(ApplicationDbContext dbContext, string code)
    {
        var seed = await SeedUnitResidentAsync(dbContext, code);
        var contract = new PropertySaleContract
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            ContractNumber = $"SC-{code}",
            SaleType = SaleType.Installment,
            ContractDate = new DateOnly(2026, 1, 1),
            PropertyPrice = 100000000m,
            DownPaymentAmount = 10000000m,
            InstallmentCount = 12,
            FirstInstallmentDueDate = new DateOnly(2026, 2, 1)
        };
        dbContext.PropertySaleContracts.Add(contract);
        await dbContext.SaveChangesAsync();
        var installment = new InstallmentScheduleItem
        {
            CompoundId = seed.Compound.Id,
            PropertySaleContractId = contract.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            InstallmentNumber = 1,
            DueDate = new DateOnly(2026, 2, 1),
            Amount = 1000m,
            InstallmentStatus = InstallmentStatus.Pending
        };
        dbContext.InstallmentScheduleItems.Add(installment);
        await dbContext.SaveChangesAsync();
        return (seed.Compound, seed.Unit, seed.Resident, contract, installment);
    }
}
