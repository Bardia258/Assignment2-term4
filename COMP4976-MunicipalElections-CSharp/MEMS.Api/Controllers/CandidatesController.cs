using System.ComponentModel.DataAnnotations;
using MEMS.Api.Infrastructure;
using MEMS.Api.Security;
using MEMS.Data.Entities;
using MEMS.Data.Services;
using MEMS.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace MEMS.Api.Controllers;

[ApiController, Route("api/candidates"), ServiceFilter(typeof(TenantActionFilter))]
public sealed class CandidatesController(ElectionRepository repository, TenantAccess tenant, DirectoryMapper map) : ControllerBase
{
    [HttpGet, HttpGet("search"), ProducesResponseType<List<CandidateResponse>>(200)]
    public async Task<IActionResult> List([FromQuery] CandidateQuery query, CancellationToken ct)
    {
        if (query.PositionId.HasValue && await repository.GetPositionAsync(tenant.ReadScope, query.PositionId.Value, ct) is null) return NotFound();
        return Ok((await repository.GetCandidatesAsync(tenant.ReadScope, query.PositionId, query.Name, ct)).Select(map.Candidate));
    }

    [HttpGet("{id:int}"), ProducesResponseType<CandidateResponse>(200)]
    public async Task<IActionResult> Get([Range(1, int.MaxValue)] int id, [FromQuery] MunicipalityQuery query, CancellationToken ct)
    {
        var value = await repository.GetCandidateAsync(tenant.ReadScope, id, ct);
        return value is null ? NotFound() : Ok(map.Candidate(value));
    }

    [Authorize, HttpPost, ProducesResponseType<CandidateResponse>(201)]
    public async Task<IActionResult> Create(CandidateRequest request, CancellationToken ct)
    {
        var value = await repository.AddCandidateAsync(tenant.Scope, Entity(request), ct);
        value = (await repository.GetCandidateAsync(tenant.ReadScope, value.CandidateId, ct))!;
        return CreatedAtAction(nameof(Get), new { id = value.CandidateId, municipalityId = value.MunicipalityId }, map.Candidate(value));
    }

    [Authorize, HttpPut("{id:int}"), ProducesResponseType<CandidateResponse>(200)]
    public async Task<IActionResult> Update([Range(1, int.MaxValue)] int id, CandidateUpdateRequest request, CancellationToken ct)
    {
        var value = Entity(request); value.CandidateId = id; value.Revision = request.Revision;
        if (!await repository.UpdateCandidateAsync(tenant.Scope, value, ct)) return NotFound();
        return Ok(map.Candidate((await repository.GetCandidateAsync(tenant.ReadScope, id, ct))!));
    }

    [Authorize, HttpDelete("{id:int}"), ProducesResponseType(204)]
    public async Task<IActionResult> Delete([Range(1, int.MaxValue)] int id,
        [FromQuery, Required, RegularExpression("^[a-f0-9]{32}$")] string revision, CancellationToken ct) =>
        await repository.DeleteCandidateAsync(tenant.Scope, id, revision, ct) ? NoContent() : NotFound();

    private static Candidate Entity(CandidateRequest r) => new()
    {
        Name = r.Name.Trim(), Profile = r.Profile.Trim(), MunicipalityId = r.MunicipalityId, PositionId = r.PositionId,
        PhotoFileName = r.PhotoFileName, Website = string.IsNullOrWhiteSpace(r.Website) ? null : r.Website.Trim(),
        Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim()
    };
}
