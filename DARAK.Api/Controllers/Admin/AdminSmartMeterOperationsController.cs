using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Meters;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.BillingManagers)]
[Route("api/admin/smart-meter-operations")]
public sealed class AdminSmartMeterOperationsController(ISmartMeterOperationsService smartMeterOperationsService)
    : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<SmartMeterOperationsSummaryResponse>> Summary(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await smartMeterOperationsService.GetSummaryAsync(compoundId, cancellationToken));
    }

    [HttpGet("devices")]
    public async Task<ActionResult<PagedResult<SmartMeterDeviceResponse>>> SearchDevices(
        [FromQuery] SmartMeterDeviceQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await smartMeterOperationsService.SearchDevicesAsync(query, cancellationToken));
    }

    [HttpGet("devices/{id:guid}")]
    public async Task<ActionResult<SmartMeterDeviceResponse>> GetDevice(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await smartMeterOperationsService.GetDeviceAsync(id, cancellationToken));
    }

    [HttpPost("devices")]
    public async Task<ActionResult<SmartMeterDeviceResponse>> RegisterDevice(
        RegisterSmartMeterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await smartMeterOperationsService.RegisterDeviceAsync(request, cancellationToken));
    }

    [HttpPatch("devices/{id:guid}/status")]
    public async Task<ActionResult<SmartMeterDeviceResponse>> UpdateDeviceStatus(
        Guid id,
        UpdateSmartMeterDeviceStatusRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await smartMeterOperationsService.UpdateDeviceStatusAsync(id, request, cancellationToken));
    }

    [HttpPost("devices/{id:guid}/readings/ingest")]
    public async Task<ActionResult<SmartMeterReadingIngestionResponse>> IngestReading(
        Guid id,
        IngestSmartMeterReadingRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await smartMeterOperationsService.IngestReadingAsync(id, request, cancellationToken));
    }

    [HttpGet("ingestions")]
    public async Task<ActionResult<PagedResult<SmartMeterReadingIngestionResponse>>> SearchIngestions(
        [FromQuery] SmartMeterIngestionQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await smartMeterOperationsService.SearchIngestionsAsync(query, cancellationToken));
    }

    [HttpPost("devices/refresh-health")]
    public async Task<ActionResult<int>> RefreshDeviceHealth(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await smartMeterOperationsService.RefreshDeviceHealthAsync(compoundId, cancellationToken));
    }
}
