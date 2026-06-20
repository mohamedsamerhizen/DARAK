using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Meters;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.BillingManagers)]
[Route("api/admin/meters")]
public sealed class AdminMetersController(IMeterService meterService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<MeterResponse>>> SearchMeters(
        [FromQuery] MeterSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await meterService.SearchMetersAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MeterResponse>> GetMeter(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await meterService.GetMeterAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<MeterResponse>> CreateMeter(
        CreateMeterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await meterService.CreateMeterAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetMeter), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MeterResponse>> UpdateMeter(
        Guid id,
        UpdateMeterRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await meterService.UpdateMeterAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeactivateMeter(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToNoContentResult(await meterService.DeactivateMeterAsync(id, cancellationToken));
    }

    [HttpGet("readings")]
    public async Task<ActionResult<PagedResult<MeterReadingResponse>>> SearchReadings(
        [FromQuery] MeterReadingSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await meterService.SearchMeterReadingsAsync(query, cancellationToken));
    }

    [HttpGet("readings/{id:guid}")]
    public async Task<ActionResult<MeterReadingResponse>> GetReading(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await meterService.GetMeterReadingAsync(id, cancellationToken));
    }

    [HttpPost("readings")]
    public async Task<ActionResult<MeterReadingResponse>> CreateReading(
        CreateMeterReadingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await meterService.CreateMeterReadingAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetReading), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("readings/{id:guid}")]
    public async Task<ActionResult<MeterReadingResponse>> UpdateReading(
        Guid id,
        UpdateMeterReadingRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await meterService.UpdateMeterReadingAsync(id, request, cancellationToken));
    }

    [HttpPost("readings/{id:guid}/generate-bill-line")]
    public async Task<ActionResult<MeterReadingResponse>> GenerateBillLineFromReading(
        Guid id,
        GenerateBillLineFromReadingRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await meterService.GenerateBillLineFromReadingAsync(id, request, cancellationToken));
    }
}
