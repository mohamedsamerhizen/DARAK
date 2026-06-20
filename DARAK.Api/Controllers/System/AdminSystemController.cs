using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.SystemReaders)]
[Route("api/admin/system")]
public sealed class AdminSystemController(
    ISystemAdministrationService systemAdministrationService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet("version")]
    public ActionResult<SystemVersionResponse> GetVersion()
    {
        return Ok(systemAdministrationService.GetVersion());
    }

    [HttpGet("settings")]
    public async Task<ActionResult<PagedResult<SystemSettingResponse>>> SearchSettings(
        [FromQuery] SystemSettingSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await systemAdministrationService.SearchSettingsAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.SystemManagers)]
    [HttpPut("settings")]
    public async Task<ActionResult<SystemSettingResponse>> UpsertSetting(
        UpsertSystemSettingRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await systemAdministrationService.UpsertSettingAsync(currentUserService.UserId, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.SuperAdmin)]
    [HttpGet("license")]
    public async Task<ActionResult<LicenseProfileResponse>> GetLicense(CancellationToken cancellationToken)
    {
        return ToActionResult(await systemAdministrationService.GetLicenseProfileAsync(cancellationToken));
    }

    [Authorize(Roles = RoleNames.SuperAdmin)]
    [HttpPut("license")]
    public async Task<ActionResult<LicenseProfileResponse>> UpdateLicense(
        UpdateLicenseProfileRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await systemAdministrationService.UpdateLicenseProfileAsync(currentUserService.UserId, request, cancellationToken));
    }

    [HttpGet("maintenance-mode")]
    public async Task<ActionResult<MaintenanceModeResponse>> GetMaintenanceMode(CancellationToken cancellationToken)
    {
        return ToActionResult(await systemAdministrationService.GetMaintenanceModeAsync(cancellationToken));
    }

    [Authorize(Roles = RoleNames.SuperAdmin)]
    [HttpPost("maintenance-mode")]
    public async Task<ActionResult<MaintenanceModeResponse>> SetMaintenanceMode(
        SetMaintenanceModeRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await systemAdministrationService.SetMaintenanceModeAsync(currentUserService.UserId, request, cancellationToken));
    }

    [HttpGet("deployment-checklist")]
    public async Task<ActionResult<DeploymentChecklistResponse>> GetDeploymentChecklist(CancellationToken cancellationToken)
    {
        return ToActionResult(await systemAdministrationService.GetDeploymentChecklistAsync(cancellationToken));
    }

    [HttpGet("health")]
    public async Task<ActionResult<SystemHealthDashboardResponse>> GetHealth(CancellationToken cancellationToken)
    {
        return ToActionResult(await systemAdministrationService.GetSystemHealthAsync(cancellationToken));
    }

    [Authorize(Roles = RoleNames.SuperAdmin)]
    [HttpGet("background-jobs")]
    public async Task<ActionResult<PagedResult<BackgroundJobRunResponse>>> SearchBackgroundJobs(
        [FromQuery] BackgroundJobRunSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await systemAdministrationService.SearchBackgroundJobRunsAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.SuperAdmin)]
    [HttpPost("background-jobs")]
    public async Task<ActionResult<BackgroundJobRunResponse>> StartBackgroundJob(
        StartBackgroundJobRunRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await systemAdministrationService.StartBackgroundJobRunAsync(request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.SuperAdmin)]
    [HttpPost("background-jobs/{id:guid}/complete")]
    public async Task<ActionResult<BackgroundJobRunResponse>> CompleteBackgroundJob(
        Guid id,
        CompleteBackgroundJobRunRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await systemAdministrationService.CompleteBackgroundJobRunAsync(id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.SuperAdmin)]
    [HttpGet("integration-failures")]
    public async Task<ActionResult<PagedResult<IntegrationFailureEventResponse>>> SearchIntegrationFailures(
        [FromQuery] IntegrationFailureSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await systemAdministrationService.SearchIntegrationFailuresAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.SuperAdmin)]
    [HttpPost("integration-failures")]
    public async Task<ActionResult<IntegrationFailureEventResponse>> RecordIntegrationFailure(
        RecordIntegrationFailureRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await systemAdministrationService.RecordIntegrationFailureAsync(request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.SuperAdmin)]
    [HttpPost("integration-failures/{id:guid}/resolve")]
    public async Task<ActionResult<IntegrationFailureEventResponse>> ResolveIntegrationFailure(
        Guid id,
        ResolveIntegrationFailureRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await systemAdministrationService.ResolveIntegrationFailureAsync(currentUserService.UserId, id, request, cancellationToken));
    }
}
