using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Compounds;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.StructureReaders)]
[Route("api/admin/compounds")]
public sealed class AdminCompoundsController(ICompoundStructureService compoundStructureService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CompoundResponse>>> Search(
        [FromQuery] CompoundSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await compoundStructureService.SearchCompoundsAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompoundResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await compoundStructureService.GetCompoundAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpPost]
    public async Task<ActionResult<CompoundResponse>> Create(
        CreateCompoundRequest request,
        CancellationToken cancellationToken)
    {
        var result = await compoundStructureService.CreateCompoundAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CompoundResponse>> Update(
        Guid id,
        UpdateCompoundRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await compoundStructureService.UpdateCompoundAsync(id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.StructureAdministrators)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToNoContentResult(await compoundStructureService.DeactivateCompoundAsync(id, cancellationToken));
    }
}

