using DARAK.Api.Controllers;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class ControllerSurfaceRedesignPhase5CTests
{
    [Fact]
    public void Phase5C_RentAndOwnershipControllers_ShouldExist()
    {
        var controllerNames = GetControllerNames();

        controllerNames.Should().Contain(new[]
        {
            "AdminRentController",
            "AdminOwnershipController"
        });
    }

    [Fact]
    public void Phase5C_ObsoleteRentAndSalesControllers_ShouldBeRemoved()
    {
        var controllerNames = GetControllerNames();

        controllerNames.Should().NotContain(new[]
        {
            "AdminRentContractsController",
            "AdminRentInvoicesController",
            "AdminPropertySalesController"
        });
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
