using DARAK.Api.DTOs.AdminPortal;
using DARAK.Api.DTOs.Analytics;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.DTOs.Financial;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.StructureReaders)]
[Route("api/admin/dashboard")]
public sealed class AdminDashboardController(
    IAdminPortalService adminPortalService,
    IAnalyticsService analyticsService,
    ICurrentUserService currentUserService,
    IActivityTimelineService activityTimelineService,
    IResidentFinancialHealthService residentFinancialHealthService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminDashboardResponse>> GetDashboard(
        [FromQuery] AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await adminPortalService.GetDashboardAsync(query, cancellationToken));
    }

    [HttpGet("overview/units")]
    public async Task<ActionResult<AdminUnitsOverviewResponse>> GetUnitsOverview(
        [FromQuery] AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await adminPortalService.GetUnitsOverviewAsync(query, cancellationToken));
    }

    [HttpGet("overview/debt")]
    public async Task<ActionResult<AdminDebtOverviewResponse>> GetDebtOverview(
        [FromQuery] AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await adminPortalService.GetDebtOverviewAsync(query, cancellationToken));
    }

    [HttpGet("overview/revenue")]
    public async Task<ActionResult<AdminRevenueOverviewResponse>> GetRevenueOverview(
        [FromQuery] AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await adminPortalService.GetRevenueOverviewAsync(query, cancellationToken));
    }

    [HttpGet("overview/occupancy")]
    public async Task<ActionResult<AdminOccupancyOverviewResponse>> GetOccupancyOverview(
        [FromQuery] AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await adminPortalService.GetOccupancyOverviewAsync(query, cancellationToken));
    }

    [HttpGet("overview/billing")]
    public async Task<ActionResult<AdminBillingOverviewResponse>> GetBillingOverview(
        [FromQuery] AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await adminPortalService.GetBillingOverviewAsync(query, cancellationToken));
    }

    [HttpGet("overview/payments")]
    public async Task<ActionResult<AdminPaymentsOverviewResponse>> GetPaymentsOverview(
        [FromQuery] AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await adminPortalService.GetPaymentsOverviewAsync(query, cancellationToken));
    }

    [HttpGet("overview/contracts")]
    public async Task<ActionResult<AdminContractsOverviewResponse>> GetContractsOverview(
        [FromQuery] AdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await adminPortalService.GetContractsOverviewAsync(query, cancellationToken));
    }




    [HttpGet("financial-health")]
    public async Task<ActionResult<FinancialHealthDashboardSummaryResponse>> GetFinancialHealthSummary(
        [FromQuery] FinancialHealthDashboardQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentFinancialHealthService.GetDashboardSummaryAsync(
            currentUserService.UserId,
            query,
            cancellationToken));
    }

    [HttpGet("recent-activity")]
    public async Task<ActionResult<PagedResult<ActivityEventResponse>>> GetRecentActivity(
        [FromQuery] ActivityTimelineQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await activityTimelineService.SearchRecentActivityAsync(
            query,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpGet("analytics/summary")]
    public async Task<ActionResult<AdminDashboardSummaryResponse>> GetAnalyticsSummary(
        [FromQuery] DateRangeQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await analyticsService.GetAdminDashboardSummaryAsync(
            currentUserService.UserId,
            query,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpGet("analytics/financial")]
    public async Task<ActionResult<FinancialReportResponse>> GetFinancialReport(
        [FromQuery] DateRangeQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await analyticsService.GetFinancialReportAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpGet("analytics/maintenance-operations")]
    public async Task<ActionResult<MaintenanceOperationsReportResponse>> GetMaintenanceOperationsReport(
        [FromQuery] DateRangeQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await analyticsService.GetMaintenanceOperationsReportAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpGet("analytics/community")]
    public async Task<ActionResult<CommunityReportResponse>> GetCommunityReport(
        [FromQuery] DateRangeQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await analyticsService.GetCommunityReportAsync(query, cancellationToken));
    }


    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpGet("analytics/visitors")]
    public async Task<ActionResult<VisitorsReportResponse>> GetVisitorsReport(
        [FromQuery] DateRangeQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await analyticsService.GetVisitorsReportAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpGet("analytics/documents")]
    public async Task<ActionResult<DocumentsReportResponse>> GetDocumentsReport(
        [FromQuery] DateRangeQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await analyticsService.GetDocumentsReportAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpGet("analytics/operations")]
    public async Task<ActionResult<OperationsReportResponse>> GetOperationsReport(
        [FromQuery] DateRangeQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await analyticsService.GetOperationsReportAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpGet("analytics/trends/payments")]
    public async Task<ActionResult<IReadOnlyCollection<ChartPointResponse>>> GetPaymentsTrend(
        [FromQuery] DateRangeQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await analyticsService.GetPaymentsTrendAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpGet("analytics/trends/maintenance")]
    public async Task<ActionResult<IReadOnlyCollection<ChartPointResponse>>> GetMaintenanceTrend(
        [FromQuery] DateRangeQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await analyticsService.GetMaintenanceTrendAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpGet("analytics/trends/work-orders")]
    public async Task<ActionResult<IReadOnlyCollection<ChartPointResponse>>> GetWorkOrdersTrend(
        [FromQuery] DateRangeQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await analyticsService.GetWorkOrdersTrendAsync(query, cancellationToken));
    }
}
