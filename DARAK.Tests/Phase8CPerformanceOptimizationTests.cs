using FluentAssertions;

namespace DARAK.Tests;

public sealed class Phase8CPerformanceOptimizationTests
{
    [Fact]
    public void AuditDashboard_ShouldUseDatabaseSideAggregations()
    {
        var source = ReadSource("DARAK.Api", "Services", "Audit", "AuditLogService.cs");

        source.Should().Contain("GroupBy(log => log.Severity)");
        source.Should().Contain("FirstOrDefaultAsync(cancellationToken)");
        source.Should().Contain("new AuditCountByActionResponse(item.ActionType, item.Count)");
        source.Should().NotContain("var rows = await logs");
        source.Should().NotContain(".ToListAsync(cancellationToken);\n\n        var response = new AuditDashboardResponse");
    }

    [Theory]
    [InlineData("Billing", "UtilityBillService.cs")]
    [InlineData("Complaints", "ComplaintViolationService.cs")]
    [InlineData("Maintenance", "MaintenanceService.cs")]
    [InlineData("Meters", "MeterService.cs")]
    [InlineData("Payments", "PaymentService.cs")]
    [InlineData("Visitors", "VisitorPassService.cs")]
    public void ReadHeavyDetailsQueries_ShouldUseSplitQueriesForReadOnlyIncludeGraphs(
        string serviceFolder,
        string fileName)
    {
        var source = ReadSource("DARAK.Api", "Services", serviceFolder, fileName);

        source.Should().Contain("AsNoTracking().AsSplitQuery()");
    }

    [Fact]
    public void OperationsWorkOrderDetailsQuery_ShouldKeepSplitQueryProtectionForLargeIncludeGraph()
    {
        var source = ReadSource("DARAK.Api", "Services", "Operations", "OperationsService.cs");

        source.Should().Contain(".Include(workOrder => workOrder.CostItems)");
        source.Should().Contain(".Include(workOrder => workOrder.Ratings)");
        source.Should().Contain(".AsSplitQuery()");
    }

    private static string ReadSource(params string[] parts)
    {
        var root = FindRepositoryRoot();
        var pathParts = new string[parts.Length + 1];
        pathParts[0] = root;
        Array.Copy(parts, 0, pathParts, 1, parts.Length);
        var path = Path.Combine(pathParts);

        File.Exists(path).Should().BeTrue($"source file must exist: {path}");
        return File.ReadAllText(path);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DARAK.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("DARAK repository root could not be located.");
    }
}

