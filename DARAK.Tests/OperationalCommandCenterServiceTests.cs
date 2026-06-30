using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operational;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DARAK.Tests;

public sealed class OperationalCommandCenterServiceTests
{
    [Fact]
    public async Task GetCommandCenterAsync_AggregatesOperationalWorkloadRisksAndBreaches()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "OP18-C1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Operations Resident");
        dbContext.MaintenanceRequests.Add(new MaintenanceRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            PropertyUnitId = Guid.NewGuid(),
            Title = "Emergency water leak",
            Description = "Major leak",
            Priority = MaintenancePriority.Emergency,
            Status = MaintenanceStatus.Open,
            CreatedAt = DateTime.UtcNow.AddHours(-8)
        });
        dbContext.Complaints.Add(new Complaint
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            Title = "Critical noise complaint",
            Description = "Escalated resident complaint",
            Status = ComplaintStatus.Open,
            CreatedAt = DateTime.UtcNow.AddHours(-60)
        });
        dbContext.WorkOrders.Add(new WorkOrder
        {
            CompoundId = compound.Id,
            Title = "Generator repair",
            Description = "Repair generator",
            Priority = WorkOrderPriority.Urgent,
            Status = WorkOrderStatus.Assigned,
            DueAtUtc = DateTime.UtcNow.AddHours(-2),
            CreatedAtUtc = DateTime.UtcNow.AddHours(-20)
        });
        dbContext.ApprovalRequests.Add(new ApprovalRequest
        {
            CompoundId = compound.Id,
            RequestedByUserId = Guid.NewGuid(),
            ActionType = ApprovalActionType.ManualFinancialCorrection,
            Status = ApprovalStatus.Pending,
            Reason = "Needs approval"
        });
        dbContext.FinancialAdjustments.Add(new FinancialAdjustment
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            AdjustmentType = FinancialAdjustmentType.Debit,
            Status = FinancialAdjustmentStatus.PendingApproval,
            Amount = 100m,
            Reason = "Adjustment",
            RequestedByUserId = Guid.NewGuid()
        });
        dbContext.ResidentRiskFlags.Add(new ResidentRiskFlag
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            CreatedByUserId = Guid.NewGuid(),
            FlagType = ResidentRiskFlagType.RepeatedLatePayments,
            Severity = ResidentRiskFlagSeverity.Critical,
            Status = ResidentRiskFlagStatus.Active,
            Source = ResidentRiskFlagSource.Manual,
            Title = "Critical resident risk",
            Description = "Critical review required",
            NextReviewAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        dbContext.OperationalTasks.Add(new OperationalTask
        {
            CompoundId = compound.Id,
            CreatedByUserId = Guid.NewGuid(),
            Title = "Follow up overdue risk",
            Description = "Follow up",
            TaskType = OperationalTaskType.RiskReview,
            Priority = OperationalTaskPriority.Critical,
            Status = OperationalTaskStatus.Open,
            DueAtUtc = DateTime.UtcNow.AddHours(-1)
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetCommandCenterAsync(new OperationalCommandCenterQuery { CompoundId = compound.Id });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.OpenMaintenanceRequestCount.Should().Be(1);
        result.Value.EmergencyMaintenanceRequestCount.Should().Be(1);
        result.Value.OpenComplaintCount.Should().Be(1);
        result.Value.OpenWorkOrderCount.Should().Be(1);
        result.Value.PendingApprovalRequestCount.Should().Be(1);
        result.Value.PendingFinancialAdjustmentCount.Should().Be(1);
        result.Value.CriticalRiskFlagCount.Should().Be(1);
        result.Value.OverdueOperationalTaskCount.Should().Be(1);
        result.Value.SlaBreachCount.Should().BeGreaterThanOrEqualTo(4);
        result.Value.CompoundHealthScore.Should().BeLessThan(100);
        result.Value.PriorityItems.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSlaBreachesAsync_ReturnsPagedBreachesOrderedBySeverityAge()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "OP18-SLA");
        var resident = await AddResidentAsync(dbContext, compound.Id, "SLA Resident");
        dbContext.MaintenanceRequests.Add(new MaintenanceRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            PropertyUnitId = Guid.NewGuid(),
            Title = "Unassigned emergency",
            Description = "Emergency",
            Priority = MaintenancePriority.Emergency,
            Status = MaintenanceStatus.Open,
            CreatedAt = DateTime.UtcNow.AddHours(-12)
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetSlaBreachesAsync(new SlaBreachQuery { CompoundId = compound.Id, PageSize = 5 });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().SourceType.Should().Be("MaintenanceRequest");
        result.Value.Items.Single().BreachHours.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetCommandCenterAsync_ReturnsNotFoundOutsideCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowedCompound = await AddCompoundAsync(dbContext, "OP18-A");
        var blockedCompound = await AddCompoundAsync(dbContext, "OP18-B");
        var service = CreateService(dbContext, allowedCompound.Id);

        var result = await service.GetCommandCenterAsync(new OperationalCommandCenterQuery { CompoundId = blockedCompound.Id });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task CreateTaskAsync_CreatesOpenTaskAndAuditEntry()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "OP18-T1");
        var service = CreateService(dbContext, compound.Id);
        var userId = Guid.NewGuid();

        var result = await service.CreateTaskAsync(userId, new CreateOperationalTaskRequest
        {
            CompoundId = compound.Id,
            TaskType = OperationalTaskType.SlaBreachFollowUp,
            Priority = OperationalTaskPriority.High,
            Title = "Follow up SLA breach",
            Description = "Call the responsible operations owner.",
            DueAtUtc = DateTime.UtcNow.AddHours(4)
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(OperationalTaskStatus.Open);
        dbContext.OperationalTasks.Should().ContainSingle(task => task.Id == result.Value.Id && task.CreatedByUserId == userId);
        dbContext.AuditLogEntries.Should().ContainSingle(audit =>
            audit.ActionType == AuditActionType.OperationalTaskCreated
            && audit.EntityType == AuditEntityType.OperationalTask
            && audit.EntityId == result.Value.Id);
    }

    [Fact]
    public async Task CreateTaskAsync_ReturnsNotFoundOutsideCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowedCompound = await AddCompoundAsync(dbContext, "OP18-T2A");
        var blockedCompound = await AddCompoundAsync(dbContext, "OP18-T2B");
        var service = CreateService(dbContext, allowedCompound.Id);

        var result = await service.CreateTaskAsync(Guid.NewGuid(), new CreateOperationalTaskRequest
        {
            CompoundId = blockedCompound.Id,
            TaskType = OperationalTaskType.General,
            Priority = OperationalTaskPriority.Normal,
            Title = "Blocked",
            Description = "Should not be created.",
            DueAtUtc = DateTime.UtcNow.AddHours(1)
        });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
        dbContext.OperationalTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task Phase8_CreateTaskAsync_RejectsAssignedUserOutsideCompound()
    {
        await using var dbContext = TestDb.Create();
        var allowedCompound = await AddCompoundAsync(dbContext, "OP-P8-A");
        var blockedCompound = await AddCompoundAsync(dbContext, "OP-P8-B");
        var assignedUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "assigned.outside@darak.test",
            Email = "assigned.outside@darak.test",
            FullName = "Assigned Outside"
        };
        dbContext.Users.Add(assignedUser);
        dbContext.UserCompoundAssignments.Add(new UserCompoundAssignment
        {
            UserId = assignedUser.Id,
            CompoundId = blockedCompound.Id,
            Role = UserRole.CompoundAdmin,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, allowedCompound.Id);

        var result = await service.CreateTaskAsync(Guid.NewGuid(), new CreateOperationalTaskRequest
        {
            CompoundId = allowedCompound.Id,
            TaskType = OperationalTaskType.General,
            Priority = OperationalTaskPriority.Normal,
            Title = "Wrong compound assignment",
            Description = "Should be rejected.",
            AssignedToUserId = assignedUser.Id,
            DueAtUtc = DateTime.UtcNow.AddHours(1)
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("Assigned user");
        dbContext.OperationalTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteTaskAsync_CompletesOpenTaskAndWritesAuditEntry()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "OP18-T3");
        var task = await AddTaskAsync(dbContext, compound.Id, OperationalTaskStatus.Open);
        var service = CreateService(dbContext, compound.Id);
        var userId = Guid.NewGuid();

        var result = await service.CompleteTaskAsync(
            userId,
            task.Id,
            new CompleteOperationalTaskRequest { CompletionNotes = "Done and verified." });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Status.Should().Be(OperationalTaskStatus.Completed);
        result.Value.CompletedByUserId.Should().Be(userId);
        dbContext.AuditLogEntries.Should().ContainSingle(audit => audit.ActionType == AuditActionType.OperationalTaskCompleted);
    }

    [Fact]
    public async Task CancelTaskAsync_RejectsAlreadyCompletedTask()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "OP18-T4");
        var task = await AddTaskAsync(dbContext, compound.Id, OperationalTaskStatus.Completed);
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CancelTaskAsync(
            Guid.NewGuid(),
            task.Id,
            new CancelOperationalTaskRequest { Reason = "Cannot cancel completed task." });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
    }

    [Fact]
    public async Task SearchTasksAsync_RespectsCompoundScopeAndOverdueFilter()
    {
        await using var dbContext = TestDb.Create();
        var allowedCompound = await AddCompoundAsync(dbContext, "OP18-TA");
        var blockedCompound = await AddCompoundAsync(dbContext, "OP18-TB");
        await AddTaskAsync(dbContext, allowedCompound.Id, OperationalTaskStatus.Open, dueAtUtc: DateTime.UtcNow.AddHours(-2));
        await AddTaskAsync(dbContext, blockedCompound.Id, OperationalTaskStatus.Open, dueAtUtc: DateTime.UtcNow.AddHours(-2));
        var service = CreateService(dbContext, allowedCompound.Id);

        var result = await service.SearchTasksAsync(new OperationalTaskSearchQuery { IsOverdue = true });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().CompoundId.Should().Be(allowedCompound.Id);
    }

    [Fact]
    public async Task GetStaffPerformanceAsync_ReturnsWorkloadCompletionOverdueRatingAndCost()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "OP18-SP");
        var staff = new StaffMember
        {
            CompoundId = compound.Id,
            FullName = "Senior Technician",
            PhoneNumber = "+9647700001111"
        };
        var completedOrder = new WorkOrder
        {
            CompoundId = compound.Id,
            AssignedStaffMemberId = staff.Id,
            Title = "Completed repair",
            Description = "Completed",
            Status = WorkOrderStatus.Completed,
            Priority = WorkOrderPriority.High,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
            CompletedAtUtc = DateTime.UtcNow.AddDays(-1),
            ActualCost = 250m
        };
        var overdueOrder = new WorkOrder
        {
            CompoundId = compound.Id,
            AssignedStaffMemberId = staff.Id,
            Title = "Overdue repair",
            Description = "Overdue",
            Status = WorkOrderStatus.Assigned,
            Priority = WorkOrderPriority.Urgent,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            DueAtUtc = DateTime.UtcNow.AddHours(-3),
            ActualCost = 50m
        };
        dbContext.StaffMembers.Add(staff);
        dbContext.WorkOrders.AddRange(completedOrder, overdueOrder);
        dbContext.WorkOrderRatings.Add(new WorkOrderRating
        {
            WorkOrderId = completedOrder.Id,
            UserId = Guid.NewGuid(),
            Rating = 5
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetStaffPerformanceAsync(new StaffPerformanceQuery { CompoundId = compound.Id });

        result.IsSuccess.Should().BeTrue(result.Message);
        var row = result.Value!.StaffMembers.Single();
        row.StaffMemberId.Should().Be(staff.Id);
        row.CompletedWorkOrderCount.Should().Be(1);
        row.AssignedWorkOrderCount.Should().Be(1);
        row.OverdueWorkOrderCount.Should().Be(1);
        row.AverageRating.Should().Be(5m);
        row.ActualCostTotal.Should().Be(300m);
    }

    [Fact]
    public async Task GetCompoundHealthAsync_ReturnsPenaltiesForOperationalAndFinancialRisk()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "OP18-H1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Health Resident");
        dbContext.UtilityBills.Add(new UtilityBill
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            PropertyUnitId = Guid.NewGuid(),
            BillingCycleId = Guid.NewGuid(),
            BillNumber = "H-UTL-1",
            TotalAmount = 1000m,
            PaidAmount = 0m,
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-40),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-20),
            BillStatus = BillStatus.Unpaid
        });
        dbContext.MaintenanceRequests.Add(new MaintenanceRequest
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            PropertyUnitId = Guid.NewGuid(),
            Title = "Old emergency",
            Description = "Emergency",
            Priority = MaintenancePriority.Emergency,
            Status = MaintenanceStatus.Open,
            CreatedAt = DateTime.UtcNow.AddHours(-10)
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetCompoundHealthAsync(new CompoundHealthQuery { CompoundId = compound.Id });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.HealthScore.Should().BeLessThan(100);
        result.Value.OverdueFinancialItemCount.Should().Be(1);
        result.Value.Factors.Should().Contain(factor => factor.Area == "Finance");
        result.Value.Factors.Should().Contain(factor => factor.Area == "SLA");
    }


    [Fact]
    public async Task GetIntelligenceAsync_AggregatesCrossDomainCriticalItems()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "OP-INT-1");
        var resident = await AddResidentAsync(dbContext, compound.Id, "Intelligence Resident");
        dbContext.FinancialDisputes.Add(new FinancialDispute
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            TargetType = FinancialDisputeTargetType.UtilityBill,
            TargetId = Guid.NewGuid(),
            Status = FinancialDisputeStatus.UnderReview,
            Reason = "High utility bill dispute",
            ResidentMessage = "Please review the charge.",
            CreatedByUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
        });
        dbContext.CollectionCases.Add(new CollectionCase
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            AmountDue = 750_000m,
            Reason = "Overdue balance",
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5),
            Status = CollectionCaseStatus.Open,
            OpenedAtUtc = DateTime.UtcNow.AddDays(-5)
        });
        dbContext.WorkOrders.Add(new WorkOrder
        {
            CompoundId = compound.Id,
            Title = "SLA breached pump repair",
            Description = "Pump repair",
            Status = WorkOrderStatus.InProgress,
            Priority = WorkOrderPriority.Urgent,
            SlaStatus = MaintenanceSlaStatus.ResolutionBreached,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-30),
            ResolutionDueAtUtc = DateTime.UtcNow.AddHours(-3)
        });
        dbContext.UtilityOutages.Add(new UtilityOutage
        {
            CompoundId = compound.Id,
            ServiceType = UtilityOutageServiceType.Water,
            AffectedScope = UtilityOutageAffectedScope.Compound,
            Status = UtilityOutageStatus.Active,
            Severity = UtilityOutageSeverity.Critical,
            Title = "Critical water outage",
            Description = "Water outage",
            EstimatedStartAtUtc = DateTime.UtcNow.AddHours(-2),
            EstimatedEndAtUtc = DateTime.UtcNow.AddHours(2),
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
        });
        dbContext.SmartMeterDevices.Add(new SmartMeterDevice
        {
            CompoundId = compound.Id,
            MeterId = Guid.NewGuid(),
            DeviceIdentifier = "OFFLINE-INT-1",
            ProviderName = "Provider",
            HealthStatus = SmartMeterDeviceHealthStatus.Offline,
            LastSeenAtUtc = DateTime.UtcNow.AddDays(-2)
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetIntelligenceAsync(new AdminCommandCenterIntelligenceQuery
        {
            CompoundId = compound.Id,
            CriticalItemLimit = 10
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.OverallHealthScore.Should().BeLessThan(100);
        result.Value.CriticalItemCount.Should().BeGreaterThan(0);
        result.Value.Domains.Should().Contain(domain => domain.Domain == "Finance" && domain.OpenItemCount > 0);
        result.Value.Domains.Should().Contain(domain => domain.Domain == "Maintenance" && domain.CriticalItemCount > 0);
        result.Value.Domains.Should().Contain(domain => domain.Domain == "Communications" && domain.CriticalItemCount > 0);
        result.Value.Domains.Should().Contain(domain => domain.Domain == "SmartMeters" && domain.CriticalItemCount > 0);
        result.Value.CriticalItems.Should().Contain(item => item.SourceType == "CollectionCase");
        result.Value.CriticalItems.Should().Contain(item => item.SourceType == "WorkOrder");
        result.Value.CriticalItems.Should().Contain(item => item.SourceType == "UtilityOutage");
        result.Value.CriticalItems.Should().Contain(item => item.SourceType == "SmartMeterDevice");
    }

    [Fact]
    public async Task GetIntelligenceAsync_ReturnsNotFoundOutsideCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowedCompound = await AddCompoundAsync(dbContext, "OP-INT-A");
        var blockedCompound = await AddCompoundAsync(dbContext, "OP-INT-B");
        var service = CreateService(dbContext, allowedCompound.Id);

        var result = await service.GetIntelligenceAsync(new AdminCommandCenterIntelligenceQuery { CompoundId = blockedCompound.Id });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    private static OperationalCommandCenterService CreateService(ApplicationDbContext dbContext, params Guid[] allowedCompoundIds)
    {
        return new OperationalCommandCenterService(
            dbContext,
            new FakeCompoundAccessService(allowedCompoundIds),
            new AuditLogService(dbContext, new FakeCompoundAccessService(allowedCompoundIds), new HttpContextAccessor()));
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

    private static async Task<ResidentProfile> AddResidentAsync(ApplicationDbContext dbContext, Guid compoundId, string fullName)
    {
        var resident = new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            CompoundId = compoundId,
            FullName = fullName,
            PhoneNumber = "+9647700000000",
            IsActive = true
        };
        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }

    private static async Task<OperationalTask> AddTaskAsync(
        ApplicationDbContext dbContext,
        Guid compoundId,
        OperationalTaskStatus status,
        DateTime? dueAtUtc = null)
    {
        var task = new OperationalTask
        {
            CompoundId = compoundId,
            CreatedByUserId = Guid.NewGuid(),
            Title = "Operational task",
            Description = "Task description",
            TaskType = OperationalTaskType.General,
            Priority = OperationalTaskPriority.High,
            Status = status,
            DueAtUtc = dueAtUtc,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-4),
            CompletedAtUtc = status == OperationalTaskStatus.Completed ? DateTime.UtcNow.AddHours(-1) : null,
            CompletedByUserId = status == OperationalTaskStatus.Completed ? Guid.NewGuid() : null
        };
        dbContext.OperationalTasks.Add(task);
        await dbContext.SaveChangesAsync();
        return task;
    }
}
