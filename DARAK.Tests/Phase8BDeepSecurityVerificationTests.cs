using System.Net;
using System.Net.Http.Json;
using DARAK.Api.Enums;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class Phase8BDeepSecurityVerificationTests
{
    [Theory]
    [InlineData("/api/admin/system/health")]
    [InlineData("/api/resident/account")]
    [InlineData("/api/guard/access/visitors/today")]
    public async Task AnonymousCaller_CannotAccessProtectedSurfaces(string path)
    {
        using var factory = new DarakApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResidentToken_CannotAccessGuardVisitorSurface()
    {
        using var factory = new DarakApiFactory();
        var resident = await factory.CreateAuthenticatedClientAsync(UserRole.Resident);

        var response = await resident.Client.GetAsync("/api/guard/access/visitors/today");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GuardToken_CannotAccessResidentAccountSurface()
    {
        using var factory = new DarakApiFactory();
        var guard = await factory.CreateAuthenticatedClientAsync(UserRole.Guard, Guid.NewGuid());

        var response = await guard.Client.GetAsync("/api/resident/account");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AccountantToken_CannotManageSuperAdminUsers()
    {
        using var factory = new DarakApiFactory();
        var accountant = await factory.CreateAuthenticatedClientAsync(UserRole.Accountant, Guid.NewGuid());

        var response = await accountant.Client.GetAsync("/api/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AccountantToken_CannotToggleMaintenanceMode()
    {
        using var factory = new DarakApiFactory();
        var accountant = await factory.CreateAuthenticatedClientAsync(UserRole.Accountant, Guid.NewGuid());

        var response = await accountant.Client.PostAsJsonAsync(
            "/api/admin/system/maintenance-mode",
            new
            {
                isEnabled = true,
                message = "Security regression test should be blocked before service execution."
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ResidentToken_CannotReadAuditLogs()
    {
        using var factory = new DarakApiFactory();
        var resident = await factory.CreateAuthenticatedClientAsync(UserRole.Resident);

        var response = await resident.Client.GetAsync("/api/admin/audit/logs");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GuardToken_CannotSearchAdminDocuments()
    {
        using var factory = new DarakApiFactory();
        var guard = await factory.CreateAuthenticatedClientAsync(UserRole.Guard, Guid.NewGuid());

        var response = await guard.Client.GetAsync("/api/admin/documents");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MaintenanceStaffToken_CannotReadAdminFinanceDashboard()
    {
        using var factory = new DarakApiFactory();
        var maintenanceStaff = await factory.CreateAuthenticatedClientAsync(UserRole.MaintenanceStaff);

        var response = await maintenanceStaff.Client.GetAsync("/api/admin/finance/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ResidentToken_CannotAccessApprovalDashboard()
    {
        using var factory = new DarakApiFactory();
        var resident = await factory.CreateAuthenticatedClientAsync(UserRole.Resident);

        var response = await resident.Client.GetAsync("/api/admin/approvals/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
