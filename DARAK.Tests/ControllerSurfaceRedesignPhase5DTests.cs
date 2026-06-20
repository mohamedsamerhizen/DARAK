using DARAK.Api.Controllers;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class ControllerSurfaceRedesignPhase5DTests
{
    [Fact]
    public void Phase5D_ResidentAccountAndCommunicationControllers_ShouldExist()
    {
        var controllerNames = GetControllerNames();

        controllerNames.Should().Contain(new[]
        {
            "ResidentAccountController",
            "AdminCommunicationController",
            "ResidentCommunicationController"
        });
    }

    [Fact]
    public void Phase5D_ObsoleteResidentAndCommunicationControllers_ShouldBeRemoved()
    {
        var controllerNames = GetControllerNames();

        controllerNames.Should().NotContain(new[]
        {
            "ResidentDashboardController",
            "ResidentPortalController",
            "ResidentProfileController",
            "ResidentBillsController",
            "ResidentMeterReadingsController",
            "ResidentViolationFinesController",
            "AnnouncementsController",
            "CommunityPollsController",
            "ResidentNotificationsController"
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
