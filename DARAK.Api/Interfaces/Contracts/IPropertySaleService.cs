using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.PropertySales;

namespace DARAK.Api.Interfaces;

public interface IPropertySaleService
{
    Task<PagedResult<PropertySaleContractResponse>> SearchSaleContractsAsync(PropertySaleContractSearchQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<PropertySaleContractResponse>> GetSaleContractAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<PropertySaleContractResponse>> CreateCashSaleContractAsync(CreateCashSaleContractRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<PropertySaleContractResponse>> CreateInstallmentSaleContractAsync(CreateInstallmentSaleContractRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<PropertySaleContractResponse>> CancelSaleContractAsync(Guid id, CancelSaleContractRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<InstallmentScheduleItemResponse>> SearchInstallmentsAsync(InstallmentSearchQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<InstallmentScheduleItemResponse>> GetInstallmentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<InstallmentScheduleItemResponse>> RecalculateInstallmentStatusAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<PropertySaleContractResponse>> SearchResidentSaleContractsAsync(Guid userId, PropertySaleContractSearchQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<PropertySaleContractResponse>> GetResidentSaleContractAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<InstallmentScheduleItemResponse>> SearchResidentInstallmentsAsync(Guid userId, InstallmentSearchQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<InstallmentScheduleItemResponse>> GetResidentInstallmentAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
