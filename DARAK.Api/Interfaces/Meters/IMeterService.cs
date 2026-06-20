using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Meters;

namespace DARAK.Api.Interfaces;

public interface IMeterService
{
    Task<PagedResult<MeterResponse>> SearchMetersAsync(
        MeterSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeterResponse>> GetMeterAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeterResponse>> CreateMeterAsync(
        CreateMeterRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeterResponse>> UpdateMeterAsync(
        Guid id,
        UpdateMeterRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> DeactivateMeterAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<MeterReadingResponse>> SearchMeterReadingsAsync(
        MeterReadingSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeterReadingResponse>> GetMeterReadingAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeterReadingResponse>> CreateMeterReadingAsync(
        CreateMeterReadingRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeterReadingResponse>> UpdateMeterReadingAsync(
        Guid id,
        UpdateMeterReadingRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<MeterReadingResponse>> SearchResidentMeterReadingsAsync(
        Guid userId,
        MeterReadingSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeterReadingResponse>> GetResidentMeterReadingAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeterReadingResponse>> GenerateBillLineFromReadingAsync(
        Guid id,
        GenerateBillLineFromReadingRequest request,
        CancellationToken cancellationToken = default);
}
