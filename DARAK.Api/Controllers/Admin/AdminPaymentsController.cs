using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.PaymentManagers)]
[Route("api/admin/payments")]
public sealed class AdminPaymentsController(IPaymentService paymentService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PaymentResponse>>> Search(
        [FromQuery] PaymentSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await paymentService.SearchPaymentsAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await paymentService.GetPaymentAsync(id, cancellationToken));
    }

    [HttpPost("manual")]
    public async Task<ActionResult<PaymentResponse>> RecordManual(
        ManualPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await paymentService.RecordManualPaymentAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.PaymentRefundManagers)]
    [HttpPost("{id:guid}/refund")]
    public async Task<ActionResult<PaymentResponse>> Refund(
        Guid id,
        RefundPaymentRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await paymentService.RefundPaymentAsync(id, request, cancellationToken));
    }
}

