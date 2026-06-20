using DARAK.Api.Controllers;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class ControllerBaseTests
{
    [Fact]
    public void ResidentControllers_InheritFromApiControllerBase()
    {
        var apiAssembly = typeof(ApiControllerBase).Assembly;

        var residentControllers = apiAssembly
            .GetTypes()
            .Where(type => type.Name.StartsWith("Resident", StringComparison.Ordinal)
                && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .ToArray();

        residentControllers.Should().NotBeEmpty();
        residentControllers.Should().OnlyContain(type => typeof(ApiControllerBase).IsAssignableFrom(type));
    }

    [Fact]
    public void LegacyAdminBaseType_IsNotPresent()
    {
        var legacyTypeName = "Admin" + "ControllerBase";

        typeof(ApiControllerBase).Assembly
            .GetTypes()
            .Should()
            .NotContain(type => type.Name == legacyTypeName);
    }
}
