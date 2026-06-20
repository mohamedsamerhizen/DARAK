using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.PaymentManagers)]
[Route("api/admin/payments/reconciliation-batches")]
public sealed class AdminPaymentReconciliationController(
    IPaymentReconciliationService reconciliationService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PaymentReconciliationBatchSummaryResponse>>> Search(
        [FromQuery] PaymentReconciliationBatchSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await reconciliationService.SearchBatchesAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentReconciliationBatchResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await reconciliationService.GetBatchAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<PaymentReconciliationBatchResponse>> Create(
        CreatePaymentReconciliationBatchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await reconciliationService.CreateBatchAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("{batchId:guid}/items/{itemId:guid}/review")]
    public async Task<ActionResult<PaymentReconciliationItemResponse>> ReviewItem(
        Guid batchId,
        Guid itemId,
        ReviewPaymentReconciliationItemRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await reconciliationService.ReviewItemAsync(
            currentUserService.UserId,
            batchId,
            itemId,
            request,
            cancellationToken));
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<PaymentReconciliationBatchResponse>> Close(
        Guid id,
        ClosePaymentReconciliationBatchRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await reconciliationService.CloseBatchAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }
}
