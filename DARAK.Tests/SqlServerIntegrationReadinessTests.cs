using System.Data.Common;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class SqlServerIntegrationReadinessTests
{
    [Fact]
    public async Task SqlServerMigrations_WhenSqlServerAvailable_ApplyWithNoPendingMigrations()
    {
        await RunIfSqlServerAvailableAsync(async dbContext =>
        {
            var pending = await dbContext.Database.GetPendingMigrationsAsync();

            pending.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task SqlServerPersistence_WhenSqlServerAvailable_EnforcesCompoundSkuUniqueIndex()
    {
        await RunIfSqlServerAvailableAsync(async dbContext =>
        {
            var compound = new Compound
            {
                Name = "SQL Test Compound",
                Code = $"SQL-{Guid.NewGuid():N}"[..12],
                City = "Baghdad",
                Area = "Karrada"
            };
            dbContext.Compounds.Add(compound);
            await dbContext.SaveChangesAsync();
            dbContext.StockItems.AddRange(
                new StockItem
                {
                    CompoundId = compound.Id,
                    Name = "SQL Filter",
                    Sku = "SQL-UNIQUE-01",
                    UnitOfMeasure = "pcs"
                },
                new StockItem
                {
                    CompoundId = compound.Id,
                    Name = "SQL Filter Duplicate",
                    Sku = "SQL-UNIQUE-01",
                    UnitOfMeasure = "pcs"
                });

            var act = () => dbContext.SaveChangesAsync();

            await act.Should().ThrowAsync<DbUpdateException>();
        });
    }

    [Fact]
    public async Task SqlServerPersistence_WhenSqlServerAvailable_KeepsStockNonNegative()
    {
        await RunIfSqlServerAvailableAsync(async dbContext =>
        {
            var compound = new Compound
            {
                Name = "SQL Stock Compound",
                Code = $"SQL-{Guid.NewGuid():N}"[..12],
                City = "Baghdad",
                Area = "Karrada"
            };
            var stock = new StockItem
            {
                CompoundId = compound.Id,
                Name = "SQL Limited Stock",
                Sku = "SQL-LIMITED-01",
                UnitOfMeasure = "pcs",
                CurrentQuantity = 1,
                MinimumQuantity = 0
            };
            var workOrder = new WorkOrder
            {
                CompoundId = compound.Id,
                Title = "SQL Stock Issue",
                Description = "SQL integration stock issue.",
                Priority = WorkOrderPriority.Normal,
                Status = WorkOrderStatus.New,
                SourceType = WorkOrderSourceType.Manual
            };
            dbContext.AddRange(compound, stock, workOrder);
            await dbContext.SaveChangesAsync();
            var service = new ProcurementInventoryService(dbContext, new FakeCompoundAccessService([compound.Id]));

            var result = await service.IssueStockToWorkOrderAsync(
                stock.Id,
                Guid.NewGuid(),
                new IssueStockToWorkOrderRequest
                {
                    WorkOrderId = workOrder.Id,
                    Quantity = 2
                });

            result.Status.Should().Be(ServiceResultStatus.Conflict);
            (await dbContext.StockItems.FindAsync(stock.Id))!.CurrentQuantity.Should().Be(1);
        });
    }

    private static async Task RunIfSqlServerAvailableAsync(Func<ApplicationDbContext, Task> assertion)
    {
        if (!TryBuildConnectionStrings(out var masterConnectionString, out var testConnectionString))
        {
            return;
        }

        if (!await CanConnectAsync(masterConnectionString))
        {
            return;
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(testConnectionString)
            .Options;
        await using var dbContext = new ApplicationDbContext(options);

        try
        {
            await dbContext.Database.MigrateAsync();
            await assertion(dbContext);
        }
        finally
        {
            try
            {
                await dbContext.Database.EnsureDeletedAsync();
            }
            catch
            {
                // Best-effort cleanup for optional local SQL integration databases.
            }
        }
    }

    private static async Task<bool> CanConnectAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var dbContext = new ApplicationDbContext(options);

        try
        {
            return await dbContext.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static bool TryBuildConnectionStrings(out string masterConnectionString, out string testConnectionString)
    {
        masterConnectionString = string.Empty;
        testConnectionString = string.Empty;
        var raw = Environment.GetEnvironmentVariable("DARAK_SQLSERVER_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(raw) || LooksLikePlaceholder(raw))
        {
            return false;
        }

        try
        {
            masterConnectionString = WithDatabase(raw, "master");
            testConnectionString = WithDatabase(raw, $"DARAK_SQL_IT_{Guid.NewGuid():N}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        builder.Remove("Database");
        builder.Remove("Initial Catalog");
        builder["Database"] = databaseName;

        if (!builder.ContainsKey("TrustServerCertificate"))
        {
            builder["TrustServerCertificate"] = "True";
        }

        return builder.ConnectionString;
    }

    private static bool LooksLikePlaceholder(string value)
    {
        return value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
            || value.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
            || value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
            || value.Contains("EXAMPLE_", StringComparison.OrdinalIgnoreCase);
    }
}
