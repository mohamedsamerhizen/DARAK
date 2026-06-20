using DARAK.Api.Controllers;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class ControllerSurfaceRedesignPhase5FTests
{
    [Fact]
    public void Phase5F_ResidentsController_ShouldRemainAsCompleteResidentAdministrationSurface()
    {
        var controllerNames = GetControllerNames();

        controllerNames.Should().Contain("AdminResidentsController");
    }

    [Fact]
    public void Phase5F_ObsoleteOccupanciesController_ShouldBeRemoved()
    {
        var controllerNames = GetControllerNames();

        controllerNames.Should().NotContain("AdminOccupanciesController");
    }

    private static HashSet<string> GetControllerNames()
    {
        var apiAssembly = typeof(ApiControllerBase).Assembly;

        return apiAssembly
            .GetTypes()
            .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);
    }
}
