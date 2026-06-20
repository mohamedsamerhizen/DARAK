using DARAK.Api.DTOs.Financial;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.BillingManagers)]
[Route("api/admin/financial-operations")]
public sealed class AdminFinancialOperationsController(IOverdueStatusService overdueStatusService)
    : ApiControllerBase
{
    [HttpPost("overdue-status/process")]
    public async Task<ActionResult<ProcessOverdueStatusResponse>> ProcessOverdueStatus(
        ProcessOverdueStatusRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await overdueStatusService.ProcessAsync(request, cancellationToken));
    }
}
