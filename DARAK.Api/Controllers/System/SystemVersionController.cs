using DARAK.Api.DTOs.System;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/system")]
public sealed class SystemVersionController(ISystemAdministrationService systemAdministrationService) : ControllerBase
{
    [HttpGet("version")]
    public ActionResult<SystemVersionResponse> GetVersion()
    {
        return Ok(systemAdministrationService.GetVersion());
    }
}
