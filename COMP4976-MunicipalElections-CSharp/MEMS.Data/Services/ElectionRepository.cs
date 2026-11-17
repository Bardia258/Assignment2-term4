using System.ComponentModel.DataAnnotations;
using MEMS.Data.Entities;
using MEMS.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data.Services;

/// <summary>All reads and mutations require an explicit trusted scope; raw DbContext access is privileged.</summary>
public sealed class ElectionRepository(ApplicationDbContext db)
{
    private IQueryable<Municipality> Municipalities(MunicipalityScope scope) => db.Municipalities
        .Where(m => scope.IsSuperAdmin || m.MunicipalityId == scope.MunicipalityId);
    private IQueryable<Position> Positions(MunicipalityScope scope) => db.Positions
        .Where(p => scope.IsSuperAdmin || p.MunicipalityId == scope.MunicipalityId);
    private IQueryable<Candidate> Candidates(MunicipalityScope scope) => db.Candidates
        .Where(c => scope.IsSuperAdmin || c.MunicipalityId == scope.MunicipalityId);

    public Task<List<Municipality>> GetMunicipalitiesAsync(MunicipalityScope scope, CancellationToken ct = default) =>
        Municipalities(scope).AsNoTracking().OrderBy(m => m.Name).ToListAsync(ct);

    public Task<Municipality?> GetMunicipalityByIdAsync(MunicipalityScope scope, int id, CancellationToken ct = default) =>
        Municipalities(scope).AsNoTracking().SingleOrDefaultAsync(m => m.MunicipalityId == id, ct);

    public Task<Municipality?> GetMunicipalityBySlugAsync(MunicipalityScope scope, string slug, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        var normalized = slug.Trim().ToLowerInvariant();
        return Municipalities(scope).AsNoTracking().SingleOrDefaultAsync(m => m.Slug == normalized, ct);
    }

    public Task<List<Position>> GetPositionsAsync(MunicipalityScope scope, CancellationToken ct = default) =>
        Positions(scope).AsNoTracking().Include(p => p.Municipality).OrderBy(p => p.Name).ThenBy(p => p.PositionId).ToListAsync(ct);

    public Task<Position?> GetPositionAsync(MunicipalityScope scope, int id, CancellationToken ct = default) =>
        Positions(scope).AsNoTracking().Include(p => p.Municipality).SingleOrDefaultAsync(p => p.PositionId == id, ct);

    public Task<List<Candidate>> GetCandidatesAsync(MunicipalityScope scope, int? positionId = null,
        string? search = null, CancellationToken ct = default)
    {
        var query = Candidates(scope).AsNoTracking();
        if (positionId.HasValue) query = query.Where(c => c.PositionId == positionId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var normalized = term.ToUpperInvariant();
            query = query.Where(c => c.Name.ToUpper().Contains(normalized));
        }
        return query.Include(c => c.Position).Include(c => c.Municipality)
            .OrderBy(c => c.Name).ThenBy(c => c.CandidateId).ToListAsync(ct);
    }

    public Task<Candidate?> GetCandidateAsync(MunicipalityScope scope, int id, CancellationToken ct = default) =>
        Candidates(scope).AsNoTracking().Include(c => c.Position).Include(c => c.Municipality)
            .SingleOrDefaultAsync(c => c.CandidateId == id, ct);

