using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.ResidentLifecycle;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class ResidentLifecycleServiceTests
{
    [Fact]
    public async Task CreateProcessAsync_CreatesMoveOutPendingFinancialClearance()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-1");
        var service = CreateService(dbContext, seed.Compound.Id);

        var result = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 7, 1),
                FinancialClearanceRequired = true
            });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(ResidentLifecycleStatus.PendingFinancialClearance);
        result.Value.FinancialClearanceRequired.Should().BeTrue();
        dbContext.ResidentLifecycleProcesses.Should().ContainSingle(item => item.Id == result.Value.Id);
    }

    [Fact]
    public async Task CompleteMoveOutAsync_BlocksWhenCustodyIsStillIssued()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-2");
        var service = CreateService(dbContext, seed.Compound.Id);
        var process = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 7, 1),
                FinancialClearanceRequired = true
            });
        await service.ConfirmFinancialClearanceAsync(
            process.Value!.Id,
            Guid.NewGuid(),
            new ConfirmLifecycleFinancialClearanceRequest { Notes = "Cleared" });
        var custody = await service.IssueCustodyItemAsync(
            Guid.NewGuid(),
            new IssueCustodyItemRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ItemType = CustodyItemType.Key,
                Identifier = "A-101-K1"
            });

        var blocked = await service.CompleteProcessAsync(
            process.Value.Id,
            Guid.NewGuid(),
            new CompleteResidentLifecycleProcessRequest());
        await service.ReturnCustodyItemAsync(
            custody.Value!.Id,
            Guid.NewGuid(),
            new ReturnCustodyItemRequest { Notes = "Returned" });
        var completed = await service.CompleteProcessAsync(
            process.Value.Id,
            Guid.NewGuid(),
            new CompleteResidentLifecycleProcessRequest { Notes = "Move-out complete" });

        blocked.Status.Should().Be(ServiceResultStatus.BadRequest);
        completed.IsSuccess.Should().BeTrue(completed.Message);
        completed.Value!.Status.Should().Be(ResidentLifecycleStatus.Completed);
        dbContext.OccupancyRecords.Single(item => item.Id == seed.Occupancy.Id).OccupancyStatus.Should().Be(OccupancyStatus.Ended);
        dbContext.PropertyUnits.Single(item => item.Id == seed.Unit.Id).UnitStatus.Should().Be(UnitStatus.Available);
    }

    [Fact]
    public async Task CompleteMoveInAsync_CreatesActiveOccupancyAndMarksUnitOccupied()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-3", withOccupancy: false, unitStatus: UnitStatus.Available);
        var service = CreateService(dbContext, seed.Compound.Id);
        var process = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveIn,
                TargetDate = new DateOnly(2026, 7, 3),
                FinancialClearanceRequired = false
            });

        var completed = await service.CompleteProcessAsync(
            process.Value!.Id,
            Guid.NewGuid(),
            new CompleteResidentLifecycleProcessRequest { Notes = "Move-in complete" });

        completed.IsSuccess.Should().BeTrue(completed.Message);
        dbContext.OccupancyRecords.Should().ContainSingle(item => item.PropertyUnitId == seed.Unit.Id && item.OccupancyStatus == OccupancyStatus.Active);
        dbContext.PropertyUnits.Single(item => item.Id == seed.Unit.Id).UnitStatus.Should().Be(UnitStatus.Occupied);
    }

    [Fact]
    public async Task MovePermitDecision_ApprovesThenCompletesPermit()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-4");
        var service = CreateService(dbContext, seed.Compound.Id);
        var permit = await service.CreateMovePermitAsync(
            Guid.NewGuid(),
            new CreateMoveLogisticsPermitRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                MoveType = ResidentLifecycleProcessType.MoveOut,
                ScheduledStartAtUtc = DateTime.UtcNow.AddDays(1),
                ScheduledEndAtUtc = DateTime.UtcNow.AddDays(1).AddHours(2),
                TruckInfo = "White truck",
                WorkersCount = 3
            });

        var approved = await service.DecideMovePermitAsync(
            permit.Value!.Id,
            Guid.NewGuid(),
            new DecideMoveLogisticsPermitRequest { Approved = true, Reason = "Allowed" });
        var completed = await service.CompleteMovePermitAsync(
            permit.Value.Id,
            Guid.NewGuid(),
            new CompleteMoveLogisticsPermitRequest { CompletionNotes = "No damage reported" });

        approved.Value!.Status.Should().Be(MoveLogisticsPermitStatus.Approved);
        completed.Value!.Status.Should().Be(MoveLogisticsPermitStatus.Completed);
    }

    [Fact]
    public async Task UnitReadinessAndDamageLiability_AreCompoundScopedOperationalRecords()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-5");
        var service = CreateService(dbContext, seed.Compound.Id);
        var readiness = await service.CreateUnitReadinessRecordAsync(
            Guid.NewGuid(),
            new CreateUnitReadinessRecordRequest
            {
                PropertyUnitId = seed.Unit.Id,
                Status = UnitReadinessStatus.NeedsInspection,
                Notes = "Move-out inspection required"
            });
        var ready = await service.UpdateUnitReadinessStatusAsync(
            readiness.Value!.Id,
            Guid.NewGuid(),
            new UpdateUnitReadinessStatusRequest
            {
                Status = UnitReadinessStatus.ReadyForMoveIn,
                Notes = "Ready"
            });
        var damage = await service.CreateDamageLiabilityAsync(
            Guid.NewGuid(),
            new CreateUnitDamageLiabilityRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                EstimatedAmount = 125000,
                Description = "Broken door handle"
            });

        ready.IsSuccess.Should().BeTrue(ready.Message);
        dbContext.PropertyUnits.Single(item => item.Id == seed.Unit.Id).UnitStatus.Should().Be(UnitStatus.Available);
        damage.IsSuccess.Should().BeTrue(damage.Message);
        damage.Value!.Status.Should().Be(DamageLiabilityStatus.Draft);
        var summary = await service.GetSummaryAsync(new ResidentLifecycleSummaryQuery { CompoundId = seed.Compound.Id });
        summary.Value!.OpenDamageLiabilityCount.Should().Be(1);
    }


    [Fact]
    public async Task GetMoveOutReadinessAsync_ReturnsFinancialAndOperationalBlockers()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-45A");
        var service = CreateService(dbContext, seed.Compound.Id);
        var bill = new UtilityBill
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            BillingCycleId = Guid.NewGuid(),
            BillNumber = "UB-45A",
            BillStatus = BillStatus.Overdue,
            IssueDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 6, 10),
            TotalAmount = 100000,
            PaidAmount = 25000
        };
        dbContext.UtilityBills.Add(bill);
        dbContext.FinancialDisputes.Add(new FinancialDispute
        {
            CompoundId = seed.Compound.Id,
            ResidentProfileId = seed.Resident.Id,
            TargetType = FinancialDisputeTargetType.UtilityBill,
            TargetId = bill.Id,
            Status = FinancialDisputeStatus.UnderReview,
            Reason = "Bill objection",
            ResidentMessage = "Please review",
            CreatedByUserId = Guid.NewGuid()
        });
        dbContext.CollectionCases.Add(new CollectionCase
        {
            CompoundId = seed.Compound.Id,
            ResidentProfileId = seed.Resident.Id,
            Status = CollectionCaseStatus.Open,
            AmountDue = 75000,
            Reason = "Overdue utility bill"
        });
        await dbContext.SaveChangesAsync();

        var result = await service.GetMoveOutReadinessAsync(new MoveOutReadinessQuery
        {
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            AsOfDate = new DateOnly(2026, 6, 20)
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.HasActiveOccupancy.Should().BeTrue();
        result.Value.HasFinancialBlockers.Should().BeTrue();
        result.Value.CanStartMoveOutProcess.Should().BeTrue();
        result.Value.CanConfirmFinancialClearance.Should().BeFalse();
        result.Value.OutstandingAmount.Should().Be(75000);
        result.Value.ActiveFinancialDisputeCount.Should().Be(1);
        result.Value.OpenCollectionCaseCount.Should().Be(1);
        result.Value.FinancialItems.Should().ContainSingle(item => item.SourceType == FinancialLedgerSourceType.UtilityBill && item.HasActiveFinancialDispute);
        result.Value.Blockers.Select(item => item.Code).Should().Contain("OUTSTANDING_FINANCIAL_BALANCE");
    }

    [Fact]
    public async Task ConfirmFinancialClearanceAsync_BlocksMoveOutWithFinancialBlockers()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-FIN-1");
        var service = CreateService(dbContext, seed.Compound.Id);
        var process = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 7, 20),
                FinancialClearanceRequired = true
            });
        await AddMoveOutFinancialBlockersAsync(dbContext, seed);

        var readiness = await service.GetMoveOutReadinessAsync(new MoveOutReadinessQuery
        {
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            AsOfDate = new DateOnly(2026, 7, 1)
        });
        var result = await service.ConfirmFinancialClearanceAsync(
            process.Value!.Id,
            Guid.NewGuid(),
            new ConfirmLifecycleFinancialClearanceRequest { Notes = "Attempted clearance" });

        readiness.IsSuccess.Should().BeTrue(readiness.Message);
        readiness.Value!.FinancialItems.Should().Contain(item => item.SourceType == FinancialLedgerSourceType.PaymentPlanInstallment);
        readiness.Value.Blockers.Select(item => item.Code).Should().Contain("ACTIVE_LEGAL_NOTICES");
        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("outstanding amount");
        result.Message.Should().Contain("active financial disputes");
        result.Message.Should().Contain("active collection cases");
        result.Message.Should().Contain("active legal notices");
        var persisted = dbContext.ResidentLifecycleProcesses.Single(item => item.Id == process.Value.Id);
        persisted.Status.Should().Be(ResidentLifecycleStatus.PendingFinancialClearance);
        persisted.FinancialClearanceConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteProcessAsync_RevalidatesFinancialClearanceBeforeEndingMoveOut()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-FIN-2");
        var service = CreateService(dbContext, seed.Compound.Id);
        var process = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 7, 25),
                FinancialClearanceRequired = true
            });
        var confirmed = await service.ConfirmFinancialClearanceAsync(
            process.Value!.Id,
            Guid.NewGuid(),
            new ConfirmLifecycleFinancialClearanceRequest { Notes = "Clean before new bill" });
        dbContext.UtilityBills.Add(new UtilityBill
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            BillingCycleId = Guid.NewGuid(),
            BillNumber = "UB-FIN-2",
            BillStatus = BillStatus.Unpaid,
            IssueDate = new DateOnly(2026, 7, 1),
            DueDate = new DateOnly(2026, 7, 10),
            TotalAmount = 50_000m,
            PaidAmount = 0m
        });
        await dbContext.SaveChangesAsync();

        var result = await service.CompleteProcessAsync(
            process.Value.Id,
            Guid.NewGuid(),
            new CompleteResidentLifecycleProcessRequest { Notes = "Should block" });

        confirmed.Status.Should().Be(ServiceResultStatus.Success);
        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("Move-out financial clearance is blocked");
        var persisted = dbContext.ResidentLifecycleProcesses.Single(item => item.Id == process.Value.Id);
        persisted.FinancialClearanceConfirmed.Should().BeFalse();
        dbContext.OccupancyRecords.Single(item => item.Id == seed.Occupancy.Id).OccupancyStatus.Should().Be(OccupancyStatus.Active);
        dbContext.PropertyUnits.Single(item => item.Id == seed.Unit.Id).UnitStatus.Should().Be(UnitStatus.Occupied);
    }

    [Fact]
    public async Task CreateProcessAsync_RejectsMoveOutWithoutActiveOccupancy()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-45B", withOccupancy: false, unitStatus: UnitStatus.Available);
        var service = CreateService(dbContext, seed.Compound.Id);

        var result = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 7, 15),
                FinancialClearanceRequired = true
            });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        result.Message.Should().Contain("active occupancy");
    }

    [Fact]
    public async Task GetMoveOutReadinessAsync_ReportsExistingActiveMoveOutProcess()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-45C");
        var service = CreateService(dbContext, seed.Compound.Id);
        var process = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 7, 20),
                FinancialClearanceRequired = true
            });

        var result = await service.GetMoveOutReadinessAsync(new MoveOutReadinessQuery
        {
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            AsOfDate = new DateOnly(2026, 7, 1)
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.CanStartMoveOutProcess.Should().BeFalse();
        result.Value.ActiveMoveOutProcessId.Should().Be(process.Value!.Id);
        result.Value.Blockers.Select(item => item.Code).Should().Contain("ACTIVE_MOVE_OUT_PROCESS");
    }


    [Fact]
    public async Task RecordMoveOutFinalMeterReadingsAsync_CreatesUnbilledFinalReadingsAndSettlementSummary()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-46A");
        var service = CreateService(dbContext, seed.Compound.Id);
        var meter = new Meter
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            MeterType = MeterType.Electricity,
            MeterNumber = "E-46A",
            RatePerUnit = 2,
            IsActive = true
        };
        dbContext.Meters.Add(meter);
        dbContext.MeterReadings.Add(new MeterReading
        {
            CompoundId = seed.Compound.Id,
            MeterId = meter.Id,
            PropertyUnitId = seed.Unit.Id,
            Year = 2026,
            Month = 6,
            PreviousReading = 0,
            CurrentReading = 100,
            Consumption = 100,
            RatePerUnit = 2,
            Amount = 200,
            ReadingDate = new DateTime(2026, 6, 1)
        });
        await dbContext.SaveChangesAsync();
        var process = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 7, 30),
                FinancialClearanceRequired = false
            });

        var recorded = await service.RecordMoveOutFinalMeterReadingsAsync(
            process.Value!.Id,
            Guid.NewGuid(),
            new RecordMoveOutFinalMeterReadingsRequest
            {
                Readings =
                [
                    new RecordMoveOutFinalMeterReadingRequest
                    {
                        MeterId = meter.Id,
                        CurrentReading = 150,
                        ReadingDateUtc = new DateTime(2026, 7, 30),
                        Notes = "Resident exit"
                    }
                ]
            });
        var settlement = await service.GetMoveOutOperationalSettlementAsync(process.Value.Id);

        recorded.IsSuccess.Should().BeTrue(recorded.Message);
        recorded.Value!.Single().PreviousReading.Should().Be(100);
        recorded.Value!.Single().Consumption.Should().Be(50);
        recorded.Value!.Single().Amount.Should().Be(100);
        settlement.IsSuccess.Should().BeTrue(settlement.Message);
        settlement.Value!.ActiveMeterCount.Should().Be(1);
        settlement.Value.FinalMeterReadingCount.Should().Be(1);
        settlement.Value.MissingFinalMeterReadingCount.Should().Be(0);
        settlement.Value.UnbilledFinalMeterReadingCount.Should().Be(1);
        settlement.Value.CanCompleteOperationalSettlement.Should().BeFalse();
        settlement.Value.Blockers.Select(item => item.Code).Should().Contain("FINAL_METER_READING_UNBILLED");
    }

    [Fact]
    public async Task CompleteMoveOutAsync_BlocksWhenActiveMeterHasNoFinalReading()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-46B");
        var service = CreateService(dbContext, seed.Compound.Id);
        dbContext.Meters.Add(new Meter
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            MeterType = MeterType.Water,
            MeterNumber = "W-46B",
            RatePerUnit = 1,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();
        var process = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 8, 1),
                FinancialClearanceRequired = false
            });

        var result = await service.CompleteProcessAsync(
            process.Value!.Id,
            Guid.NewGuid(),
            new CompleteResidentLifecycleProcessRequest());

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("final move-out readings");
    }

    [Fact]
    public async Task CustodyAndDamageSettlement_UpdatesOperationalBlockers()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-47A");
        var service = CreateService(dbContext, seed.Compound.Id);
        var process = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 8, 5),
                FinancialClearanceRequired = false
            });
        var custody = await service.IssueCustodyItemAsync(
            Guid.NewGuid(),
            new IssueCustodyItemRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ItemType = CustodyItemType.AccessCard,
                Identifier = "CARD-47A",
                ReplacementFeeAmount = 25000
            });
        var damage = await service.CreateDamageLiabilityAsync(
            Guid.NewGuid(),
            new CreateUnitDamageLiabilityRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ResidentLifecycleProcessId = process.Value!.Id,
                EstimatedAmount = 90000,
                Description = "Wall repaint required"
            });

        var before = await service.GetMoveOutOperationalSettlementAsync(process.Value.Id);
        var returned = await service.UpdateCustodyItemSettlementStatusAsync(
            custody.Value!.Id,
            Guid.NewGuid(),
            new UpdateCustodySettlementStatusRequest
            {
                Status = CustodyItemStatus.Returned,
                Notes = "Replacement card received"
            });
        var resolved = await service.UpdateDamageLiabilityStatusAsync(
            damage.Value!.Id,
            Guid.NewGuid(),
            new UpdateDamageLiabilityStatusRequest
            {
                Status = DamageLiabilityStatus.Resolved,
                Notes = "Settled with resident"
            });
        var after = await service.GetMoveOutOperationalSettlementAsync(process.Value.Id);

        before.Value!.IssuedCustodyItemCount.Should().Be(1);
        before.Value.OpenDamageLiabilityCount.Should().Be(1);
        returned.Value!.Status.Should().Be(CustodyItemStatus.Returned);
        resolved.Value!.Status.Should().Be(DamageLiabilityStatus.Resolved);
        after.Value!.IssuedCustodyItemCount.Should().Be(0);
        after.Value.OpenDamageLiabilityCount.Should().Be(0);
        after.Value.CanCompleteOperationalSettlement.Should().BeTrue();
    }

    [Fact]
    public async Task GetMoveOutExitCertificateAsync_BecomesEligibleAfterMoveOutCompletion()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-48A");
        var service = CreateService(dbContext, seed.Compound.Id);
        var process = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 8, 10),
                FinancialClearanceRequired = false
            });

        var before = await service.GetMoveOutExitCertificateAsync(process.Value!.Id);
        var completed = await service.CompleteProcessAsync(
            process.Value.Id,
            Guid.NewGuid(),
            new CompleteResidentLifecycleProcessRequest { Notes = "Move-out completed" });
        var after = await service.GetMoveOutExitCertificateAsync(process.Value.Id);

        before.IsSuccess.Should().BeTrue(before.Message);
        before.Value!.ExitCertificateEligible.Should().BeFalse();
        before.Value.Blockers.Select(item => item.Code).Should().Contain("MOVE_OUT_NOT_COMPLETED");
        completed.IsSuccess.Should().BeTrue(completed.Message);
        after.IsSuccess.Should().BeTrue(after.Message);
        after.Value!.ExitCertificateEligible.Should().BeTrue();
        after.Value.CertificateNumber.Should().StartWith("DARAK-EXIT-");
        after.Value.Blockers.Should().BeEmpty();
    }

    [Fact]
    public async Task PrepareMoveOutUnitTurnoverAsync_BlocksBeforeCompletionThenCreatesReadiness()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-49A");
        var service = CreateService(dbContext, seed.Compound.Id);
        var process = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 8, 12),
                FinancialClearanceRequired = false
            });

        var blocked = await service.PrepareMoveOutUnitTurnoverAsync(
            process.Value!.Id,
            Guid.NewGuid(),
            new PrepareMoveOutUnitTurnoverRequest { InitialStatus = UnitReadinessStatus.CleaningRequired });
        await service.CompleteProcessAsync(
            process.Value.Id,
            Guid.NewGuid(),
            new CompleteResidentLifecycleProcessRequest { Notes = "Move-out complete" });
        var prepared = await service.PrepareMoveOutUnitTurnoverAsync(
            process.Value.Id,
            Guid.NewGuid(),
            new PrepareMoveOutUnitTurnoverRequest
            {
                InitialStatus = UnitReadinessStatus.CleaningRequired,
                Notes = "Post move-out cleaning"
            });

        blocked.Status.Should().Be(ServiceResultStatus.Conflict);
        prepared.IsSuccess.Should().BeTrue(prepared.Message);
        prepared.Value!.Status.Should().Be(UnitReadinessStatus.CleaningRequired);
        dbContext.UnitReadinessRecords.Should().ContainSingle(item => item.ResidentLifecycleProcessId == process.Value.Id);
        dbContext.PropertyUnits.Single(item => item.Id == seed.Unit.Id).UnitStatus.Should().Be(UnitStatus.UnderMaintenance);
    }

    [Fact]
    public async Task GetMoveOutUnitTurnoverTimelineAsync_TracksReadinessUntilReadyForNextResident()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedAsync(dbContext, "LC-49B");
        var service = CreateService(dbContext, seed.Compound.Id);
        var process = await service.CreateProcessAsync(
            Guid.NewGuid(),
            new CreateResidentLifecycleProcessRequest
            {
                PropertyUnitId = seed.Unit.Id,
                ResidentProfileId = seed.Resident.Id,
                ProcessType = ResidentLifecycleProcessType.MoveOut,
                TargetDate = new DateOnly(2026, 8, 15),
                FinancialClearanceRequired = false
            });
        await service.CompleteProcessAsync(
            process.Value!.Id,
            Guid.NewGuid(),
            new CompleteResidentLifecycleProcessRequest { Notes = "Move-out complete" });
        var readiness = await service.PrepareMoveOutUnitTurnoverAsync(
            process.Value.Id,
            Guid.NewGuid(),
            new PrepareMoveOutUnitTurnoverRequest { InitialStatus = UnitReadinessStatus.NeedsMaintenance });

        var beforeReady = await service.GetMoveOutUnitTurnoverTimelineAsync(process.Value.Id);
        await service.UpdateUnitReadinessStatusAsync(
            readiness.Value!.Id,
            Guid.NewGuid(),
            new UpdateUnitReadinessStatusRequest
            {
                Status = UnitReadinessStatus.ReadyForMoveIn,
                Notes = "Ready for next resident"
            });
        var afterReady = await service.GetMoveOutUnitTurnoverTimelineAsync(process.Value.Id);

        beforeReady.IsSuccess.Should().BeTrue(beforeReady.Message);
        beforeReady.Value!.ReadyForNextResident.Should().BeFalse();
        beforeReady.Value.LatestReadinessStatus.Should().Be(UnitReadinessStatus.NeedsMaintenance);
        beforeReady.Value.Items.Select(item => item.Step).Should().Contain("UNIT_TURNOVER_READINESS");
        afterReady.IsSuccess.Should().BeTrue(afterReady.Message);
        afterReady.Value!.ReadyForNextResident.Should().BeTrue();
        afterReady.Value.LatestReadinessStatus.Should().Be(UnitReadinessStatus.ReadyForMoveIn);
        afterReady.Value.CurrentUnitStatus.Should().Be(UnitStatus.Available);
    }

    private static ResidentLifecycleService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new ResidentLifecycleService(dbContext, new FakeCompoundAccessService([compoundId]));
    }

    private static async Task<SeedData> SeedAsync(
        ApplicationDbContext dbContext,
        string code,
        bool withOccupancy = true,
        UnitStatus unitStatus = UnitStatus.Occupied)
    {
        var compound = new Compound
        {
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Karrada",
            Address = "Baghdad"
        };
        var userId = Guid.NewGuid();
        var resident = new ResidentProfile
        {
            CompoundId = compound.Id,
            UserId = userId,
            FullName = $"Resident {code}",
            PhoneNumber = "07700000000"
        };
        var unit = new PropertyUnit
        {
            CompoundId = compound.Id,
            UnitNumber = $"U-{code}",
            PropertyType = PropertyType.Apartment,
            UnitStatus = unitStatus,
            AreaSquareMeters = 100,
            Bedrooms = 2,
            Bathrooms = 1,
            IsActive = true
        };

        dbContext.Compounds.Add(compound);
        dbContext.ResidentProfiles.Add(resident);
        dbContext.PropertyUnits.Add(unit);
        OccupancyRecord? occupancy = null;
        if (withOccupancy)
        {
            occupancy = new OccupancyRecord
            {
                CompoundId = compound.Id,
                PropertyUnitId = unit.Id,
                ResidentProfileId = resident.Id,
                OccupancyType = OccupancyType.Tenant,
                OccupancyStatus = OccupancyStatus.Active,
                StartDate = new DateOnly(2026, 1, 1)
            };
            dbContext.OccupancyRecords.Add(occupancy);
        }

        await dbContext.SaveChangesAsync();
        return new SeedData(compound, resident, unit, occupancy ?? new OccupancyRecord());
    }

    private static async Task AddMoveOutFinancialBlockersAsync(ApplicationDbContext dbContext, SeedData seed)
    {
        var collectionCase = new CollectionCase
        {
            CompoundId = seed.Compound.Id,
            ResidentProfileId = seed.Resident.Id,
            Status = CollectionCaseStatus.Open,
            AmountDue = 75_000m,
            Reason = "Open move-out collection case"
        };
        var paymentPlan = new PaymentPlan
        {
            CompoundId = seed.Compound.Id,
            ResidentProfileId = seed.Resident.Id,
            CollectionCaseId = collectionCase.Id,
            Status = PaymentPlanStatus.Active,
            TotalAmount = 90_000m,
            InstallmentCount = 1,
            StartDate = new DateOnly(2026, 7, 1)
        };

        dbContext.UtilityBills.Add(new UtilityBill
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            BillingCycleId = Guid.NewGuid(),
            BillNumber = "UB-FIN-1",
            BillStatus = BillStatus.Overdue,
            IssueDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 6, 10),
            TotalAmount = 100_000m,
            PaidAmount = 25_000m
        });
        dbContext.RentInvoices.Add(new RentInvoice
        {
            RentContractId = Guid.NewGuid(),
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            InvoiceNumber = "RI-FIN-1",
            Year = 2026,
            Month = 6,
            IssueDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 6, 30),
            RentAmount = 200_000m,
            TotalAmount = 200_000m,
            PaidAmount = 50_000m,
            RentInvoiceStatus = RentInvoiceStatus.PartiallyPaid
        });
        dbContext.InstallmentScheduleItems.Add(new InstallmentScheduleItem
        {
            PropertySaleContractId = Guid.NewGuid(),
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            InstallmentNumber = 1,
            DueDate = new DateOnly(2026, 7, 1),
            Amount = 125_000m,
            PaidAmount = 25_000m,
            InstallmentStatus = InstallmentStatus.Pending
        });
        dbContext.CollectionCases.Add(collectionCase);
        dbContext.PaymentPlans.Add(paymentPlan);
        dbContext.PaymentPlanInstallments.Add(new PaymentPlanInstallment
        {
            PaymentPlanId = paymentPlan.Id,
            PaymentPlan = paymentPlan,
            InstallmentNumber = 1,
            DueDate = new DateOnly(2026, 7, 15),
            Amount = 90_000m,
            PaidAmount = 0m,
            Status = PaymentPlanInstallmentStatus.Pending
        });
        dbContext.FinancialDisputes.Add(new FinancialDispute
        {
            CompoundId = seed.Compound.Id,
            ResidentProfileId = seed.Resident.Id,
            TargetType = FinancialDisputeTargetType.UtilityBill,
            TargetId = Guid.NewGuid(),
            Status = FinancialDisputeStatus.UnderReview,
            Reason = "Move-out financial dispute",
            ResidentMessage = "Please review before move-out.",
            CreatedByUserId = Guid.NewGuid()
        });
        dbContext.LegalNotices.Add(new LegalNotice
        {
            CompoundId = seed.Compound.Id,
            ResidentProfileId = seed.Resident.Id,
            CollectionCaseId = collectionCase.Id,
            NoticeType = LegalNoticeType.FinalPaymentNotice,
            Status = LegalNoticeStatus.Issued,
            Title = "Final payment notice",
            Body = "Settle before move-out."
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed record SeedData(Compound Compound, ResidentProfile Resident, PropertyUnit Unit, OccupancyRecord Occupancy);
}
