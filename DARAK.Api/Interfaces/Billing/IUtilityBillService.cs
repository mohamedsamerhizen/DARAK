using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.UtilityBills;

namespace DARAK.Api.Interfaces;

public interface IUtilityBillService
{
    Task<PagedResult<UtilityBillResponse>> SearchUtilityBillsAsync(UtilityBillSearchQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<UtilityBillResponse>> GetUtilityBillAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<UtilityBillResponse>> GenerateUtilityBillAsync(GenerateUtilityBillRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<GenerateMonthlyUtilityBillsResponse>> GenerateMonthlyUtilityBillsAsync(GenerateMonthlyUtilityBillsRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<UtilityBillResponse>> UpdateUtilityBillAsync(Guid id, UpdateUtilityBillRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<UtilityBillResponse>> CancelUtilityBillAsync(Guid id, CancelUtilityBillRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<UtilityBillResponse>> RecalculateUtilityBillStatusAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<UtilityBillResponse>> SearchResidentBillsAsync(Guid userId, UtilityBillSearchQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<UtilityBillResponse>> GetResidentBillAsync(Guid userId, Guid billId, CancellationToken cancellationToken = default);
}
