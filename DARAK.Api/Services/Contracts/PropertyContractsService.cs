using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.PropertySales;
using DARAK.Api.DTOs.Rents;
using DARAK.Api.Interfaces;

namespace DARAK.Api.Services;

public sealed class PropertyContractsService(
    IPropertySaleService propertySaleService,
    IRentContractService rentContractService,
    IRentInvoiceService rentInvoiceService)
    : IPropertyContractsService
{
    public Task<PagedResult<PropertySaleContractResponse>> SearchSaleContractsAsync(PropertySaleContractSearchQuery query, CancellationToken cancellationToken = default)
        => propertySaleService.SearchSaleContractsAsync(query, cancellationToken);

    public Task<ServiceResult<PropertySaleContractResponse>> GetSaleContractAsync(Guid id, CancellationToken cancellationToken = default)
        => propertySaleService.GetSaleContractAsync(id, cancellationToken);

    public Task<ServiceResult<PropertySaleContractResponse>> CreateCashSaleContractAsync(CreateCashSaleContractRequest request, CancellationToken cancellationToken = default)
        => propertySaleService.CreateCashSaleContractAsync(request, cancellationToken);

    public Task<ServiceResult<PropertySaleContractResponse>> CreateInstallmentSaleContractAsync(CreateInstallmentSaleContractRequest request, CancellationToken cancellationToken = default)
        => propertySaleService.CreateInstallmentSaleContractAsync(request, cancellationToken);

    public Task<ServiceResult<PropertySaleContractResponse>> CancelSaleContractAsync(Guid id, CancelSaleContractRequest request, CancellationToken cancellationToken = default)
        => propertySaleService.CancelSaleContractAsync(id, request, cancellationToken);

    public Task<PagedResult<InstallmentScheduleItemResponse>> SearchInstallmentsAsync(InstallmentSearchQuery query, CancellationToken cancellationToken = default)
        => propertySaleService.SearchInstallmentsAsync(query, cancellationToken);

    public Task<ServiceResult<InstallmentScheduleItemResponse>> GetInstallmentAsync(Guid id, CancellationToken cancellationToken = default)
        => propertySaleService.GetInstallmentAsync(id, cancellationToken);

    public Task<ServiceResult<InstallmentScheduleItemResponse>> RecalculateInstallmentStatusAsync(Guid id, CancellationToken cancellationToken = default)
        => propertySaleService.RecalculateInstallmentStatusAsync(id, cancellationToken);

    public Task<PagedResult<RentContractResponse>> SearchRentContractsAsync(RentContractSearchQuery query, CancellationToken cancellationToken = default)
        => rentContractService.SearchRentContractsAsync(query, cancellationToken);

    public Task<ServiceResult<RentContractResponse>> GetRentContractAsync(Guid id, CancellationToken cancellationToken = default)
        => rentContractService.GetRentContractAsync(id, cancellationToken);

    public Task<ServiceResult<RentContractResponse>> CreateRentContractAsync(CreateRentContractRequest request, CancellationToken cancellationToken = default)
        => rentContractService.CreateRentContractAsync(request, cancellationToken);

    public Task<ServiceResult<RentContractResponse>> TerminateRentContractAsync(Guid id, TerminateRentContractRequest request, CancellationToken cancellationToken = default)
        => rentContractService.TerminateRentContractAsync(id, request, cancellationToken);

    public Task<ServiceResult<RentInvoiceResponse>> GenerateRentInvoiceAsync(GenerateRentInvoiceRequest request, CancellationToken cancellationToken = default)
        => rentInvoiceService.GenerateRentInvoiceAsync(request, cancellationToken);

    public Task<ServiceResult<GenerateMonthlyRentInvoicesResponse>> GenerateMonthlyRentInvoicesAsync(GenerateMonthlyRentInvoicesRequest request, CancellationToken cancellationToken = default)
        => rentInvoiceService.GenerateMonthlyRentInvoicesAsync(request, cancellationToken);

    public Task<PagedResult<RentInvoiceResponse>> SearchRentInvoicesAsync(RentInvoiceSearchQuery query, CancellationToken cancellationToken = default)
        => rentInvoiceService.SearchRentInvoicesAsync(query, cancellationToken);

    public Task<ServiceResult<RentInvoiceResponse>> GetRentInvoiceAsync(Guid id, CancellationToken cancellationToken = default)
        => rentInvoiceService.GetRentInvoiceAsync(id, cancellationToken);

    public Task<ServiceResult<RentInvoiceResponse>> CancelRentInvoiceAsync(Guid id, CancelRentInvoiceRequest request, CancellationToken cancellationToken = default)
        => rentInvoiceService.CancelRentInvoiceAsync(id, request, cancellationToken);

    public Task<ServiceResult<RentInvoiceResponse>> RecalculateRentInvoiceStatusAsync(Guid id, CancellationToken cancellationToken = default)
        => rentInvoiceService.RecalculateRentInvoiceStatusAsync(id, cancellationToken);

    public Task<PagedResult<PropertySaleContractResponse>> SearchResidentSaleContractsAsync(Guid userId, PropertySaleContractSearchQuery query, CancellationToken cancellationToken = default)
        => propertySaleService.SearchResidentSaleContractsAsync(userId, query, cancellationToken);

    public Task<ServiceResult<PropertySaleContractResponse>> GetResidentSaleContractAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
        => propertySaleService.GetResidentSaleContractAsync(userId, id, cancellationToken);

    public Task<PagedResult<InstallmentScheduleItemResponse>> SearchResidentInstallmentsAsync(Guid userId, InstallmentSearchQuery query, CancellationToken cancellationToken = default)
        => propertySaleService.SearchResidentInstallmentsAsync(userId, query, cancellationToken);

    public Task<ServiceResult<InstallmentScheduleItemResponse>> GetResidentInstallmentAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
        => propertySaleService.GetResidentInstallmentAsync(userId, id, cancellationToken);

    public Task<PagedResult<RentContractResponse>> SearchResidentRentContractsAsync(Guid userId, RentContractSearchQuery query, CancellationToken cancellationToken = default)
        => rentContractService.SearchResidentRentContractsAsync(userId, query, cancellationToken);

    public Task<ServiceResult<RentContractResponse>> GetResidentRentContractAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
        => rentContractService.GetResidentRentContractAsync(userId, id, cancellationToken);

    public Task<PagedResult<RentInvoiceResponse>> SearchResidentRentInvoicesAsync(Guid userId, RentInvoiceSearchQuery query, CancellationToken cancellationToken = default)
        => rentInvoiceService.SearchResidentRentInvoicesAsync(userId, query, cancellationToken);

    public Task<ServiceResult<RentInvoiceResponse>> GetResidentRentInvoiceAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
        => rentInvoiceService.GetResidentRentInvoiceAsync(userId, id, cancellationToken);
}
