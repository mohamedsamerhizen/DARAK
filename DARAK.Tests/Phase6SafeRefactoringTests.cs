using DARAK.Api.DTOs.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DARAK.Tests;

public sealed class Phase6SafeRefactoringTests
{
    [Theory]
    [InlineData(ServiceResultStatus.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ServiceResultStatus.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ServiceResultStatus.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ServiceResultStatus.BadRequest, StatusCodes.Status400BadRequest)]
    [InlineData(ServiceResultStatus.Success, StatusCodes.Status400BadRequest)]
    public void ToErrorHttpStatusCode_PreservesExistingControllerStatusMapping(
        ServiceResultStatus status,
        int expectedStatusCode)
    {
        status.ToErrorHttpStatusCode().Should().Be(expectedStatusCode);
    }
}
