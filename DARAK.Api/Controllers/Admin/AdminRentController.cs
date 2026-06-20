using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Rents;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.ContractManagers)]
[Route("api/admin/rent")]
public sealed class AdminRentController(
    IRentContractService rentContractService,
    IRentInvoiceService rentInvoiceService)
    : ApiControllerBase
{
    [HttpGet("contracts")]
    public async Task<ActionResult<PagedResult<RentContractResponse>>> SearchContracts(
        [FromQuery] RentContractSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await rentContractService.SearchRentContractsAsync(query, cancellationToken));
    }

    [HttpGet("contracts/{id:guid}")]
    public async Task<ActionResult<RentContractResponse>> GetContract(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await rentContractService.GetRentContractAsync(id, cancellationToken));
    }

    [HttpPost("contracts")]
    public async Task<ActionResult<RentContractResponse>> CreateContract(
        CreateRentContractRequest request,
        CancellationToken cancellationToken)
    {
        var result = await rentContractService.CreateRentContractAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetContract), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("contracts/{id:guid}/terminate")]
    public async Task<ActionResult<RentContractResponse>> TerminateContract(
        Guid id,
        TerminateRentContractRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await rentContractService.TerminateRentContractAsync(id, request, cancellationToken));
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<PagedResult<RentInvoiceResponse>>> SearchInvoices(
        [FromQuery] RentInvoiceSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await rentInvoiceService.SearchRentInvoicesAsync(query, cancellationToken));
    }

    [HttpGet("invoices/{id:guid}")]
    public async Task<ActionResult<RentInvoiceResponse>> GetInvoice(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await rentInvoiceService.GetRentInvoiceAsync(id, cancellationToken));
    }

    [HttpPost("invoices/generate-single")]
    public async Task<ActionResult<RentInvoiceResponse>> GenerateSingleInvoice(
        GenerateRentInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await rentInvoiceService.GenerateRentInvoiceAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetInvoice), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("invoices/generate-monthly")]
    public async Task<ActionResult<GenerateMonthlyRentInvoicesResponse>> GenerateMonthlyInvoices(
        GenerateMonthlyRentInvoicesRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await rentInvoiceService.GenerateMonthlyRentInvoicesAsync(request, cancellationToken));
    }

    [HttpPost("invoices/{id:guid}/cancel")]
    public async Task<ActionResult<RentInvoiceResponse>> CancelInvoice(
        Guid id,
        CancelRentInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await rentInvoiceService.CancelRentInvoiceAsync(id, request, cancellationToken));
    }

    [HttpPost("invoices/{id:guid}/recalculate-status")]
    public async Task<ActionResult<RentInvoiceResponse>> RecalculateInvoiceStatus(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await rentInvoiceService.RecalculateRentInvoiceStatusAsync(id, cancellationToken));
    }
}
