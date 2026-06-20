using DARAK.Api.DTOs.AdminPortal;
using DARAK.Api.DTOs.Analytics;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Entities;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class ReportingAccessTests
{
    [Fact]
    public async Task AdminPortalOverview_ReturnsForbiddenForUnassignedCompound()
    {
        await using var dbContext = TestDb.Create();
        var compoundId = await SeedCompoundAsync(dbContext);
        var service = new AdminPortalService(dbContext, new FakeCompoundAccessService());

        var result = await service.GetPaymentsOverviewAsync(new AdminOverviewQuery
        {
            CompoundId = compoundId
        });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
    }

    [Fact]
    public async Task AdminPortalOverview_AllowsAssignedCompound()
    {
        await using var dbContext = TestDb.Create();
        var compoundId = await SeedCompoundAsync(dbContext);
        var service = new AdminPortalService(
            dbContext,
            new FakeCompoundAccessService([compoundId]));

        var result = await service.GetPaymentsOverviewAsync(new AdminOverviewQuery
        {
            CompoundId = compoundId
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
    }

    [Fact]
    public async Task AnalyticsReport_ReturnsForbiddenForUnassignedCompound()
    {
        await using var dbContext = TestDb.Create();
        var compoundId = await SeedCompoundAsync(dbContext);
        var service = new AnalyticsService(dbContext, new FakeCompoundAccessService());

        var result = await service.GetFinancialReportAsync(new DateRangeQueryRequest
        {
            CompoundId = compoundId
        });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
    }

    [Fact]
    public async Task AnalyticsReport_AllowsAssignedCompound()
    {
        await using var dbContext = TestDb.Create();
        var compoundId = await SeedCompoundAsync(dbContext);
        var service = new AnalyticsService(
            dbContext,
            new FakeCompoundAccessService([compoundId]));

        var result = await service.GetFinancialReportAsync(new DateRangeQueryRequest
        {
            CompoundId = compoundId
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task AdminPortalOverview_ValidatesTopCountBeforeAccessChecks(int topCount)
    {
        await using var dbContext = TestDb.Create();
        var compoundId = await SeedCompoundAsync(dbContext);
        var service = new AdminPortalService(dbContext, new FakeCompoundAccessService());

        var result = await service.GetDashboardAsync(new AdminOverviewQuery
        {
            CompoundId = compoundId,
            TopCount = topCount
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task AnalyticsReport_RequiresCompoundId()
    {
        await using var dbContext = TestDb.Create();
        var service = new AnalyticsService(dbContext, new FakeCompoundAccessService(isSuperAdmin: true));

        var result = await service.GetFinancialReportAsync(new DateRangeQueryRequest());

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    private static async Task<Guid> SeedCompoundAsync(DARAK.Api.Data.ApplicationDbContext dbContext)
    {
        var compound = new Compound
        {
            Name = "Darak",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound.Id;
    }
}
