using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operational;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DARAK.Tests;

public sealed class ExecutiveCommandCenterPack7Tests
{
    [Fact]
    public async Task GetExecutiveDailySummaryAsync_AggregatesCrossDomainExecutiveSignals()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "PACK7-EXEC");
        var resident = await AddResidentAsync(dbContext, compound.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        dbContext.CollectionCases.Add(new CollectionCase
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            AmountDue = 1_250_000m,
            Reason = "High overdue balance",
            Status = CollectionCaseStatus.LegalEscalated,
            DueDate = today.AddDays(-2),
            OpenedAtUtc = DateTime.UtcNow.AddDays(-6)
        });
        dbContext.WorkOrders.Add(new WorkOrder
        {
            CompoundId = compound.Id,
            Title = "Critical pump failure",
            Description = "Main pump unavailable",
            Status = WorkOrderStatus.InProgress,
            Priority = WorkOrderPriority.Emergency,
            SlaStatus = MaintenanceSlaStatus.Escalated,
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
            Description = "Water service unavailable",
            EstimatedStartAtUtc = DateTime.UtcNow.AddHours(-3),
            EstimatedEndAtUtc = DateTime.UtcNow.AddHours(2),
            CreatedAtUtc = DateTime.UtcNow.AddHours(-3)
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
            Description = "Executive review required",
            RecommendedAction = "Call resident and assign legal owner.",
            NextReviewAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        dbContext.StockItems.Add(new StockItem
        {
            CompoundId = compound.Id,
            Name = "Water pump seal",
            Sku = "PUMP-SEAL-01",
            CurrentQuantity = 0,
            MinimumQuantity = 2
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, compound.Id);

        var result = await service.GetExecutiveDailySummaryAsync(new ExecutiveIntelligenceQuery
        {
            CompoundId = compound.Id,
            ItemLimit = 20
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.ExecutiveScore.Should().BeLessThan(100);
        result.Value.CriticalSignalCount.Should().BeGreaterThan(0);
        result.Value.DomainSignals.Should().Contain(item => item.Domain == "Legal" && item.CriticalCount > 0);
        result.Value.DomainSignals.Should().Contain(item => item.Domain == "Maintenance" && item.CriticalCount > 0);
        result.Value.DomainSignals.Should().Contain(item => item.Domain == "Communications" && item.CriticalCount > 0);
        result.Value.DomainSignals.Should().Contain(item => item.Domain == "Inventory" && item.CriticalCount > 0);
        result.Value.DomainSignals.Should().Contain(item => item.Domain == "ResidentRisk" && item.CriticalCount > 0);
        result.Value.CriticalActions.Should().Contain(item => item.SourceType == "CollectionCase");
        result.Value.CriticalActions.Should().Contain(item => item.SourceType == "WorkOrder");
        result.Value.CriticalActions.Should().Contain(item => item.SourceType == "UtilityOutage");
        result.Value.CriticalActions.Should().Contain(item => item.SourceType == "ResidentRiskFlag");
        result.Value.DecisionBriefs.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetCriticalActionQueueAsync_ReturnsScopedOrderedExecutiveActions()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "PACK7-A");
        var blocked = await AddCompoundAsync(dbContext, "PACK7-B");
        var allowedResident = await AddResidentAsync(dbContext, allowed.Id);
        var blockedResident = await AddResidentAsync(dbContext, blocked.Id);

        dbContext.ResidentRiskFlags.AddRange(
            new ResidentRiskFlag
            {
                CompoundId = allowed.Id,
                ResidentProfileId = allowedResident.Id,
                CreatedByUserId = Guid.NewGuid(),
                FlagType = ResidentRiskFlagType.RepeatedLatePayments,
                Severity = ResidentRiskFlagSeverity.Critical,
                Status = ResidentRiskFlagStatus.Active,
                Source = ResidentRiskFlagSource.Manual,
                Title = "Allowed critical risk",
                Description = "Allowed"
            },
            new ResidentRiskFlag
            {
                CompoundId = blocked.Id,
                ResidentProfileId = blockedResident.Id,
                CreatedByUserId = Guid.NewGuid(),
                FlagType = ResidentRiskFlagType.RepeatedLatePayments,
                Severity = ResidentRiskFlagSeverity.Critical,
                Status = ResidentRiskFlagStatus.Active,
                Source = ResidentRiskFlagSource.Manual,
                Title = "Blocked critical risk",
                Description = "Blocked"
            });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, allowed.Id);

        var result = await service.GetCriticalActionQueueAsync(new ExecutiveIntelligenceQuery { CompoundId = allowed.Id });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Items.Should().ContainSingle(item => item.CompoundId == allowed.Id);
        result.Value.Items.Should().NotContain(item => item.CompoundId == blocked.Id);
    }

    [Fact]
    public async Task GetDomainSignalBoardAsync_ReturnsNotFoundOutsideCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "PACK7-SCOPE-A");
        var blocked = await AddCompoundAsync(dbContext, "PACK7-SCOPE-B");
        var service = CreateService(dbContext, allowed.Id);

        var result = await service.GetDomainSignalBoardAsync(new ExecutiveIntelligenceQuery { CompoundId = blocked.Id });

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

    private static async Task<ResidentProfile> AddResidentAsync(ApplicationDbContext dbContext, Guid compoundId)
    {
        var resident = new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            CompoundId = compoundId,
            FullName = "Pack 7 Resident",
            PhoneNumber = "+9647700000000",
            IsActive = true
        };
        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }
}
