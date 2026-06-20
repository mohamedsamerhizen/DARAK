using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Resident)]
[Route("api/resident/payments")]
public sealed class ResidentPaymentsController(
    ICurrentUserService currentUserService,
    IPaymentService paymentService,
    IConfiguration configuration)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PaymentResponse>>> Search(
        [FromQuery] PaymentSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return Ok(await paymentService.SearchResidentPaymentsAsync(userId, query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await paymentService.GetResidentPaymentAsync(userId, id, cancellationToken));
    }

    [HttpPost("start")]
    public async Task<ActionResult<PaymentResponse>> Start(
        StartPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        var result = await paymentService.StartResidentPaymentAsync(userId, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("{id:guid}/mock-zaincash/success")]
    public async Task<ActionResult<PaymentResponse>> ConfirmZainCashSuccess(
        Guid id,
        ConfirmMockPaymentRequest request,
        CancellationToken cancellationToken)
    {
        return await ConfirmSuccessAsync(
            id,
            PaymentMethod.ZainCashMock,
            request,
            cancellationToken);
    }

    [HttpPost("{id:guid}/mock-zaincash/failure")]
    public async Task<ActionResult<PaymentResponse>> ConfirmZainCashFailure(
        Guid id,
        ConfirmMockPaymentRequest request,
        CancellationToken cancellationToken)
    {
        return await ConfirmFailureAsync(
            id,
            PaymentMethod.ZainCashMock,
            request,
            cancellationToken);
    }

    [HttpPost("{id:guid}/mock-mastercard/success")]
    public async Task<ActionResult<PaymentResponse>> ConfirmMasterCardSuccess(
        Guid id,
        ConfirmMockPaymentRequest request,
        CancellationToken cancellationToken)
    {
        return await ConfirmSuccessAsync(
            id,
            PaymentMethod.MasterCardMock,
            request,
            cancellationToken);
    }

    [HttpPost("{id:guid}/mock-mastercard/failure")]
    public async Task<ActionResult<PaymentResponse>> ConfirmMasterCardFailure(
        Guid id,
        ConfirmMockPaymentRequest request,
        CancellationToken cancellationToken)
    {
        return await ConfirmFailureAsync(
            id,
            PaymentMethod.MasterCardMock,
            request,
            cancellationToken);
    }

    private async Task<ActionResult<PaymentResponse>> ConfirmSuccessAsync(
        Guid id,
        PaymentMethod paymentMethod,
        ConfirmMockPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!MockGatewayEndpointsEnabled())
        {
            return NotFound(ApiErrorResponseFactory.Create(
                HttpContext,
                "Mock payment gateway endpoints are disabled."));
        }

        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await paymentService.ConfirmResidentMockPaymentSuccessAsync(
            userId,
            id,
            paymentMethod,
            request,
            cancellationToken));
    }

    private async Task<ActionResult<PaymentResponse>> ConfirmFailureAsync(
        Guid id,
        PaymentMethod paymentMethod,
        ConfirmMockPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!MockGatewayEndpointsEnabled())
        {
            return NotFound(ApiErrorResponseFactory.Create(
                HttpContext,
                "Mock payment gateway endpoints are disabled."));
        }

        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await paymentService.ConfirmResidentMockPaymentFailureAsync(
            userId,
            id,
            paymentMethod,
            request,
            cancellationToken));
    }

    private bool MockGatewayEndpointsEnabled()
    {
        return bool.TryParse(configuration["Payments:EnableMockGatewayEndpoints"], out var enabled) && enabled;
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

