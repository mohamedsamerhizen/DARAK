using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.PropertySales;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Resident)]
[Route("api/resident/property-contracts")]
public sealed class ResidentPropertyContractsController(
    ICurrentUserService currentUserService,
    IPropertySaleService propertySaleService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PropertySaleContractResponse>>> Search(
        [FromQuery] PropertySaleContractSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return Ok(await propertySaleService.SearchResidentSaleContractsAsync(userId, query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PropertySaleContractResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await propertySaleService.GetResidentSaleContractAsync(userId, id, cancellationToken));
    }

    [HttpGet("installments")]
    public async Task<ActionResult<PagedResult<InstallmentScheduleItemResponse>>> SearchInstallments(
        [FromQuery] InstallmentSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return Ok(await propertySaleService.SearchResidentInstallmentsAsync(userId, query, cancellationToken));
    }

    [HttpGet("installments/{id:guid}")]
    public async Task<ActionResult<InstallmentScheduleItemResponse>> GetInstallment(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await propertySaleService.GetResidentInstallmentAsync(userId, id, cancellationToken));
    }

    private bool TryGetCurrentUserId(out Guid userId, out ObjectResult unauthorizedResult)
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId.HasValue)
        {
            userId = currentUserId.Value;
            unauthorizedResult = null!;
            return true;
        }

        userId = Guid.Empty;
        unauthorizedResult = Unauthorized(ApiErrorResponseFactory.Create(HttpContext, "Current user is invalid."));
        return false;
    }
}

