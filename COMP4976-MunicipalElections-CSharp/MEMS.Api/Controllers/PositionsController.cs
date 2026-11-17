using System.ComponentModel.DataAnnotations;
using MEMS.Api.Infrastructure;
using MEMS.Api.Security;
using MEMS.Data.Entities;
using MEMS.Data.Services;
using MEMS.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace MEMS.Api.Controllers;

[ApiController, Route("api/positions"), ServiceFilter(typeof(TenantActionFilter))]
public sealed class PositionsController(ElectionRepository repository, TenantAccess tenant, DirectoryMapper map) : ControllerBase
{
    [HttpGet, ProducesResponseType<List<PositionResponse>>(200)]
    public async Task<IActionResult> List([FromQuery] MunicipalityQuery query, CancellationToken ct) =>
        Ok((await repository.GetPositionsAsync(tenant.ReadScope, ct)).Select(map.Position));

    [HttpGet("{id:int}"), ProducesResponseType<PositionResponse>(200)]
    public async Task<IActionResult> Get([Range(1, int.MaxValue)] int id, [FromQuery] MunicipalityQuery query, CancellationToken ct)
    {
        var value = await repository.GetPositionAsync(tenant.ReadScope, id, ct);
        return value is null ? NotFound() : Ok(map.Position(value));
    }

    [Authorize, HttpPost, ProducesResponseType<PositionResponse>(201)]
    public async Task<IActionResult> Create(PositionRequest request, CancellationToken ct)
    {
        var value = await repository.AddPositionAsync(tenant.Scope, Entity(request), ct);
        value = (await repository.GetPositionAsync(tenant.ReadScope, value.PositionId, ct))!;
        return CreatedAtAction(nameof(Get), new { id = value.PositionId, municipalityId = value.MunicipalityId }, map.Position(value));
    }

    [Authorize, HttpPut("{id:int}"), ProducesResponseType<PositionResponse>(200)]
    public async Task<IActionResult> Update([Range(1, int.MaxValue)] int id, PositionUpdateRequest request, CancellationToken ct)
    {
        var value = Entity(request); value.PositionId = id; value.Revision = request.Revision;
        if (!await repository.UpdatePositionAsync(tenant.Scope, value, ct)) return NotFound();
        return Ok(map.Position((await repository.GetPositionAsync(tenant.ReadScope, id, ct))!));
    }

    [Authorize, HttpDelete("{id:int}"), ProducesResponseType(204)]
    public async Task<IActionResult> Delete([Range(1, int.MaxValue)] int id,
        [FromQuery, Required, RegularExpression("^[a-f0-9]{32}$")] string revision, CancellationToken ct) =>
        await repository.DeletePositionAsync(tenant.Scope, id, revision, ct) ? NoContent() : NotFound();

    private static Position Entity(PositionRequest r) => new()
    { Name = r.Name.Trim(), SeatsAvailable = r.SeatsAvailable, MunicipalityId = r.MunicipalityId };
}
