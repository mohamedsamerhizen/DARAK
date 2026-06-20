using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Meters;

namespace DARAK.Api.Interfaces;

public interface ISmartMeterOperationsService
{
    Task<ServiceResult<SmartMeterOperationsSummaryResponse>> GetSummaryAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<SmartMeterDeviceResponse>> SearchDevicesAsync(
        SmartMeterDeviceQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SmartMeterDeviceResponse>> GetDeviceAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SmartMeterDeviceResponse>> RegisterDeviceAsync(
        RegisterSmartMeterDeviceRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SmartMeterDeviceResponse>> UpdateDeviceStatusAsync(
        Guid id,
        UpdateSmartMeterDeviceStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SmartMeterReadingIngestionResponse>> IngestReadingAsync(
        Guid deviceId,
        IngestSmartMeterReadingRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<SmartMeterReadingIngestionResponse>> SearchIngestionsAsync(
        SmartMeterIngestionQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<int>> RefreshDeviceHealthAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);
}
