using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using DARAK.Api.DTOs;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Residents;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class DtoValidationPass07Tests
{
    [Fact]
    public void Pass07_PaginationQuery_RejectsOversizedPageSize()
    {
        var query = new PaginationQuery { PageNumber = 1, PageSize = 101 };

        Validate(query).Should().Contain(error => error.MemberNames.Contains(nameof(PaginationQuery.PageSize)));
    }

    [Fact]
    public void Pass07_NotEmptyGuid_RejectsEmptyRequiredGuidContracts()
    {
        var request = new CreateResidentProfileRequest
        {
            UserId = Guid.Empty,
            CompoundId = Guid.Empty,
            FullName = "Resident PASS07"
        };

        var errors = Validate(request);

        errors.Should().Contain(error => error.MemberNames.Contains(nameof(CreateResidentProfileRequest.UserId)));
        errors.Should().Contain(error => error.MemberNames.Contains(nameof(CreateResidentProfileRequest.CompoundId)));
    }

    [Fact]
    public void Pass07_SearchTerm_HasCentralMaxLengthGuard()
    {
        var query = new ResidentProfileSearchQuery
        {
            SearchTerm = new string('x', 201)
        };

        Validate(query).Should().Contain(error => error.MemberNames.Contains(nameof(ResidentProfileSearchQuery.SearchTerm)));
    }

    [Fact]
    public async Task Pass07_InvalidModelState_UsesApiErrorResponseEnvelope()
    {
        using var factory = new DarakApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { });
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().NotBeNull();
        body!.TraceId.Should().NotBeNullOrWhiteSpace();
        body.Message.Should().Be("Validation failed.");
        body.Errors.Should().NotBeNull();
        body.Errors!.Keys.Should().Contain(key => key.Contains("Email", StringComparison.OrdinalIgnoreCase));
        body.Errors!.Keys.Should().Contain(key => key.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<ValidationResult> Validate(object instance)
    {
        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);
        return results;
    }
}
