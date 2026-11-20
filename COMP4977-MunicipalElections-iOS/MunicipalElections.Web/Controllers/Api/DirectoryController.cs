using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunicipalElections.Web.Data;
using MunicipalElections.Web.Models;
using MunicipalElections.Web.Services;

namespace MunicipalElections.Web.Controllers.Api;

public sealed record MunicipalityResponse(int Id, string Name, string Province, DateTime ElectionDate,
    string Description, string? LogoUrl);
public sealed record PositionResponse(int Id, string Name, int SeatsAvailable, int MunicipalityId, string MunicipalityName);
public sealed record CandidateResponse(int Id, string Name, string Profile, string? PhotoUrl,
    string? Website, string? Email, DateTime DateCreated, int MunicipalityId, string MunicipalityName,
    int PositionId, string PositionTitle, MunicipalityResponse Municipality, PositionResponse Position);

public sealed class DirectoryResponses(IImageStorage images)
{
    public MunicipalityResponse Municipality(Municipality m) => new(m.MunicipalityId, m.Name, m.Province,
        m.ElectionDate, m.Description, images.Url(m.LogoFileName, "logos"));
    public PositionResponse Position(Position p) => new(p.PositionId, p.Name, p.SeatsAvailable, p.MunicipalityId, p.Municipality.Name);
    public CandidateResponse Candidate(Candidate c) => new(c.CandidateId, c.Name, c.Profile,
        images.Url(c.PhotoFileName, "candidates"), c.Website, c.Email, c.DateCreated, c.MunicipalityId,
        c.Municipality.Name, c.PositionId, c.Position.Name, Municipality(c.Municipality),
        new PositionResponse(c.PositionId, c.Position.Name, c.Position.SeatsAvailable, c.MunicipalityId, c.Municipality.Name));
}

[ApiController, AllowAnonymous, Route("api/municipalities")]
public sealed class MunicipalitiesApiController(ApplicationDbContext db, IImageStorage images) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] MunicipalityDirectoryQuery filter, CancellationToken cancellationToken)
    {
        var (municipalityId, error) = await MunicipalityDirectoryFilter.ResolveAsync(db, filter, cancellationToken, Request.Query);
        if (error is not null) return error;
        return Ok((await db.Municipalities.AsNoTracking()
            .Where(m => municipalityId == null || m.MunicipalityId == municipalityId)
            .OrderBy(m => m.Name).ToListAsync(cancellationToken)).Select(new DirectoryResponses(images).Municipality));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details([System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int id,
        [FromQuery] MunicipalityDirectoryQuery filter, CancellationToken cancellationToken)
    {
        var (municipalityId, error) = await MunicipalityDirectoryFilter.ResolveAsync(db, filter, cancellationToken, Request.Query);
        if (error is not null) return error;
        var municipality = await db.Municipalities.AsNoTracking().SingleOrDefaultAsync(m => m.MunicipalityId == id
            && (municipalityId == null || m.MunicipalityId == municipalityId), cancellationToken);
        return municipality is null ? NotFound() : Ok(new DirectoryResponses(images).Municipality(municipality));
    }
}

[ApiController, AllowAnonymous, Route("api/positions")]
public sealed class PositionsApiController(ApplicationDbContext db, IImageStorage images) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] MunicipalityDirectoryQuery filter, CancellationToken cancellationToken)
    {
        var (municipalityId, error) = await MunicipalityDirectoryFilter.ResolveAsync(db, filter, cancellationToken, Request.Query);
        if (error is not null) return error;
        return Ok((await db.Positions.AsNoTracking().Include(p => p.Municipality)
            .Where(p => municipalityId == null || p.MunicipalityId == municipalityId)
            .OrderBy(p => p.Municipality.Name).ThenBy(p => p.Name).ToListAsync(cancellationToken))
            .Select(new DirectoryResponses(images).Position));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details([System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int id,
        [FromQuery] MunicipalityDirectoryQuery filter, CancellationToken cancellationToken)
    {
        var (municipalityId, error) = await MunicipalityDirectoryFilter.ResolveAsync(db, filter, cancellationToken, Request.Query);
        if (error is not null) return error;
        var position = await db.Positions.AsNoTracking().Include(p => p.Municipality)
            .SingleOrDefaultAsync(p => p.PositionId == id && (municipalityId == null || p.MunicipalityId == municipalityId), cancellationToken);
        return position is null ? NotFound() : Ok(new DirectoryResponses(images).Position(position));
    }
}

[ApiController, AllowAnonymous, Route("api/candidates")]
public sealed class CandidatesApiController(ApplicationDbContext db, IImageStorage images) : ControllerBase
{
    [HttpGet, HttpGet("search")]
    public async Task<IActionResult> List([FromQuery] CandidateDirectoryQuery filter, CancellationToken cancellationToken)
    {
        var (municipalityId, error) = await MunicipalityDirectoryFilter.ResolveAsync(db, filter, cancellationToken, Request.Query);
        if (error is not null) return error;
        var name = filter.Name;
        var positionId = filter.PositionId;
        if (positionId.HasValue && !await db.Positions.AnyAsync(p => p.PositionId == positionId
            && (municipalityId == null || p.MunicipalityId == municipalityId), cancellationToken))
            return MunicipalityDirectoryFilter.Error(404, "Position not found for the requested municipality.");
        var query = db.Candidates.AsNoTracking().Include(c => c.Municipality).Include(c => c.Position).AsQueryable();
        if (!string.IsNullOrWhiteSpace(name))
        {
            var term = name.Trim().ToUpperInvariant();
            query = query.Where(c => c.Name.ToUpper().Contains(term));
        }
        if (municipalityId.HasValue) query = query.Where(c => c.MunicipalityId == municipalityId);
        if (positionId.HasValue) query = query.Where(c => c.PositionId == positionId);
        return Ok((await query.OrderBy(c => c.Name).ThenBy(c => c.CandidateId).ToListAsync(cancellationToken))
            .Select(new DirectoryResponses(images).Candidate));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details([System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int id,
        [FromQuery] MunicipalityDirectoryQuery filter, CancellationToken cancellationToken)
    {
        var (municipalityId, error) = await MunicipalityDirectoryFilter.ResolveAsync(db, filter, cancellationToken, Request.Query);
        if (error is not null) return error;
        var candidate = await db.Candidates.AsNoTracking().Include(c => c.Municipality).Include(c => c.Position)
            .SingleOrDefaultAsync(c => c.CandidateId == id && (municipalityId == null || c.MunicipalityId == municipalityId), cancellationToken);
        return candidate is null ? NotFound() : Ok(new DirectoryResponses(images).Candidate(candidate));
    }
}
