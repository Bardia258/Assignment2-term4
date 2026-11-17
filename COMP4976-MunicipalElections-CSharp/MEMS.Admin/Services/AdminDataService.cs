using System.ComponentModel.DataAnnotations;
using MEMS.Admin.Models;
using MEMS.Admin.Security;
using MEMS.Data;
using MEMS.Data.Entities;
using MEMS.Data.Services;
using MEMS.Data.Tenancy;
using MEMS.Shared.Contracts;
using MEMS.Shared.Tenancy;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Admin.Services;

/// <summary>Fresh context and current Identity verification for each circuit operation. Components contain no persistence rules.</summary>
public sealed class AdminDataService(AuthenticationStateProvider authentication, IServiceScopeFactory scopes,
    AdminPrincipalValidator validator, AdminImageStore images)
{
    private sealed record Operation(ApplicationDbContext Db, ElectionRepository Repository,
        AdminSession Session, MunicipalityScope ReadScope);

    private async Task<T> RunAsync<T>(Func<Operation, Task<T>> operation, bool superOnly = false,
        int? municipalityId = null, bool allowSuperTransfer = false)
    {
        var principal = (await authentication.GetAuthenticationStateAsync()).User;
        await using var scope = scopes.CreateAsyncScope();
        var session = await validator.ValidateInScopeAsync(principal, scope.ServiceProvider)
            ?? throw new UnauthorizedAccessException("Your session is no longer valid. Sign in again.");
        if (superOnly && !session.IsSuperAdmin) throw new UnauthorizedAccessException("Only Super Admin can manage municipality settings.");
        if (municipalityId.HasValue) session.Scope.Demand(municipalityId.Value);
        var readScope = municipalityId.HasValue && !(allowSuperTransfer && session.IsSuperAdmin)
            ? MunicipalityScope.ForMunicipality(municipalityId.Value) : session.Scope;
        var boundary = scope.ServiceProvider.GetRequiredService<TenantQueryContext>();
        if (readScope.IsSuperAdmin) boundary.AllowAll(); else boundary.RestrictTo(readScope.MunicipalityId!.Value);
        return await operation(new Operation(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<ElectionRepository>(), session, readScope));
    }

    public Task<AdminSession> SessionAsync() => RunAsync(op => Task.FromResult(op.Session));
    public Task<DashboardCounts> DashboardAsync() => RunAsync(async op => new DashboardCounts(
        await op.Db.Municipalities.CountAsync(), await op.Db.Positions.CountAsync(), await op.Db.Candidates.CountAsync()));
    public Task<List<MunicipalityResponse>> MunicipalitiesAsync(int? municipalityId = null) =>
        RunAsync(async op => (await op.Repository.GetMunicipalitiesAsync(op.ReadScope)).Select(Municipality).ToList(), municipalityId: municipalityId);
    public Task<MunicipalityResponse> MunicipalityAsync(int id, bool requireSuper = false) =>
        RunAsync(async op => Municipality(await op.Repository.GetMunicipalityByIdAsync(op.ReadScope, id)
            ?? throw new KeyNotFoundException("Municipality not found in your permitted scope.")), requireSuper, id);
    public Task<List<PositionResponse>> PositionsAsync(int? municipalityId = null) =>
        RunAsync(async op => (await op.Repository.GetPositionsAsync(op.ReadScope)).Select(Position).ToList(), municipalityId: municipalityId);
    public Task<PositionResponse> PositionAsync(int id) => RunAsync(async op =>
        Position(await op.Repository.GetPositionAsync(op.ReadScope, id) ?? throw new KeyNotFoundException("Position not found in your permitted scope.")));
    public Task<List<CandidateResponse>> CandidatesAsync(int? municipalityId = null, int? positionId = null, string? search = null) =>
        RunAsync(async op =>
        {
            if (search?.Length > 100) throw new ValidationException("Candidate search must be at most 100 characters.");
            if (positionId.HasValue && await op.Repository.GetPositionAsync(op.ReadScope, positionId.Value) is null)
                throw new ValidationException("Choose a position belonging to the selected municipality.");
            return (await op.Repository.GetCandidatesAsync(op.ReadScope, positionId, search)).Select(Candidate).ToList();
        }, municipalityId: municipalityId);
    public Task<CandidateResponse> CandidateAsync(int id) => RunAsync(async op =>
        Candidate(await op.Repository.GetCandidateAsync(op.ReadScope, id) ?? throw new KeyNotFoundException("Candidate not found in your permitted scope.")));

    public Task<int> SaveMunicipalityAsync(int? id, MunicipalityForm input, string? revision, IBrowserFile? upload) =>
        RunAsync(async op =>
        {
            Validate(input);
            if (id.HasValue) RequireRevision(revision);
            var stored = id.HasValue ? await op.Repository.GetMunicipalityByIdAsync(op.ReadScope, id.Value)
                ?? throw new KeyNotFoundException("Municipality not found.") : null;
            var oldImage = stored?.LogoFileName;
            string? newImage = null;
            try
            {
                newImage = await UploadAsync(upload, "logos");
                var entity = new Municipality
                {
                    MunicipalityId = id ?? 0, Name = input.Name.Trim(), Slug = input.Slug, Province = input.Province.Trim(),
                    ElectionDate = input.ElectionDate!.Value.ToDateTime(TimeOnly.MinValue),
                    Description = input.Description?.Trim() ?? "", LogoFileName = newImage ?? oldImage,
                    Revision = revision ?? Guid.NewGuid().ToString("N")
                };
                var result = id.HasValue
                    ? await op.Repository.UpdateMunicipalityAsync(op.Session.Scope, entity)
                        ? id.Value : throw new KeyNotFoundException("Municipality not found.")
                    : (await op.Repository.AddMunicipalityAsync(op.Session.Scope, entity)).MunicipalityId;
                if (newImage is not null) await images.DeleteIfUnreferencedAsync(op.Db, oldImage, "logos");
                return result;
            }
            catch
            {
                if (newImage is not null) await images.DeleteIfUnreferencedAsync(op.Db, newImage, "logos");
                throw;
            }
        }, superOnly: true, municipalityId: id);

    public Task<int> SavePositionAsync(int? id, PositionRequest input, string? revision) =>
        RunAsync(async op =>
        {
            Validate(input);
            if (id.HasValue) RequireRevision(revision);
            var entity = new Position { PositionId = id ?? 0, Name = input.Name.Trim(), SeatsAvailable = input.SeatsAvailable,
                MunicipalityId = input.MunicipalityId, Revision = revision ?? Guid.NewGuid().ToString("N") };
            return id.HasValue
                ? await op.Repository.UpdatePositionAsync(op.Session.Scope, entity)
                    ? id.Value : throw new KeyNotFoundException("Position not found in your permitted scope.")
                : (await op.Repository.AddPositionAsync(op.Session.Scope, entity)).PositionId;
        }, municipalityId: input.MunicipalityId);

    public Task<int> SaveCandidateAsync(int? id, CandidateRequest input, string? revision, IBrowserFile? upload) =>
        RunAsync(async op =>
        {
            Validate(input);
            if (id.HasValue) RequireRevision(revision);
            var stored = id.HasValue ? await op.Repository.GetCandidateAsync(op.ReadScope, id.Value)
                ?? throw new KeyNotFoundException("Candidate not found in your permitted scope.") : null;
            if (!await op.Db.Positions.AnyAsync(p => p.PositionId == input.PositionId && p.MunicipalityId == input.MunicipalityId))
                throw new ValidationException("Choose a position belonging to the candidate's municipality.");
            var oldImage = stored?.PhotoFileName;
            string? newImage = null;
            try
            {
                newImage = await UploadAsync(upload, "candidates");
                var entity = new Candidate
                {
                    CandidateId = id ?? 0, Name = input.Name.Trim(), Profile = input.Profile.Trim(),
                    MunicipalityId = input.MunicipalityId, PositionId = input.PositionId,
                    Website = string.IsNullOrWhiteSpace(input.Website) ? null : input.Website.Trim(),
                    Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim(),
                    PhotoFileName = newImage ?? oldImage, Revision = revision ?? Guid.NewGuid().ToString("N")
                };
                var result = id.HasValue
                    ? await op.Repository.UpdateCandidateAsync(op.Session.Scope, entity)
                        ? id.Value : throw new KeyNotFoundException("Candidate not found.")
                    : (await op.Repository.AddCandidateAsync(op.Session.Scope, entity)).CandidateId;
                if (newImage is not null) await images.DeleteIfUnreferencedAsync(op.Db, oldImage, "candidates");
                return result;
            }
            catch
            {
                if (newImage is not null) await images.DeleteIfUnreferencedAsync(op.Db, newImage, "candidates");
                throw;
            }
        }, municipalityId: input.MunicipalityId, allowSuperTransfer: id.HasValue);

    public Task<bool> DeleteMunicipalityAsync(int id, string revision) => RunAsync(async op =>
    {
        RequireRevision(revision);
        var entity = await op.Repository.GetMunicipalityByIdAsync(op.ReadScope, id) ?? throw new KeyNotFoundException("Municipality not found.");
        if (!await op.Repository.DeleteMunicipalityAsync(op.Session.Scope, id, revision)) throw new KeyNotFoundException("Municipality not found.");
        await images.DeleteIfUnreferencedAsync(op.Db, entity.LogoFileName, "logos");
        return true;
    }, superOnly: true, municipalityId: id);

    public Task<bool> DeletePositionAsync(int id, string revision) => RunAsync(async op =>
    {
        RequireRevision(revision);
        return await op.Repository.DeletePositionAsync(op.Session.Scope, id, revision)
            ? true : throw new KeyNotFoundException("Position not found in your permitted scope.");
    });

    public Task<bool> DeleteCandidateAsync(int id, string revision) => RunAsync(async op =>
    {
        RequireRevision(revision);
        var entity = await op.Repository.GetCandidateAsync(op.ReadScope, id) ?? throw new KeyNotFoundException("Candidate not found in your permitted scope.");
        if (!await op.Repository.DeleteCandidateAsync(op.Session.Scope, id, revision)) throw new KeyNotFoundException("Candidate not found.");
        await images.DeleteIfUnreferencedAsync(op.Db, entity.PhotoFileName, "candidates");
        return true;
    });

    private async Task<string?> UploadAsync(IBrowserFile? file, string category)
    {
        if (file is null) return null;
        if (file.Size <= 0 || file.Size > AdminImageStore.MaxBytes) throw new ValidationException("Images must be nonempty and no larger than 2 MB.");
        await using var stream = file.OpenReadStream(AdminImageStore.MaxBytes);
        return await images.SaveAsync(stream, file.Name, file.ContentType, file.Size, category);
    }

    private static void Validate(object input) => Validator.ValidateObject(input, new ValidationContext(input), true);
    private static void RequireRevision(string? value)
    {
        if (value is null || !System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-f0-9]{32}$"))
            throw new ValidationException("Reload the record before saving or deleting it.");
    }

    private MunicipalityResponse Municipality(Municipality m) => new(m.MunicipalityId, m.Name, m.Slug, m.Province,
        m.ElectionDate, m.Description, images.Url(m.LogoFileName, "logos"), m.Revision);
    private static PositionResponse Position(Position p) => new(p.PositionId, p.Name, p.SeatsAvailable,
        p.MunicipalityId, p.Municipality.Name, p.Revision);
    private CandidateResponse Candidate(Candidate c) => new(c.CandidateId, c.Name, c.Profile,
        images.Url(c.PhotoFileName, "candidates"), c.Website, c.Email, c.DateCreated, c.MunicipalityId,
        c.Municipality.Name, c.PositionId, c.Position.Name, Municipality(c.Municipality),
        new PositionResponse(c.PositionId, c.Position.Name, c.Position.SeatsAvailable, c.MunicipalityId, c.Municipality.Name, c.Position.Revision), c.Revision);
}
