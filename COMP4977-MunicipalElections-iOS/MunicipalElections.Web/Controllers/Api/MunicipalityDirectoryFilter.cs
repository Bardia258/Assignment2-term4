using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunicipalElections.Web.Data;

namespace MunicipalElections.Web.Controllers.Api;

public class MunicipalityDirectoryQuery
{
    [Range(1, int.MaxValue)] public int? MunicipalityId { get; set; }
    [StringLength(100)] public string? City { get; set; }
}
public sealed class CandidateDirectoryQuery : MunicipalityDirectoryQuery
{
    [StringLength(100)] public string? Name { get; set; }
    [Range(1, int.MaxValue)] public int? PositionId { get; set; }
}

internal static class MunicipalityDirectoryFilter
{
    public static async Task<(int? Id, IActionResult? Error)> ResolveAsync(
        ApplicationDbContext db, MunicipalityDirectoryQuery query, CancellationToken ct, IQueryCollection? parameters = null)
    {
        if (parameters is not null && ((parameters.ContainsKey("municipalityId") && string.IsNullOrWhiteSpace(parameters["municipalityId"]))
            || (parameters.ContainsKey("city") && string.IsNullOrWhiteSpace(parameters["city"]))))
            return (null, Error(400, "Municipality selectors cannot be empty."));
        var id = query.MunicipalityId;
        if (id.HasValue && !await db.Municipalities.AnyAsync(m => m.MunicipalityId == id.Value, ct))
            return (null, Error(404, "Municipality not found."));
        if (query.City is not null)
        {
            if (string.IsNullOrWhiteSpace(query.City)) return (null, Error(400, "City cannot be empty."));
            var name = query.City.Trim().Replace('-', ' ').ToUpperInvariant();
            var seedKey = "demo:" + query.City.Trim().ToLowerInvariant().Replace(' ', '-');
            var ids = await db.Municipalities.Where(m => m.Name.ToUpper() == name || m.SeedKey == seedKey)
                .Select(m => m.MunicipalityId).Take(2).ToListAsync(ct);
            if (ids.Count == 0) return (null, Error(404, "Municipality not found."));
            if (ids.Count != 1) return (null, Error(400, "City is ambiguous. Use municipalityId."));
            if (id.HasValue && id != ids[0]) return (null, Error(400, "City and municipalityId disagree."));
            id = ids[0];
        }
        return (id, null);
    }

    public static ObjectResult Error(int status, string detail) => new(new ProblemDetails
    {
        Status = status, Title = status == 404 ? "Not found" : "Invalid municipality filter", Detail = detail
    }) { StatusCode = status, ContentTypes = { "application/problem+json" } };
}
