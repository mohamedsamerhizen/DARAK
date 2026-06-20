using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Payments;

namespace DARAK.Api.Interfaces;

public interface IPaymentReconciliationService
{
    Task<PagedResult<PaymentReconciliationBatchSummaryResponse>> SearchBatchesAsync(
        PaymentReconciliationBatchSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentReconciliationBatchResponse>> GetBatchAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentReconciliationBatchResponse>> CreateBatchAsync(
        Guid? createdByUserId,
        CreatePaymentReconciliationBatchRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentReconciliationItemResponse>> ReviewItemAsync(
        Guid? reviewedByUserId,
        Guid batchId,
        Guid itemId,
        ReviewPaymentReconciliationItemRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentReconciliationBatchResponse>> CloseBatchAsync(
        Guid? closedByUserId,
        Guid id,
        ClosePaymentReconciliationBatchRequest request,
        CancellationToken cancellationToken = default);
}
