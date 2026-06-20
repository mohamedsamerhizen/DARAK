using DARAK.Api.Controllers;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class ControllerSurfaceRedesignPhase5BTests
{
    [Fact]
    public void Phase5B_WorkflowControllers_ShouldExist()
    {
        var controllerNames = GetControllerNames();

        controllerNames.Should().Contain(new[]
        {
            "AdminResidentRequestsController",
            "ResidentRequestsController",
            "GuardAccessController",
            "MaintenanceWorkController",
            "AdminWorkOrdersController"
        });
    }

    [Fact]
    public void Phase5B_ObsoleteRequestAndOperationsControllers_ShouldBeRemoved()
    {
        var controllerNames = GetControllerNames();

        controllerNames.Should().NotContain(new[]
        {
            "AdminMaintenanceRequestsController",
            "AdminComplaintsController",
            "ResidentMaintenanceRequestsController",
            "ResidentComplaintsController",
            "ResidentVisitorPassesController",
            "GuardVisitorPassesController",
            "MaintenanceStaffRequestsController",
            "WorkOrdersController",
            "StaffMembersController",
            "ServiceVendorsController"
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
