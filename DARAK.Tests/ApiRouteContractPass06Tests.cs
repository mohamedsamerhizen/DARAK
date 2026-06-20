using System.Reflection;
using DARAK.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace DARAK.Tests;

public sealed class ApiRouteContractPass06Tests
{
    [Fact]
    public void Pass06_ControllerRouteTemplates_DoNotUseControllerToken()
    {
        var tokenizedRoutes = GetControllerTypes()
            .SelectMany(controllerType => controllerType
                .GetCustomAttributes<RouteAttribute>(inherit: true)
                .Where(attribute => attribute.Template?.Contains("[controller]", StringComparison.OrdinalIgnoreCase) == true)
                .Select(attribute => $"{controllerType.Name}: {attribute.Template}"))
            .OrderBy(item => item)
            .ToArray();

        tokenizedRoutes.Should().BeEmpty("public route metadata should use explicit lowercase route templates");
    }

    [Fact]
    public void Pass06_ControllerRoutes_UseApprovedApiPrefixes()
    {
        var approvedPrefixes = new[]
        {
            "api/admin",
            "api/resident",
            "api/guard",
            "api/maintenance",
            "api/system",
            "api/auth"
        };

        var invalidRoutes = GetRouteEntries()
            .Where(entry => !approvedPrefixes.Any(prefix => IsUnderPrefix(entry.Route, prefix)))
            .Select(entry => $"{entry.Verb} {entry.Route} -> {entry.ControllerAction}")
            .OrderBy(item => item)
            .ToArray();

        invalidRoutes.Should().BeEmpty("every controller endpoint should stay under an approved actor/domain API prefix");
    }

    [Fact]
    public void Pass06_ControllerRoutes_DoNotDefineDuplicateVerbAndRoutePairs()
    {
        var duplicates = GetRouteEntries()
            .GroupBy(entry => $"{entry.Verb} {entry.Route}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(entry => entry.ControllerAction).OrderBy(item => item))}")
            .OrderBy(item => item)
            .ToArray();

        duplicates.Should().BeEmpty("duplicate HTTP verb and route pairs make API routing ambiguous");
    }

    [Fact]
    public void Pass06_RouteContract_CoversLargeControllerSurface()
    {
        GetRouteEntries().Should().HaveCountGreaterThan(500);
    }

    private static IReadOnlyList<RouteEntry> GetRouteEntries()
    {
        return GetControllerTypes()
            .SelectMany(controllerType => GetActionMethods(controllerType)
                .SelectMany(method => BuildRouteEntries(controllerType, method)))
            .OrderBy(entry => entry.Route)
            .ThenBy(entry => entry.Verb)
            .ThenBy(entry => entry.ControllerAction)
            .ToArray();
    }

    private static IEnumerable<RouteEntry> BuildRouteEntries(Type controllerType, MethodInfo method)
    {
        var controllerRoutes = controllerType
            .GetCustomAttributes<RouteAttribute>(inherit: true)
            .Select(attribute => attribute.Template ?? string.Empty)
            .DefaultIfEmpty(string.Empty)
            .ToArray();

        var httpMethodAttributes = method
            .GetCustomAttributes<HttpMethodAttribute>(inherit: true)
            .ToArray();

        foreach (var controllerRoute in controllerRoutes)
        {
            foreach (var httpAttribute in httpMethodAttributes)
            {
                var route = NormalizeRoute(CombineRoutes(controllerRoute, httpAttribute.Template ?? string.Empty));
                var httpMethods = httpAttribute.HttpMethods.ToArray();
                var verbs = httpMethods.Length == 0
                    ? new[] { "ANY" }
                    : httpMethods;

                foreach (var verb in verbs)
                {
                    yield return new RouteEntry(
                        verb.ToUpperInvariant(),
                        route,
                        $"{controllerType.Name}.{method.Name}");
                }
            }
        }
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
            .Where(method => !method.GetCustomAttributes<NonActionAttribute>(inherit: true).Any())
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any());
    }

    private static string CombineRoutes(string controllerRoute, string actionRoute)
    {
        if (actionRoute.StartsWith("~/", StringComparison.Ordinal))
        {
            return actionRoute[2..];
        }

        if (actionRoute.StartsWith("/", StringComparison.Ordinal))
        {
            return actionRoute[1..];
        }

        return string.Join('/', new[] { controllerRoute, actionRoute }
            .Where(route => !string.IsNullOrWhiteSpace(route)));
    }

    private static string NormalizeRoute(string route)
    {
        return route
            .Replace("//", "/", StringComparison.Ordinal)
            .Trim('/')
            .ToLowerInvariant();
    }

    private static bool IsUnderPrefix(string route, string prefix)
    {
        return route.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || route.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record RouteEntry(string Verb, string Route, string ControllerAction);
}
