using DARAK.Api.Data;
using DARAK.Api.DTOs.System;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class CommercialProductizationServiceTests
{
    [Fact]
    public async Task GetFinalDeliveryScorecardAsync_ReturnsCommercialClosureSignals()
    {
        await using var dbContext = TestDb.Create();
        var compound = await SeedCommercialDeliveryScenarioAsync(dbContext);
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetFinalDeliveryScorecardAsync(new FinalDeliveryQuery
        {
            CompoundId = compound.Id,
            Days = 30
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.TotalModules.Should().BeGreaterThan(8);
        result.Value.DataFootprint.PropertyUnits.Should().Be(1);
        result.Value.DataFootprint.Residents.Should().Be(1);
        result.Value.ValueSummary.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetModuleRegistryAsync_RespectsCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await SeedCommercialDeliveryScenarioAsync(dbContext);
        var other = await AddCompoundAsync(dbContext, "FINAL-OTHER");
        dbContext.PropertyUnits.Add(new PropertyUnit
        {
            CompoundId = other.Id,
            UnitNumber = "OTHER-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Available
        });
        dbContext.UtilityBills.Add(new UtilityBill
        {
            CompoundId = other.Id,
            PropertyUnitId = Guid.NewGuid(),
            BillingCycleId = Guid.NewGuid(),
            BillNumber = "OTHER-BILL",
            BillStatus = BillStatus.Unpaid,
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            TotalAmount = 1000
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, allowed.Id);

        var result = await service.GetFinalDeliveryScorecardAsync(new FinalDeliveryQuery
        {
            Days = 30
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.DataFootprint.PropertyUnits.Should().Be(1);
        result.Value.DataFootprint.FinancialRecords.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetBuyerDemoReadinessAsync_ReturnsScenariosAndWarnings()
    {
        await using var dbContext = TestDb.Create();
        var compound = await SeedCommercialDeliveryScenarioAsync(dbContext);
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetBuyerDemoReadinessAsync(new FinalDeliveryQuery
        {
            CompoundId = compound.Id
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Scenarios.Should().Contain(item => item.ScenarioKey == "finance-demo" && item.IsReady);
        result.Value.Scenarios.Should().Contain(item => item.ScenarioKey == "handoff-demo" && item.IsReady);
    }

    [Fact]
    public async Task GetClientOnboardingReadinessAsync_FlagsMissingOnboardingEvidence()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "FINAL-MIN");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetClientOnboardingReadinessAsync(new FinalDeliveryQuery
        {
            CompoundId = compound.Id
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.OnboardingStatus.Should().Be("Blocked");
        result.Value.RequiredActions.Should().Contain(action => action.Contains("commercial license", StringComparison.OrdinalIgnoreCase));
    }

    private static CommercialProductizationService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new CommercialProductizationService(dbContext, new FakeCompoundAccessService([compoundId]));
    }

    private static async Task<Compound> SeedCommercialDeliveryScenarioAsync(ApplicationDbContext dbContext)
    {
        var now = DateTime.UtcNow;
        var compound = await AddCompoundAsync(dbContext, "FINAL-COMP");
        var unit = new PropertyUnit
        {
            Id = Guid.NewGuid(),
            CompoundId = compound.Id,
            UnitNumber = "FINAL-101",
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Occupied
        };
        var resident = new ResidentProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CompoundId = compound.Id,
            FullName = "Final Buyer Resident",
            PhoneNumber = "07000000000",
            IsActive = true
        };
        dbContext.PropertyUnits.Add(unit);
        dbContext.ResidentProfiles.Add(resident);
        dbContext.OccupancyRecords.Add(new OccupancyRecord
        {
            ResidentProfileId = resident.Id,
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            OccupancyType = OccupancyType.OwnerCash,
            OccupancyStatus = OccupancyStatus.Active,
            StartDate = DateOnly.FromDateTime(now.AddMonths(-2))
        });
        dbContext.UtilityBills.Add(new UtilityBill
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            BillingCycleId = Guid.NewGuid(),
            BillNumber = "FINAL-BILL-1",
            BillStatus = BillStatus.Unpaid,
            IssueDate = DateOnly.FromDateTime(now.AddDays(-5)),
            DueDate = DateOnly.FromDateTime(now.AddDays(10)),
            TotalAmount = 120000
        });
        dbContext.Payments.Add(new Payment
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = Guid.NewGuid(),
            PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Succeeded,
            Amount = 50000,
            PaymentReference = "FINAL-PAY-1",
            CompletedAt = now.AddDays(-1)
        });
        dbContext.RentContracts.Add(new RentContract
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            ResidentProfileId = resident.Id,
            ContractNumber = "FINAL-RENT",
            ContractStatus = RentContractStatus.Active,
            StartDate = DateOnly.FromDateTime(now.AddMonths(-1)),
            EndDate = DateOnly.FromDateTime(now.AddYears(1)),
            MonthlyRentAmount = 500000,
            DepositAmount = 1000000
        });
        dbContext.WorkOrders.Add(new WorkOrder
        {
            CompoundId = compound.Id,
            PropertyUnitId = unit.Id,
            Title = "Final demo work order",
            Description = "Buyer demo maintenance reliability evidence.",
            Status = WorkOrderStatus.InProgress,
            Priority = WorkOrderPriority.High
        });
        dbContext.MaintenanceAssets.Add(new MaintenanceAsset
        {
            CompoundId = compound.Id,
            Name = "Generator",
            Code = "GEN-FINAL",
            AssetType = MaintenanceAssetType.Generator,
            Status = MaintenanceAssetStatus.Active
        });
        dbContext.StockItems.Add(new StockItem
        {
            CompoundId = compound.Id,
            Name = "Filter",
            Sku = "FLT-FINAL",
            CurrentQuantity = 10,
            MinimumQuantity = 2
        });
        dbContext.ProcurementRequests.Add(new ProcurementRequest
        {
            CompoundId = compound.Id,
            Title = "Final spare parts request",
            Reason = "Demo procurement readiness",
            Status = ProcurementRequestStatus.PendingApproval
        });
        dbContext.VisitorPasses.Add(new VisitorPass
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            PropertyUnitId = unit.Id,
            VisitorName = "Demo Visitor",
            VisitorPhoneNumber = "07111111111",
            VisitReason = "Buyer demo",
            AccessCode = "FINAL-VISIT",
            Status = VisitorPassStatus.Approved,
            ValidFrom = now.AddHours(-1),
            ValidUntil = now.AddHours(4)
        });
        dbContext.ContractorWorkPermits.Add(new ContractorWorkPermit
        {
            CompoundId = compound.Id,
            VendorId = Guid.NewGuid(),
            Purpose = "Final contractor demo",
            WorkArea = "Gate A",
            Status = ContractorWorkPermitStatus.Approved,
            RiskLevel = ContractorWorkPermitRiskLevel.Low,
            AllowedFromUtc = now.AddHours(-2),
            AllowedUntilUtc = now.AddHours(3)
        });
        dbContext.Announcements.Add(new Announcement
        {
            CompoundId = compound.Id,
            Title = "Final demo announcement",
            Body = "Buyer demo announcement",
            Status = AnnouncementStatus.Published,
            PublishedAt = now.AddHours(-3)
        });
        dbContext.UtilityOutages.Add(new UtilityOutage
        {
            CompoundId = compound.Id,
            ServiceType = UtilityOutageServiceType.Electricity,
            Title = "Final demo outage",
            Description = "Buyer demo outage",
            Status = UtilityOutageStatus.Active,
            Severity = UtilityOutageSeverity.Medium
        });
        dbContext.Conversations.Add(new Conversation
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            PropertyUnitId = unit.Id,
            Status = ConversationStatus.PendingAdminReply,
            Priority = ConversationPriority.High,
            Topic = ConversationTopic.General,
            IssueType = ConversationIssueType.GeneralInquiry
        });
        dbContext.CollectionCases.Add(new CollectionCase
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            Status = CollectionCaseStatus.Open,
            Stage = CollectionStage.FirstNotice,
            AmountDue = 200000,
            Reason = "Final legal case evidence"
        });
        dbContext.LegalNotices.Add(new LegalNotice
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            NoticeType = LegalNoticeType.PaymentReminder,
            Status = LegalNoticeStatus.Issued,
            Title = "Final legal notice",
            Body = "Final legal notice body",
            IssuedAtUtc = now.AddDays(-1)
        });
        dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            CompoundId = compound.Id,
            ActionType = AuditActionType.SystemSettingUpdated,
            EntityType = AuditEntityType.SystemSetting,
            Severity = AuditSeverity.Medium,
            SourceModule = "FinalDelivery",
            Description = "Final delivery audit evidence.",
            CreatedAtUtc = now.AddMinutes(-30),
            CorrelationId = "FINAL-CORR"
        });
        dbContext.SystemSettings.Add(new SystemSetting
        {
            CompoundId = compound.Id,
            Key = "final.delivery.mode",
            Value = "enabled",
            ValueType = SystemSettingValueType.String,
            Description = "Final delivery readiness setting"
        });
        dbContext.NotificationTemplates.Add(new NotificationTemplate
        {
            EventType = NotificationEventType.General,
            Channel = NotificationChannel.Email,
            Code = "final.delivery",
            SubjectTemplate = "Final Delivery",
            BodyTemplate = "Final delivery body"
        });
        dbContext.SavedReports.Add(new SavedReport
        {
            CompoundId = compound.Id,
            CreatedByUserId = Guid.NewGuid(),
            ReportType = ManagementReportType.Financial,
            Name = "Final buyer report",
            IsActive = true
        });
        dbContext.ReportExportJobs.Add(new ReportExportJob
        {
            CompoundId = compound.Id,
            RequestedByUserId = Guid.NewGuid(),
            ReportType = ManagementReportType.Financial,
            Status = ReportExportJobStatus.Completed,
            RequestedAtUtc = now.AddHours(-1),
            CompletedAtUtc = now.AddMinutes(-20)
        });
        dbContext.DocumentFiles.Add(new DocumentFile
        {
            CompoundId = compound.Id,
            OriginalFileName = "handoff.pdf",
            StoredFileName = "handoff.pdf",
            ContentType = "application/pdf",
            Extension = ".pdf",
            SizeInBytes = 2048,
            StoragePath = "/docs/handoff.pdf",
            Category = DocumentCategory.Other,
            Visibility = DocumentVisibility.AdminOnly
        });
        dbContext.LicenseProfiles.Add(new LicenseProfile
        {
            LicensedTo = "Final Buyer",
            LicenseKeyFingerprint = "FINAL-LICENSE",
            Status = LicenseStatus.Active,
            Plan = LicensePlan.Enterprise,
            MaxCompounds = 10,
            MaxUnits = 5000,
            ExpiresAtUtc = now.AddYears(1)
        });

        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = new Compound
        {
            Id = Guid.NewGuid(),
            Name = "Final Delivery " + code,
            Code = code,
            City = "Baghdad",
            Area = "Karrada",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }
}

