using System.ComponentModel.DataAnnotations;
using MEMS.Api.Infrastructure;
using MEMS.Api.Security;
using MEMS.Data.Entities;
using MEMS.Data.Services;
using MEMS.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace MEMS.Api.Controllers;

[ApiController, Route("api/municipalities"), ServiceFilter(typeof(TenantActionFilter))]
public sealed class MunicipalitiesController(ElectionRepository repository, TenantAccess tenant, DirectoryMapper map) : ControllerBase
{
    [HttpGet, ProducesResponseType<List<MunicipalityResponse>>(200)]
    public async Task<IActionResult> List([FromQuery] MunicipalityQuery query, CancellationToken ct) =>
        Ok((await repository.GetMunicipalitiesAsync(tenant.ReadScope, ct)).Select(map.Municipality));

    [HttpGet("{id:int}"), ProducesResponseType<MunicipalityResponse>(200)]
    public async Task<IActionResult> Get([Range(1, int.MaxValue)] int id, [FromQuery] MunicipalityQuery query, CancellationToken ct)
    {
        var value = await repository.GetMunicipalityByIdAsync(tenant.ReadScope, id, ct);
        return value is null ? NotFound() : Ok(map.Municipality(value));
    }

    [HttpGet("slug/{slug}"), ProducesResponseType<MunicipalityResponse>(200)]
    public async Task<IActionResult> BySlug([RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$"), StringLength(100)] string slug, CancellationToken ct)
    {
        var value = await repository.GetMunicipalityBySlugAsync(tenant.ReadScope, slug, ct);
        return value is null ? NotFound() : Ok(map.Municipality(value));
    }

    [Authorize, HttpPost, ProducesResponseType<MunicipalityResponse>(201)]
    public async Task<IActionResult> Create(MunicipalityRequest request, CancellationToken ct)
    {
        var value = await repository.AddMunicipalityAsync(tenant.Scope, Entity(request), ct);
        return CreatedAtAction(nameof(Get), new { id = value.MunicipalityId }, map.Municipality(value));
    }

    [Authorize, HttpPut("{id:int}"), ProducesResponseType<MunicipalityResponse>(200)]
    public async Task<IActionResult> Update([Range(1, int.MaxValue)] int id, MunicipalityUpdateRequest request, CancellationToken ct)
    {
        var value = Entity(request); value.MunicipalityId = id; value.Revision = request.Revision;
        if (!await repository.UpdateMunicipalityAsync(tenant.Scope, value, ct)) return NotFound();
        return Ok(map.Municipality((await repository.GetMunicipalityByIdAsync(tenant.ReadScope, id, ct))!));
    }

    [Authorize, HttpDelete("{id:int}"), ProducesResponseType(204)]
    public async Task<IActionResult> Delete([Range(1, int.MaxValue)] int id,
        [FromQuery, Required, RegularExpression("^[a-f0-9]{32}$")] string revision, CancellationToken ct) =>
        await repository.DeleteMunicipalityAsync(tenant.Scope, id, revision, ct) ? NoContent() : NotFound();

    private static Municipality Entity(MunicipalityRequest r) => new()
    {
        Name = r.Name.Trim(), Slug = r.Slug, Province = r.Province.Trim(),
        ElectionDate = r.ElectionDate!.Value.ToDateTime(TimeOnly.MinValue),
        Description = r.Description?.Trim() ?? "", LogoFileName = r.LogoFileName
    };
}
