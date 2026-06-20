using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Meters;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.DTOs.PropertySales;
using DARAK.Api.DTOs.ResidentPortal;
using DARAK.Api.DTOs.Rents;
using DARAK.Api.DTOs.UtilityBills;

namespace DARAK.Api.Interfaces;

public interface IResidentPortalService
{
    Task<ServiceResult<ResidentDashboardResponse>> GetDashboardAsync(
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<ResidentPropertySummaryResponse>>> GetMyPropertiesAsync(
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<UtilityBillResponse>>> GetMyBillsAsync(
        UtilityBillSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<InstallmentScheduleItemResponse>>> GetMyInstallmentsAsync(
        InstallmentSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<RentInvoiceResponse>>> GetMyRentAsync(
        RentInvoiceSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<PaymentResponse>>> GetMyPaymentsAsync(
        PaymentSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<MeterReadingResponse>>> GetMyMeterReadingsAsync(
        MeterReadingSearchQuery query,
        CancellationToken cancellationToken = default);
}
