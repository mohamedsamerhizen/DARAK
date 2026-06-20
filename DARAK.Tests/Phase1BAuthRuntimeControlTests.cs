using System.Security.Claims;
using DARAK.Api.Data;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DARAK.Tests;

public sealed class Phase1BAuthRuntimeControlTests
{
    [Fact]
    public async Task MaintenanceModeMiddleware_WhenDisabled_AllowsRequest()
    {
        await using var dbContext = TestDb.Create();
        var nextWasCalled = false;
        var middleware = new MaintenanceModeMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext("/api/admin/billing/cycles");

        await middleware.InvokeAsync(context, dbContext);

        nextWasCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task MaintenanceModeMiddleware_WhenEnabled_BlocksNonWhitelistedRequest()
    {
        await using var dbContext = TestDb.Create();
        await EnableMaintenanceModeAsync(dbContext, "Planned maintenance.");
        var nextWasCalled = false;
        var middleware = new MaintenanceModeMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext("/api/admin/billing/cycles");

        await middleware.InvokeAsync(context, dbContext);

        nextWasCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Contain("Planned maintenance.");
    }

    [Fact]
    public async Task MaintenanceModeMiddleware_WhenEnabled_AllowsWhitelistedHealthRequest()
    {
        await using var dbContext = TestDb.Create();
        await EnableMaintenanceModeAsync(dbContext, "Planned maintenance.");
        var nextWasCalled = false;
        var middleware = new MaintenanceModeMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext("/health");

        await middleware.InvokeAsync(context, dbContext);

        nextWasCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task MaintenanceModeMiddleware_WhenEnabled_AllowsSuperAdminRequest()
    {
        await using var dbContext = TestDb.Create();
        await EnableMaintenanceModeAsync(dbContext, "Planned maintenance.");
        var nextWasCalled = false;
        var middleware = new MaintenanceModeMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext("/api/admin/billing/cycles");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, RoleNames.SuperAdmin)
        ], "TestAuth"));

        await middleware.InvokeAsync(context, dbContext);

        nextWasCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    private static DefaultHttpContext CreateHttpContext(string path)
    {
        return new DefaultHttpContext
        {
            Request =
            {
                Path = path,
                Method = HttpMethods.Get
            },
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    private static async Task EnableMaintenanceModeAsync(ApplicationDbContext dbContext, string message)
    {
        dbContext.SystemSettings.AddRange(
            new SystemSetting
            {
                Key = "system.maintenance.enabled",
                Value = "true",
                ValueType = SystemSettingValueType.Boolean,
                Description = "Global maintenance mode flag."
            },
            new SystemSetting
            {
                Key = "system.maintenance.message",
                Value = message,
                ValueType = SystemSettingValueType.String,
                Description = "Global maintenance mode message."
            });
        await dbContext.SaveChangesAsync();
    }
}
