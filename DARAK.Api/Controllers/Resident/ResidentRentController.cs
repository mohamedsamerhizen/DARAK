using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Rents;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Resident)]
[Route("api/resident/rent")]
public sealed class ResidentRentController(
    ICurrentUserService currentUserService,
    IRentContractService rentContractService,
    IRentInvoiceService rentInvoiceService)
    : ApiControllerBase
{
    [HttpGet("contracts")]
    public async Task<ActionResult<PagedResult<RentContractResponse>>> SearchContracts(
        [FromQuery] RentContractSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return Ok(await rentContractService.SearchResidentRentContractsAsync(userId, query, cancellationToken));
    }

    [HttpGet("contracts/{id:guid}")]
    public async Task<ActionResult<RentContractResponse>> GetContract(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await rentContractService.GetResidentRentContractAsync(userId, id, cancellationToken));
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<PagedResult<RentInvoiceResponse>>> SearchInvoices(
        [FromQuery] RentInvoiceSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return Ok(await rentInvoiceService.SearchResidentRentInvoicesAsync(userId, query, cancellationToken));
    }

    [HttpGet("invoices/{id:guid}")]
    public async Task<ActionResult<RentInvoiceResponse>> GetInvoice(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await rentInvoiceService.GetResidentRentInvoiceAsync(userId, id, cancellationToken));
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

