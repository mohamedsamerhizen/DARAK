using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Reports;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.ManagementReportReaders)]
[Route("api/admin/reports")]
public sealed class AdminManagementReportsController(
    IManagementReportService managementReportService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet("financial")]
    public async Task<ActionResult<FinancialManagementReportResponse>> GetFinancialReport([FromQuery] ManagementReportQuery query, CancellationToken cancellationToken)
    {
        return ToActionResult(await managementReportService.GetFinancialReportAsync(query, cancellationToken));
    }

    [HttpGet("occupancy")]
    public async Task<ActionResult<OccupancyManagementReportResponse>> GetOccupancyReport([FromQuery] ManagementReportQuery query, CancellationToken cancellationToken)
    {
        return ToActionResult(await managementReportService.GetOccupancyReportAsync(query, cancellationToken));
    }

    [HttpGet("maintenance")]
    public async Task<ActionResult<MaintenanceManagementReportResponse>> GetMaintenanceReport([FromQuery] ManagementReportQuery query, CancellationToken cancellationToken)
    {
        return ToActionResult(await managementReportService.GetMaintenanceReportAsync(query, cancellationToken));
    }

    [HttpGet("support")]
    public async Task<ActionResult<SupportManagementReportResponse>> GetSupportReport([FromQuery] ManagementReportQuery query, CancellationToken cancellationToken)
    {
        return ToActionResult(await managementReportService.GetSupportReportAsync(query, cancellationToken));
    }

    [HttpGet("risk-audit")]
    public async Task<ActionResult<RiskAuditManagementReportResponse>> GetRiskAuditReport([FromQuery] ManagementReportQuery query, CancellationToken cancellationToken)
    {
        return ToActionResult(await managementReportService.GetRiskAuditReportAsync(query, cancellationToken));
    }

    [HttpGet("saved")]
    public async Task<ActionResult<PagedResult<SavedReportResponse>>> SearchSavedReports([FromQuery] SavedReportSearchQuery query, CancellationToken cancellationToken)
    {
        return ToActionResult(await managementReportService.SearchSavedReportsAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.ManagementReportManagers)]
    [HttpPost("saved")]
    public async Task<ActionResult<SavedReportResponse>> CreateSavedReport(CreateSavedReportRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await managementReportService.CreateSavedReportAsync(currentUserService.UserId, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.ManagementReportManagers)]
    [HttpPost("exports")]
    public async Task<ActionResult<ReportExportJobResponse>> CreateExportJob(CreateReportExportJobRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await managementReportService.CreateExportJobAsync(currentUserService.UserId, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.ManagementReportManagers)]
    [HttpPost("exports/{id:guid}/complete")]
    public async Task<ActionResult<ReportExportJobResponse>> CompleteExportJob(Guid id, CompleteReportExportJobRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await managementReportService.CompleteExportJobAsync(currentUserService.UserId, id, request, cancellationToken));
    }
}
