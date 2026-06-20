using DARAK.Api.DTOs.BillingCycles;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.CompoundServices;
using DARAK.Api.DTOs.UtilityBills;
using DARAK.Api.Interfaces;

namespace DARAK.Api.Services;

public sealed class UtilityBillingService(
    ICompoundServiceCatalogService compoundServiceCatalogService,
    IBillingCycleService billingCycleService,
    IUtilityBillService utilityBillService)
    : IUtilityBillingService
{
    public Task<PagedResult<CompoundServiceResponse>> SearchCompoundServicesAsync(CompoundServiceSearchQuery query, CancellationToken cancellationToken = default)
        => compoundServiceCatalogService.SearchCompoundServicesAsync(query, cancellationToken);

    public Task<ServiceResult<CompoundServiceResponse>> GetCompoundServiceAsync(Guid id, CancellationToken cancellationToken = default)
        => compoundServiceCatalogService.GetCompoundServiceAsync(id, cancellationToken);

    public Task<ServiceResult<CompoundServiceResponse>> CreateCompoundServiceAsync(CreateCompoundServiceRequest request, CancellationToken cancellationToken = default)
        => compoundServiceCatalogService.CreateCompoundServiceAsync(request, cancellationToken);

    public Task<ServiceResult<CompoundServiceResponse>> UpdateCompoundServiceAsync(Guid id, UpdateCompoundServiceRequest request, CancellationToken cancellationToken = default)
        => compoundServiceCatalogService.UpdateCompoundServiceAsync(id, request, cancellationToken);

    public Task<ServiceResult<object?>> DeactivateCompoundServiceAsync(Guid id, CancellationToken cancellationToken = default)
        => compoundServiceCatalogService.DeactivateCompoundServiceAsync(id, cancellationToken);

    public Task<PagedResult<BillingCycleResponse>> SearchBillingCyclesAsync(BillingCycleSearchQuery query, CancellationToken cancellationToken = default)
        => billingCycleService.SearchBillingCyclesAsync(query, cancellationToken);

    public Task<ServiceResult<BillingCycleResponse>> GetBillingCycleAsync(Guid id, CancellationToken cancellationToken = default)
        => billingCycleService.GetBillingCycleAsync(id, cancellationToken);

    public Task<ServiceResult<BillingCycleResponse>> CreateBillingCycleAsync(CreateBillingCycleRequest request, CancellationToken cancellationToken = default)
        => billingCycleService.CreateBillingCycleAsync(request, cancellationToken);

    public Task<ServiceResult<BillingCycleResponse>> UpdateBillingCycleAsync(Guid id, UpdateBillingCycleRequest request, CancellationToken cancellationToken = default)
        => billingCycleService.UpdateBillingCycleAsync(id, request, cancellationToken);

    public Task<ServiceResult<BillingCycleResponse>> CloseBillingCycleAsync(Guid id, CancellationToken cancellationToken = default)
        => billingCycleService.CloseBillingCycleAsync(id, cancellationToken);

    public Task<PagedResult<UtilityBillResponse>> SearchUtilityBillsAsync(UtilityBillSearchQuery query, CancellationToken cancellationToken = default)
        => utilityBillService.SearchUtilityBillsAsync(query, cancellationToken);

    public Task<ServiceResult<UtilityBillResponse>> GetUtilityBillAsync(Guid id, CancellationToken cancellationToken = default)
        => utilityBillService.GetUtilityBillAsync(id, cancellationToken);

    public Task<ServiceResult<UtilityBillResponse>> GenerateUtilityBillAsync(GenerateUtilityBillRequest request, CancellationToken cancellationToken = default)
        => utilityBillService.GenerateUtilityBillAsync(request, cancellationToken);

    public Task<ServiceResult<GenerateMonthlyUtilityBillsResponse>> GenerateMonthlyUtilityBillsAsync(GenerateMonthlyUtilityBillsRequest request, CancellationToken cancellationToken = default)
        => utilityBillService.GenerateMonthlyUtilityBillsAsync(request, cancellationToken);

    public Task<ServiceResult<UtilityBillResponse>> UpdateUtilityBillAsync(Guid id, UpdateUtilityBillRequest request, CancellationToken cancellationToken = default)
        => utilityBillService.UpdateUtilityBillAsync(id, request, cancellationToken);

    public Task<ServiceResult<UtilityBillResponse>> CancelUtilityBillAsync(Guid id, CancelUtilityBillRequest request, CancellationToken cancellationToken = default)
        => utilityBillService.CancelUtilityBillAsync(id, request, cancellationToken);

    public Task<ServiceResult<UtilityBillResponse>> RecalculateUtilityBillStatusAsync(Guid id, CancellationToken cancellationToken = default)
        => utilityBillService.RecalculateUtilityBillStatusAsync(id, cancellationToken);

    public Task<PagedResult<UtilityBillResponse>> SearchResidentBillsAsync(Guid userId, UtilityBillSearchQuery query, CancellationToken cancellationToken = default)
        => utilityBillService.SearchResidentBillsAsync(userId, query, cancellationToken);

    public Task<ServiceResult<UtilityBillResponse>> GetResidentBillAsync(Guid userId, Guid billId, CancellationToken cancellationToken = default)
        => utilityBillService.GetResidentBillAsync(userId, billId, cancellationToken);
}
