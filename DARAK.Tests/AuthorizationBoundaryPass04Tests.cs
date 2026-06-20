using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using DARAK.Api.Controllers;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace DARAK.Tests;

public sealed class AuthorizationBoundaryPass04Tests
{
    [Fact]
    public void Pass04_AllControllerActions_AreAuthorizedOrExplicitlyAnonymous()
    {
        var violations = GetControllerTypes()
            .SelectMany(controllerType => GetActionMethods(controllerType)
                .Where(method => !HasEffectiveAuthorizationOrAnonymous(controllerType, method))
                .Select(method => $"{controllerType.Name}.{method.Name}"))
            .OrderBy(name => name)
            .ToArray();

        violations.Should().BeEmpty("every controller action must fail closed through [Authorize] or be explicitly [AllowAnonymous]");
    }

    [Fact]
    public void Pass04_MethodRoleGroups_DoNotLoseRolesThroughClassAuthorizeIntersection()
    {
        var violations = new List<string>();

        foreach (var controllerType in GetControllerTypes())
        {
            var classAuthorizeAttributes = controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Roles))
                .ToArray();
            if (classAuthorizeAttributes.Length == 0)
            {
                continue;
            }

            var classRoles = classAuthorizeAttributes
                .SelectMany(attribute => SplitRoles(attribute.Roles!))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var method in GetActionMethods(controllerType))
            {
                var methodRoles = method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                    .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Roles))
                    .SelectMany(attribute => SplitRoles(attribute.Roles!))
                    .ToHashSet(StringComparer.Ordinal);

                if (methodRoles.Count == 0)
                {
                    continue;
                }

                var lostRoles = methodRoles.Except(classRoles).OrderBy(role => role).ToArray();
                if (lostRoles.Length > 0)
                {
                    violations.Add($"{controllerType.Name}.{method.Name} declares roles [{string.Join(",", methodRoles)}] but class-level authorization removes [{string.Join(",", lostRoles)}]");
                }
            }
        }

        violations.Should().BeEmpty("method-level roles must not be broader than class-level roles because ASP.NET Core intersects stacked [Authorize] attributes");
    }

    [Fact]
    public void Pass04_AdminRoutes_DoNotAllowResidentRoleAtClassBoundary()
    {
        var violations = GetControllerTypes()
            .Where(HasAdminRoute)
            .Where(controllerType => ControllerClassRoles(controllerType).Contains(nameof(UserRole.Resident)))
            .Select(controllerType => controllerType.Name)
            .OrderBy(name => name)
            .ToArray();

        violations.Should().BeEmpty("resident-facing operations must not be exposed through api/admin route boundaries");
    }

    [Fact]
    public async Task Pass04_Accountant_CanReadViolationFineListButCannotCreateViolation()
    {
        using var factory = new DarakApiFactory();
        var accountant = await factory.CreateAuthenticatedClientAsync(UserRole.Accountant, Guid.NewGuid());

        var readResponse = await accountant.Client.GetAsync("/api/admin/violations/fines");
        var createResponse = await accountant.Client.PostAsJsonAsync("/api/admin/violations", new
        {
            CompoundId = Guid.NewGuid(),
            ResidentProfileId = Guid.NewGuid(),
            PropertyUnitId = Guid.NewGuid(),
            ViolationType = 1,
            Title = "Unauthorized accountant violation create",
            Description = "Accountants can read fines but cannot create violations."
        });

        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Pass04_Resident_CannotUseAdminWorkOrderRatingRouteButCanReachResidentRatingRoute()
    {
        using var factory = new DarakApiFactory();
        var resident = await factory.CreateAuthenticatedClientAsync(UserRole.Resident);
        var workOrderId = Guid.NewGuid();
        var request = new CreateWorkOrderRatingRequest
        {
            Rating = 5,
            Comment = "Resident route boundary check."
        };

        var adminRouteResponse = await resident.Client.PostAsJsonAsync(
            $"/api/admin/work-orders/{workOrderId}/ratings",
            request);
        var residentRouteResponse = await resident.Client.PostAsJsonAsync(
            $"/api/resident/requests/work-orders/{workOrderId}/ratings",
            request);

        adminRouteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        residentRouteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pass04_PublicHealthAndVersionEndpoints_RemainAnonymousAfterFallbackPolicy()
    {
        using var factory = new DarakApiFactory();
        using var client = factory.CreateClient();

        var health = await client.GetAsync("/health/live");
        var version = await client.GetAsync("/api/system/version");
        var protectedEndpoint = await client.GetAsync("/api/resident/account");

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        version.StatusCode.Should().Be(HttpStatusCode.OK);
        protectedEndpoint.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static IEnumerable<Type> GetControllerTypes()
    {
        return typeof(AuthController).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type)
                && !type.IsAbstract
                && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .OrderBy(type => type.FullName);
    }

    private static IEnumerable<MethodInfo> GetActionMethods(Type controllerType)
    {
        return controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any()
                || method.GetCustomAttributes<RouteAttribute>(inherit: true).Any());
    }

    private static bool HasEffectiveAuthorizationOrAnonymous(Type controllerType, MethodInfo method)
    {
        return controllerType.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()
            || method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()
            || controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()
            || method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
    }

    private static bool HasAdminRoute(Type controllerType)
    {
        return controllerType.GetCustomAttributes<RouteAttribute>(inherit: true)
            .Any(attribute => attribute.Template?.StartsWith("api/admin", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static HashSet<string> ControllerClassRoles(Type controllerType)
    {
        return controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Roles))
            .SelectMany(attribute => SplitRoles(attribute.Roles!))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> SplitRoles(string roles)
    {
        return roles
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(role => role.Trim());
    }
}
