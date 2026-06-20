using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.PropertySales;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.ContractManagers)]
[Route("api/admin/ownership")]
public sealed class AdminOwnershipController(IPropertySaleService propertySaleService)
    : ApiControllerBase
{
    [HttpGet("records")]
    public async Task<ActionResult<PagedResult<PropertySaleContractResponse>>> SearchOwnershipRecords(
        [FromQuery] PropertySaleContractSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await propertySaleService.SearchSaleContractsAsync(query, cancellationToken));
    }

    [HttpGet("records/{id:guid}")]
    public async Task<ActionResult<PropertySaleContractResponse>> GetOwnershipRecord(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await propertySaleService.GetSaleContractAsync(id, cancellationToken));
    }

    [HttpPost("cash-records")]
    public async Task<ActionResult<PropertySaleContractResponse>> RegisterCashOwnership(
        CreateCashSaleContractRequest request,
        CancellationToken cancellationToken)
    {
        var result = await propertySaleService.CreateCashSaleContractAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetOwnershipRecord), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("installment-records")]
    public async Task<ActionResult<PropertySaleContractResponse>> RegisterInstallmentOwnership(
        CreateInstallmentSaleContractRequest request,
        CancellationToken cancellationToken)
    {
        var result = await propertySaleService.CreateInstallmentSaleContractAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetOwnershipRecord), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("records/{id:guid}/cancel")]
    public async Task<ActionResult<PropertySaleContractResponse>> CancelOwnershipRecord(
        Guid id,
        CancelSaleContractRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await propertySaleService.CancelSaleContractAsync(id, request, cancellationToken));
    }

    [HttpGet("installments")]
    public async Task<ActionResult<PagedResult<InstallmentScheduleItemResponse>>> SearchInstallments(
        [FromQuery] InstallmentSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await propertySaleService.SearchInstallmentsAsync(query, cancellationToken));
    }

    [HttpGet("installments/{id:guid}")]
    public async Task<ActionResult<InstallmentScheduleItemResponse>> GetInstallment(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await propertySaleService.GetInstallmentAsync(id, cancellationToken));
    }

    [HttpPost("installments/{id:guid}/recalculate-status")]
    public async Task<ActionResult<InstallmentScheduleItemResponse>> RecalculateInstallmentStatus(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await propertySaleService.RecalculateInstallmentStatusAsync(id, cancellationToken));
    }
}
