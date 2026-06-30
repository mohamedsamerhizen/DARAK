using DARAK.Api.Data;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class MaintenanceReliabilityServiceTests
{
    [Fact]
    public async Task CreateAssetAsync_CreatesCompoundScopedAsset()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "MR-1");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateAssetAsync(new CreateMaintenanceAssetRequest
        {
            CompoundId = compound.Id,
            Name = "Main elevator",
            Code = "ELV-A-01",
            AssetType = MaintenanceAssetType.Elevator,
            Status = MaintenanceAssetStatus.Active,
            LocationDescription = "Building A"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Code.Should().Be("ELV-A-01");
        result.Value.AssetType.Should().Be(MaintenanceAssetType.Elevator);
        dbContext.MaintenanceAssets.Should().ContainSingle(asset => asset.CompoundId == compound.Id && asset.Code == "ELV-A-01");
    }

    [Fact]
    public async Task CreateAssetAsync_RejectsDuplicateCodeInsideCompound()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "MR-2");
        var service = CreateService(dbContext, compound.Id);
        var request = new CreateMaintenanceAssetRequest
        {
            CompoundId = compound.Id,
            Name = "Generator",
            Code = "GEN-01",
            AssetType = MaintenanceAssetType.Generator
        };

        var first = await service.CreateAssetAsync(request);
        var second = await service.CreateAssetAsync(request);

        first.IsSuccess.Should().BeTrue(first.Message);
        second.Status.Should().Be(DARAK.Api.DTOs.Common.ServiceResultStatus.Conflict);
    }

    [Fact]
    public async Task GeneratePreventiveWorkOrderAsync_CreatesAssetLinkedWorkOrderAndAppliesSla()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "MR-3");
        var service = CreateService(dbContext, compound.Id);
        var asset = await service.CreateAssetAsync(new CreateMaintenanceAssetRequest
        {
            CompoundId = compound.Id,
            Name = "Water pump",
            Code = "PUMP-01",
            AssetType = MaintenanceAssetType.Pump
        });
        await service.CreateSlaPolicyAsync(new CreateMaintenanceSlaPolicyRequest
        {
            CompoundId = compound.Id,
            Name = "Urgent preventive SLA",
            Priority = WorkOrderPriority.High,
            SourceType = WorkOrderSourceType.Other,
            ResponseDueMinutes = 30,
            ResolutionDueMinutes = 240
        });
        var plan = await service.CreatePreventivePlanAsync(new CreatePreventiveMaintenancePlanRequest
        {
            MaintenanceAssetId = asset.Value!.Id,
            Title = "Monthly pump inspection",
            Description = "Inspect pump and pressure.",
            Cadence = PreventiveMaintenanceCadence.Monthly,
            Priority = WorkOrderPriority.High,
            NextDueAtUtc = DateTime.UtcNow.AddDays(1)
        });

        var generated = await service.GeneratePreventiveWorkOrderAsync(
            plan.Value!.Id,
            Guid.NewGuid(),
            new GeneratePreventiveWorkOrderRequest());

        generated.IsSuccess.Should().BeTrue(generated.Message);
        generated.Value!.MaintenanceAssetId.Should().Be(asset.Value.Id);
        generated.Value.SlaStatus.Should().Be(MaintenanceSlaStatus.WithinSla);
        generated.Value.ResponseDueAtUtc.Should().NotBeNull();
        dbContext.WorkOrders.Should().ContainSingle(order => order.MaintenanceAssetId == asset.Value.Id);
        dbContext.PreventiveMaintenancePlans.Single().LastGeneratedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Phase7_GeneratePreventiveWorkOrderAsync_IsIdempotentForSameDueWindow()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "MR-P7-IDEM");
        var service = CreateService(dbContext, compound.Id);
        var asset = await service.CreateAssetAsync(new CreateMaintenanceAssetRequest
        {
            CompoundId = compound.Id,
            Name = "Idempotent generator",
            Code = "PM-IDEM-01",
            AssetType = MaintenanceAssetType.Generator
        });
        var dueWindow = DateTime.UtcNow.AddMinutes(-10);
        var plan = await service.CreatePreventivePlanAsync(new CreatePreventiveMaintenancePlanRequest
        {
            MaintenanceAssetId = asset.Value!.Id,
            Title = "Generator due-window inspection",
            Description = "Inspect generator once for the due window.",
            Cadence = PreventiveMaintenanceCadence.Monthly,
            Priority = WorkOrderPriority.Normal,
            NextDueAtUtc = dueWindow
        });

        var first = await service.GeneratePreventiveWorkOrderAsync(
            plan.Value!.Id,
            Guid.NewGuid(),
            new GeneratePreventiveWorkOrderRequest { ScheduledAtUtc = dueWindow });
        var second = await service.GeneratePreventiveWorkOrderAsync(
            plan.Value.Id,
            Guid.NewGuid(),
            new GeneratePreventiveWorkOrderRequest { ScheduledAtUtc = dueWindow });

        first.IsSuccess.Should().BeTrue(first.Message);
        second.IsSuccess.Should().BeTrue(second.Message);
        second.Value!.WorkOrderId.Should().Be(first.Value!.WorkOrderId);
        dbContext.WorkOrders.Count(order => order.SourceEntityId == plan.Value.Id).Should().Be(1);
    }

    [Fact]
    public async Task Phase7_RefreshSlaBreachesAsync_EscalatesResolutionBreachOnce()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "MR-P7-SLA");
        var active = new WorkOrder
        {
            CompoundId = compound.Id,
            Title = "Overdue active SLA",
            Description = "Resolution deadline passed.",
            SourceType = WorkOrderSourceType.Manual,
            Priority = WorkOrderPriority.High,
            Status = WorkOrderStatus.InProgress,
            SlaStatus = MaintenanceSlaStatus.WithinSla,
            ResponseDueAtUtc = DateTime.UtcNow.AddHours(-4),
            ResolutionDueAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        var completed = new WorkOrder
        {
            CompoundId = compound.Id,
            Title = "Completed overdue SLA",
            Description = "Should not escalate.",
            SourceType = WorkOrderSourceType.Manual,
            Priority = WorkOrderPriority.High,
            Status = WorkOrderStatus.Completed,
            SlaStatus = MaintenanceSlaStatus.WithinSla,
            ResponseDueAtUtc = DateTime.UtcNow.AddHours(-4),
            ResolutionDueAtUtc = DateTime.UtcNow.AddHours(-1),
            CompletedAtUtc = DateTime.UtcNow
        };
        dbContext.WorkOrders.AddRange(active, completed);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var firstRefresh = await service.RefreshSlaBreachesAsync(new MaintenanceReliabilitySummaryQuery { CompoundId = compound.Id });
        var secondRefresh = await service.RefreshSlaBreachesAsync(new MaintenanceReliabilitySummaryQuery { CompoundId = compound.Id });

        firstRefresh.IsSuccess.Should().BeTrue(firstRefresh.Message);
        firstRefresh.Value!.UpdatedCount.Should().Be(1);
        secondRefresh.Value!.UpdatedCount.Should().Be(0);
        var refreshedActive = dbContext.WorkOrders.Single(order => order.Id == active.Id);
        refreshedActive.SlaStatus.Should().Be(MaintenanceSlaStatus.Escalated);
        refreshedActive.SlaBreachedAtUtc.Should().NotBeNull();
        refreshedActive.SlaEscalatedAtUtc.Should().NotBeNull();
        refreshedActive.SlaEscalationCount.Should().Be(1);
        dbContext.WorkOrders.Single(order => order.Id == completed.Id).SlaStatus.Should().Be(MaintenanceSlaStatus.WithinSla);
    }

    [Fact]
    public async Task ChecklistRun_CopiesTemplateItemsAndTracksFailedRequiredItems()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "MR-4");
        var service = CreateService(dbContext, compound.Id);
        var template = await service.CreateChecklistTemplateAsync(new CreateOperationalChecklistTemplateRequest
        {
            CompoundId = compound.Id,
            Name = "Preventive inspection",
            Items =
            [
                new CreateOperationalChecklistTemplateItemRequest { Title = "Check safety switch", IsRequired = true, SortOrder = 1 },
                new CreateOperationalChecklistTemplateItemRequest { Title = "Clean machine room", IsRequired = false, SortOrder = 2 }
            ]
        });
        var run = await service.StartChecklistRunAsync(
            Guid.NewGuid(),
            new StartOperationalChecklistRunRequest
            {
                TemplateId = template.Value!.Id,
                TargetType = OperationalChecklistTargetType.MaintenanceAsset,
                TargetId = Guid.NewGuid()
            });

        var firstRequiredItem = run.Value!.Items.Single(item => item.IsRequired);
        var optionalItem = run.Value.Items.Single(item => !item.IsRequired);
        var completed = await service.CompleteChecklistRunAsync(
            run.Value.Id,
            Guid.NewGuid(),
            new CompleteOperationalChecklistRunRequest
            {
                SummaryNotes = "Inspection completed with one required failure.",
                Items =
                [
                    new CompleteOperationalChecklistRunItemRequest { ItemId = firstRequiredItem.Id, Status = OperationalChecklistItemStatus.Failed, Notes = "Switch did not respond." },
                    new CompleteOperationalChecklistRunItemRequest { ItemId = optionalItem.Id, Status = OperationalChecklistItemStatus.Passed }
                ]
            });

        completed.IsSuccess.Should().BeTrue(completed.Message);
        completed.Value!.Status.Should().Be(OperationalChecklistRunStatus.Completed);
        completed.Value.FailedRequiredItemCount.Should().Be(1);
    }

    [Fact]
    public async Task SummaryAsync_ReturnsReliabilityCountsForAllowedCompound()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "MR-5");
        var service = CreateService(dbContext, compound.Id);
        await service.CreateAssetAsync(new CreateMaintenanceAssetRequest
        {
            CompoundId = compound.Id,
            Name = "Camera rack",
            Code = "CAM-RACK-01",
            AssetType = MaintenanceAssetType.Camera,
            Status = MaintenanceAssetStatus.OutOfService
        });
        await service.CreateSlaPolicyAsync(new CreateMaintenanceSlaPolicyRequest
        {
            CompoundId = compound.Id,
            Name = "Default SLA",
            ResponseDueMinutes = 60,
            ResolutionDueMinutes = 480
        });

        var summary = await service.GetSummaryAsync(new MaintenanceReliabilitySummaryQuery { CompoundId = compound.Id });

        summary.IsSuccess.Should().BeTrue(summary.Message);
        summary.Value!.AssetsOutOfServiceCount.Should().Be(1);
        summary.Value.ActiveSlaPolicyCount.Should().Be(1);
    }


    [Fact]
    public async Task Pack3PreventiveMaintenanceDueQueue_ReturnsOverduePlansWithRiskSignal()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "MR-P3-1");
        var service = CreateService(dbContext, compound.Id);
        var asset = await service.CreateAssetAsync(new CreateMaintenanceAssetRequest
        {
            CompoundId = compound.Id,
            Name = "Generator pack 3",
            Code = "GEN-P3-01",
            AssetType = MaintenanceAssetType.Generator
        });
        await service.CreatePreventivePlanAsync(new CreatePreventiveMaintenancePlanRequest
        {
            MaintenanceAssetId = asset.Value!.Id,
            Title = "Generator overdue inspection",
            Description = "Inspect oil and cooling system.",
            Cadence = PreventiveMaintenanceCadence.Monthly,
            Priority = WorkOrderPriority.Urgent,
            NextDueAtUtc = DateTime.UtcNow.AddDays(-2)
        });

        var queue = await service.GetPreventiveMaintenanceDueQueueAsync(new PreventiveMaintenanceDueQueueQuery
        {
            CompoundId = compound.Id,
            DueWithinDays = 7,
            PageSize = 10
        });

        queue.TotalCount.Should().Be(1);
        queue.Items.Single().IsOverdue.Should().BeTrue();
        queue.Items.Single().RiskLevel.Should().Be("Critical");
    }

    [Fact]
    public async Task Pack3AssetReliabilityProfile_ReturnsCriticalBandForOutOfServiceAsset()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "MR-P3-2");
        var service = CreateService(dbContext, compound.Id);
        var asset = await service.CreateAssetAsync(new CreateMaintenanceAssetRequest
        {
            CompoundId = compound.Id,
            Name = "Main pump pack 3",
            Code = "PUMP-P3-01",
            AssetType = MaintenanceAssetType.Pump,
            Status = MaintenanceAssetStatus.OutOfService
        });

        var profile = await service.GetAssetReliabilityProfileAsync(
            asset.Value!.Id,
            new MaintenanceAssetReliabilityQuery());

        profile.IsSuccess.Should().BeTrue(profile.Message);
        profile.Value!.ReliabilityBand.Should().Be("Critical");
    }

    private static MaintenanceReliabilityService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new MaintenanceReliabilityService(dbContext, new FakeCompoundAccessService([compoundId]));
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
}
