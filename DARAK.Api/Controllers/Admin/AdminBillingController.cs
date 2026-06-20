using DARAK.Api.DTOs.BillingCycles;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.CompoundServices;
using DARAK.Api.DTOs.UtilityBills;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.BillingManagers)]
[Route("api/admin/billing")]
public sealed class AdminBillingController(
    ICompoundServiceCatalogService compoundServiceCatalogService,
    IBillingCycleService billingCycleService,
    IUtilityBillService utilityBillService)
    : ApiControllerBase
{
    [HttpGet("services")]
    public async Task<ActionResult<PagedResult<CompoundServiceResponse>>> SearchServices(
        [FromQuery] CompoundServiceSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await compoundServiceCatalogService.SearchCompoundServicesAsync(query, cancellationToken));
    }

    [HttpGet("services/{id:guid}")]
    public async Task<ActionResult<CompoundServiceResponse>> GetService(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await compoundServiceCatalogService.GetCompoundServiceAsync(id, cancellationToken));
    }

    [HttpPost("services")]
    public async Task<ActionResult<CompoundServiceResponse>> CreateService(
        CreateCompoundServiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await compoundServiceCatalogService.CreateCompoundServiceAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetService), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("services/{id:guid}")]
    public async Task<ActionResult<CompoundServiceResponse>> UpdateService(
        Guid id,
        UpdateCompoundServiceRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await compoundServiceCatalogService.UpdateCompoundServiceAsync(id, request, cancellationToken));
    }

    [HttpDelete("services/{id:guid}")]
    public async Task<IActionResult> DeactivateService(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToNoContentResult(await compoundServiceCatalogService.DeactivateCompoundServiceAsync(id, cancellationToken));
    }

    [HttpGet("cycles")]
    public async Task<ActionResult<PagedResult<BillingCycleResponse>>> SearchCycles(
        [FromQuery] BillingCycleSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await billingCycleService.SearchBillingCyclesAsync(query, cancellationToken));
    }

    [HttpGet("cycles/{id:guid}")]
    public async Task<ActionResult<BillingCycleResponse>> GetCycle(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await billingCycleService.GetBillingCycleAsync(id, cancellationToken));
    }

    [HttpPost("cycles")]
    public async Task<ActionResult<BillingCycleResponse>> CreateCycle(
        CreateBillingCycleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await billingCycleService.CreateBillingCycleAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetCycle), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("cycles/{id:guid}")]
    public async Task<ActionResult<BillingCycleResponse>> UpdateCycle(
        Guid id,
        UpdateBillingCycleRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await billingCycleService.UpdateBillingCycleAsync(id, request, cancellationToken));
    }

    [HttpPost("cycles/{id:guid}/close")]
    public async Task<ActionResult<BillingCycleResponse>> CloseCycle(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await billingCycleService.CloseBillingCycleAsync(id, cancellationToken));
    }

    [HttpGet("utility-bills")]
    public async Task<ActionResult<PagedResult<UtilityBillResponse>>> SearchUtilityBills(
        [FromQuery] UtilityBillSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await utilityBillService.SearchUtilityBillsAsync(query, cancellationToken));
    }

    [HttpGet("utility-bills/{id:guid}")]
    public async Task<ActionResult<UtilityBillResponse>> GetUtilityBill(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await utilityBillService.GetUtilityBillAsync(id, cancellationToken));
    }

    [HttpPost("utility-bills/generate-single")]
    public async Task<ActionResult<UtilityBillResponse>> GenerateSingleUtilityBill(
        GenerateUtilityBillRequest request,
        CancellationToken cancellationToken)
    {
        var result = await utilityBillService.GenerateUtilityBillAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetUtilityBill), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("utility-bills/generate-monthly")]
    public async Task<ActionResult<GenerateMonthlyUtilityBillsResponse>> GenerateMonthlyUtilityBills(
        GenerateMonthlyUtilityBillsRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await utilityBillService.GenerateMonthlyUtilityBillsAsync(
            request,
            cancellationToken));
    }

    [HttpPut("utility-bills/{id:guid}")]
    public async Task<ActionResult<UtilityBillResponse>> UpdateUtilityBill(
        Guid id,
        UpdateUtilityBillRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await utilityBillService.UpdateUtilityBillAsync(id, request, cancellationToken));
    }

    [HttpPost("utility-bills/{id:guid}/cancel")]
    public async Task<ActionResult<UtilityBillResponse>> CancelUtilityBill(
        Guid id,
        CancelUtilityBillRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await utilityBillService.CancelUtilityBillAsync(id, request, cancellationToken));
    }

    [HttpPost("utility-bills/{id:guid}/recalculate-status")]
    public async Task<ActionResult<UtilityBillResponse>> RecalculateUtilityBillStatus(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await utilityBillService.RecalculateUtilityBillStatusAsync(id, cancellationToken));
    }
}
