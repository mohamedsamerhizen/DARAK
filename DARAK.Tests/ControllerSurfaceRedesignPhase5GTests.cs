using DARAK.Api.Controllers;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class ControllerSurfaceRedesignPhase5GTests
{
    [Fact]
    public void Phase5G_DocumentControllers_ShouldBeSplitByActor()
    {
        var controllerNames = GetControllerNames();

        controllerNames.Should().Contain(new[]
        {
            "AdminDocumentsController",
            "ResidentDocumentsController"
        });
    }

    [Fact]
    public void Phase5G_GenericDocumentsController_ShouldBeRemoved()
    {
        var controllerNames = GetControllerNames();

        controllerNames.Should().NotContain("DocumentsController");
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
