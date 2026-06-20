using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.Enums;

namespace DARAK.Api.Interfaces;

public interface IPaymentService
{
    Task<PagedResult<PaymentResponse>> SearchPaymentsAsync(
        PaymentSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentResponse>> GetPaymentAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentResponse>> RecordManualPaymentAsync(
        ManualPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentResponse>> RefundPaymentAsync(
        Guid id,
        RefundPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<PaymentResponse>> SearchResidentPaymentsAsync(
        Guid userId,
        PaymentSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentResponse>> GetResidentPaymentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentResponse>> StartResidentPaymentAsync(
        Guid userId,
        StartPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentResponse>> ConfirmResidentMockPaymentSuccessAsync(
        Guid userId,
        Guid id,
        PaymentMethod expectedPaymentMethod,
        ConfirmMockPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentResponse>> ConfirmResidentMockPaymentFailureAsync(
        Guid userId,
        Guid id,
        PaymentMethod expectedPaymentMethod,
        ConfirmMockPaymentRequest request,
        CancellationToken cancellationToken = default);
}
