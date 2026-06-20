using DARAK.Api.Controllers;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class ControllerSurfaceRedesignTests
{
    [Fact]
    public void Phase5A_AdminWorkflowControllers_ShouldExist()
    {
        var apiAssembly = typeof(ApiControllerBase).Assembly;
        var controllerNames = apiAssembly
            .GetTypes()
            .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        controllerNames.Should().Contain(new[]
        {
            "AdminPropertyStructureController",
            "AdminBillingController",
            "AdminMetersController",
            "AdminViolationsController",
            "AdminDashboardController"
        });
    }

    [Fact]
    public void Phase5A_ObsoleteEntityMappedAdminControllers_ShouldBeRemoved()
    {
        var apiAssembly = typeof(ApiControllerBase).Assembly;
        var controllerNames = apiAssembly
            .GetTypes()
            .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        controllerNames.Should().NotContain(new[]
        {
            "AdminBuildingsController",
            "AdminFloorsController",
            "AdminPropertyUnitsController",
            "AdminParkingSpotsController",
            "AdminCompoundServicesController",
            "AdminBillingCyclesController",
            "AdminUtilityBillsController",
            "AdminMeterReadingsController",
            "AdminViolationFinesController",
            "AnalyticsController"
        });
    }


    [Fact]
    public void Phase5E_FacilityControllers_ShouldBeRemoved()
    {
        var apiAssembly = typeof(ApiControllerBase).Assembly;
        var controllerNames = apiAssembly
            .GetTypes()
            .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        controllerNames.Should().NotContain(new[]
        {
            "AdminFacilitiesController",
            "FacilitiesController",
            "FacilityReservationsController"
        });
    }

}
