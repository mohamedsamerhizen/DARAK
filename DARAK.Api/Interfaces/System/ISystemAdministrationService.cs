using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;

namespace DARAK.Api.Interfaces;

public interface ISystemAdministrationService
{
    SystemVersionResponse GetVersion();

    Task<ServiceResult<PagedResult<SystemSettingResponse>>> SearchSettingsAsync(SystemSettingSearchQuery query, CancellationToken cancellationToken = default);

    Task<ServiceResult<SystemSettingResponse>> UpsertSettingAsync(Guid? currentUserId, UpsertSystemSettingRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<LicenseProfileResponse>> GetLicenseProfileAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<LicenseProfileResponse>> UpdateLicenseProfileAsync(Guid? currentUserId, UpdateLicenseProfileRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceModeResponse>> GetMaintenanceModeAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceModeResponse>> SetMaintenanceModeAsync(Guid? currentUserId, SetMaintenanceModeRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<DeploymentChecklistResponse>> GetDeploymentChecklistAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<SystemHealthDashboardResponse>> GetSystemHealthAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<BackgroundJobRunResponse>>> SearchBackgroundJobRunsAsync(BackgroundJobRunSearchQuery query, CancellationToken cancellationToken = default);

    Task<ServiceResult<BackgroundJobRunResponse>> StartBackgroundJobRunAsync(StartBackgroundJobRunRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<BackgroundJobRunResponse>> CompleteBackgroundJobRunAsync(Guid id, CompleteBackgroundJobRunRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<IntegrationFailureEventResponse>>> SearchIntegrationFailuresAsync(IntegrationFailureSearchQuery query, CancellationToken cancellationToken = default);

    Task<ServiceResult<IntegrationFailureEventResponse>> RecordIntegrationFailureAsync(RecordIntegrationFailureRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<IntegrationFailureEventResponse>> ResolveIntegrationFailureAsync(Guid? currentUserId, Guid id, ResolveIntegrationFailureRequest request, CancellationToken cancellationToken = default);
}
