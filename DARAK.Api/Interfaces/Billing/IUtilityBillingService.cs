using DARAK.Api.DTOs.BillingCycles;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.CompoundServices;
using DARAK.Api.DTOs.UtilityBills;

namespace DARAK.Api.Interfaces;

public interface IUtilityBillingService
{
    Task<PagedResult<CompoundServiceResponse>> SearchCompoundServicesAsync(
        CompoundServiceSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CompoundServiceResponse>> GetCompoundServiceAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CompoundServiceResponse>> CreateCompoundServiceAsync(
        CreateCompoundServiceRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CompoundServiceResponse>> UpdateCompoundServiceAsync(
        Guid id,
        UpdateCompoundServiceRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> DeactivateCompoundServiceAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<BillingCycleResponse>> SearchBillingCyclesAsync(
        BillingCycleSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BillingCycleResponse>> GetBillingCycleAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BillingCycleResponse>> CreateBillingCycleAsync(
        CreateBillingCycleRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BillingCycleResponse>> UpdateBillingCycleAsync(
        Guid id,
        UpdateBillingCycleRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BillingCycleResponse>> CloseBillingCycleAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<UtilityBillResponse>> SearchUtilityBillsAsync(
        UtilityBillSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UtilityBillResponse>> GetUtilityBillAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UtilityBillResponse>> GenerateUtilityBillAsync(
        GenerateUtilityBillRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<GenerateMonthlyUtilityBillsResponse>> GenerateMonthlyUtilityBillsAsync(
        GenerateMonthlyUtilityBillsRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UtilityBillResponse>> UpdateUtilityBillAsync(
        Guid id,
        UpdateUtilityBillRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UtilityBillResponse>> CancelUtilityBillAsync(
        Guid id,
        CancelUtilityBillRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UtilityBillResponse>> RecalculateUtilityBillStatusAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<UtilityBillResponse>> SearchResidentBillsAsync(
        Guid userId,
        UtilityBillSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UtilityBillResponse>> GetResidentBillAsync(
        Guid userId,
        Guid billId,
        CancellationToken cancellationToken = default);
}
