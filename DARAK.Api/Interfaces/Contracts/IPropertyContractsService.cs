using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.PropertySales;
using DARAK.Api.DTOs.Rents;

namespace DARAK.Api.Interfaces;

public interface IPropertyContractsService
{
    Task<PagedResult<PropertySaleContractResponse>> SearchSaleContractsAsync(
        PropertySaleContractSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PropertySaleContractResponse>> GetSaleContractAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PropertySaleContractResponse>> CreateCashSaleContractAsync(
        CreateCashSaleContractRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PropertySaleContractResponse>> CreateInstallmentSaleContractAsync(
        CreateInstallmentSaleContractRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PropertySaleContractResponse>> CancelSaleContractAsync(
        Guid id,
        CancelSaleContractRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<InstallmentScheduleItemResponse>> SearchInstallmentsAsync(
        InstallmentSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<InstallmentScheduleItemResponse>> GetInstallmentAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<InstallmentScheduleItemResponse>> RecalculateInstallmentStatusAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<RentContractResponse>> SearchRentContractsAsync(
        RentContractSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RentContractResponse>> GetRentContractAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RentContractResponse>> CreateRentContractAsync(
        CreateRentContractRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RentContractResponse>> TerminateRentContractAsync(
        Guid id,
        TerminateRentContractRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RentInvoiceResponse>> GenerateRentInvoiceAsync(
        GenerateRentInvoiceRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<GenerateMonthlyRentInvoicesResponse>> GenerateMonthlyRentInvoicesAsync(
        GenerateMonthlyRentInvoicesRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<RentInvoiceResponse>> SearchRentInvoicesAsync(
        RentInvoiceSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RentInvoiceResponse>> GetRentInvoiceAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RentInvoiceResponse>> CancelRentInvoiceAsync(
        Guid id,
        CancelRentInvoiceRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RentInvoiceResponse>> RecalculateRentInvoiceStatusAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<PropertySaleContractResponse>> SearchResidentSaleContractsAsync(
        Guid userId,
        PropertySaleContractSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PropertySaleContractResponse>> GetResidentSaleContractAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<InstallmentScheduleItemResponse>> SearchResidentInstallmentsAsync(
        Guid userId,
        InstallmentSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<InstallmentScheduleItemResponse>> GetResidentInstallmentAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<RentContractResponse>> SearchResidentRentContractsAsync(
        Guid userId,
        RentContractSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RentContractResponse>> GetResidentRentContractAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<RentInvoiceResponse>> SearchResidentRentInvoicesAsync(
        Guid userId,
        RentInvoiceSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RentInvoiceResponse>> GetResidentRentInvoiceAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);
}