    public async Task<Municipality> AddMunicipalityAsync(MunicipalityScope scope, Municipality input, CancellationToken ct = default)
    {
        DemandSuperAdmin(scope);
        var entity = new Municipality();
        await CheckMunicipalityDuplicateAsync(input, 0, ct);
        CopyMunicipality(input, entity);
        db.Municipalities.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<bool> UpdateMunicipalityAsync(MunicipalityScope scope, Municipality input, CancellationToken ct = default)
    {
        DemandSuperAdmin(scope);
        var stored = await Municipalities(scope).SingleOrDefaultAsync(m => m.MunicipalityId == input.MunicipalityId, ct);
        if (stored is null) return false;
        await CheckMunicipalityDuplicateAsync(input, input.MunicipalityId, ct);
        CopyMunicipality(input, stored);
        db.Entry(stored).Property(m => m.Revision).OriginalValue = input.Revision;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteMunicipalityAsync(MunicipalityScope scope, int id, string revision, CancellationToken ct = default)
    {
        DemandSuperAdmin(scope);
        var stored = await Municipalities(scope).SingleOrDefaultAsync(m => m.MunicipalityId == id, ct);
        if (stored is null) return false;
        if (await db.Positions.AnyAsync(p => p.MunicipalityId == id, ct)
            || await db.Candidates.AnyAsync(c => c.MunicipalityId == id, ct)
            || await db.Users.AnyAsync(u => u.MunicipalityId == id, ct))
            throw new RecordConflictException("Remove dependent election records and reassign administrators before deleting a municipality.");
        db.Entry(stored).Property(m => m.Revision).OriginalValue = revision;
        db.Municipalities.Remove(stored);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Position> AddPositionAsync(MunicipalityScope scope, Position input, CancellationToken ct = default)
    {
        scope.Demand(input.MunicipalityId);
        if (!await Municipalities(scope).AnyAsync(m => m.MunicipalityId == input.MunicipalityId, ct))
            throw new ValidationException("Municipality not found.");
        var entity = new Position { Name = input.Name, SeatsAvailable = input.SeatsAvailable, MunicipalityId = input.MunicipalityId };
        await CheckPositionDuplicateAsync(input, 0, ct);
        db.Positions.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<bool> UpdatePositionAsync(MunicipalityScope scope, Position input, CancellationToken ct = default)
    {
        scope.Demand(input.MunicipalityId);
        var stored = await Positions(scope).SingleOrDefaultAsync(p => p.PositionId == input.PositionId, ct);
        if (stored is null) return false;
        if (stored.MunicipalityId != input.MunicipalityId) throw new ValidationException("A position cannot move between municipalities.");
        await CheckPositionDuplicateAsync(input, input.PositionId, ct);
        stored.Name = input.Name;
        stored.SeatsAvailable = input.SeatsAvailable;
        db.Entry(stored).Property(p => p.Revision).OriginalValue = input.Revision;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeletePositionAsync(MunicipalityScope scope, int id, string revision, CancellationToken ct = default)
    {
        var stored = await Positions(scope).SingleOrDefaultAsync(p => p.PositionId == id, ct);
        if (stored is null) return false;
        if (await db.Candidates.AnyAsync(c => c.PositionId == id, ct))
            throw new RecordConflictException("Remove candidates before deleting a position.");
        db.Entry(stored).Property(p => p.Revision).OriginalValue = revision;
        db.Positions.Remove(stored);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Candidate> AddCandidateAsync(MunicipalityScope scope, Candidate input, CancellationToken ct = default)
    {
        scope.Demand(input.MunicipalityId);
        await DemandPositionAsync(input.PositionId, input.MunicipalityId, ct);
        // Never attach caller-supplied navigation graphs, timestamps, or internal seed markers.
        var entity = new Candidate { MunicipalityId = input.MunicipalityId };
        CopyCandidate(input, entity);
        db.Candidates.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<bool> UpdateCandidateAsync(MunicipalityScope scope, Candidate input, CancellationToken ct = default)
    {
        scope.Demand(input.MunicipalityId);
        var stored = await Candidates(scope).SingleOrDefaultAsync(c => c.CandidateId == input.CandidateId, ct);
        if (stored is null) return false;
        if (stored.MunicipalityId != input.MunicipalityId && !scope.IsSuperAdmin)
            throw new UnauthorizedAccessException("Moving candidates between municipalities requires Super Admin access.");
        await DemandPositionAsync(input.PositionId, input.MunicipalityId, ct);
        stored.MunicipalityId = input.MunicipalityId;
        CopyCandidate(input, stored);
        db.Entry(stored).Property(c => c.Revision).OriginalValue = input.Revision;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteCandidateAsync(MunicipalityScope scope, int id, string revision, CancellationToken ct = default)
    {
        var stored = await Candidates(scope).SingleOrDefaultAsync(c => c.CandidateId == id, ct);
        if (stored is null) return false;
        db.Entry(stored).Property(c => c.Revision).OriginalValue = revision;
        db.Candidates.Remove(stored);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task DemandPositionAsync(int positionId, int municipalityId, CancellationToken ct)
    {
        if (!await db.Positions.AnyAsync(p => p.PositionId == positionId && p.MunicipalityId == municipalityId, ct))
            throw new ValidationException("The selected position does not belong to this municipality.");
    }

    private async Task CheckMunicipalityDuplicateAsync(Municipality input, int id, CancellationToken ct)
    {
        var name = input.Name.Trim().ToUpperInvariant();
        var province = input.Province.Trim().ToUpperInvariant();
        // Only Super Admin mutations reach here; uniqueness spans the full municipality directory.
        if (await db.Municipalities.IgnoreQueryFilters().AnyAsync(m => m.MunicipalityId != id
            && (m.Slug == input.Slug || (m.Name.ToUpper() == name && m.Province.ToUpper() == province)), ct))
            throw new RecordConflictException("A municipality with these unique values already exists.");
    }

    private async Task CheckPositionDuplicateAsync(Position input, int id, CancellationToken ct)
    {
        var name = input.Name.Trim().ToUpperInvariant();
        if (await db.Positions.AnyAsync(p => p.PositionId != id && p.MunicipalityId == input.MunicipalityId
            && p.Name.ToUpper() == name, ct))
            throw new RecordConflictException("This municipality already has a position with that name.");
    }

    private static void DemandSuperAdmin(MunicipalityScope scope)
    {
        if (!scope.IsSuperAdmin) throw new UnauthorizedAccessException("Municipality management requires Super Admin access.");
    }

    private static void CopyMunicipality(Municipality input, Municipality stored)
    {
        stored.Name = input.Name; stored.Slug = input.Slug; stored.Province = input.Province;
        stored.ElectionDate = input.ElectionDate; stored.Description = input.Description; stored.LogoFileName = input.LogoFileName;
    }

    private static void CopyCandidate(Candidate input, Candidate stored)
    {
        stored.Name = input.Name; stored.Profile = input.Profile; stored.PositionId = input.PositionId;
        stored.PhotoFileName = input.PhotoFileName; stored.Website = input.Website; stored.Email = input.Email;
    }
}
