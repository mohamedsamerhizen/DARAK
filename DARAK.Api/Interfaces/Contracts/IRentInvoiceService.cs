using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Rents;

namespace DARAK.Api.Interfaces;

public interface IRentInvoiceService
{
    Task<ServiceResult<RentInvoiceResponse>> GenerateRentInvoiceAsync(GenerateRentInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<GenerateMonthlyRentInvoicesResponse>> GenerateMonthlyRentInvoicesAsync(GenerateMonthlyRentInvoicesRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<RentInvoiceResponse>> SearchRentInvoicesAsync(RentInvoiceSearchQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<RentInvoiceResponse>> GetRentInvoiceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<RentInvoiceResponse>> CancelRentInvoiceAsync(Guid id, CancelRentInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<RentInvoiceResponse>> RecalculateRentInvoiceStatusAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<RentInvoiceResponse>> SearchResidentRentInvoicesAsync(Guid userId, RentInvoiceSearchQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<RentInvoiceResponse>> GetResidentRentInvoiceAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
