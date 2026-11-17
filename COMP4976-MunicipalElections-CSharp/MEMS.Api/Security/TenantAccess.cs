using System.Security.Claims;
using MEMS.Data;
using MEMS.Data.Services;
using MEMS.Data.Tenancy;
using MEMS.Shared.Contracts;
using MEMS.Shared.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Api.Security;

public sealed class TenantAccess
{
    public MunicipalityScope Scope { get; internal set; } = null!;
    public MunicipalityScope ReadScope { get; internal set; } = null!;
}

/// <summary>Resolve trusted ownership once before resource actions, including body/route/query conflicts.</summary>
public sealed class TenantActionFilter(ApplicationDbContext db, AdministratorScopeResolver administrators,
    TenantAccess access, TenantQueryContext queries) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var ct = context.HttpContext.RequestAborted;
        var query = context.ActionArguments.Values.OfType<MunicipalityQuery>().FirstOrDefault() ?? new();
        if ((request.Query.ContainsKey("municipalityId") && string.IsNullOrWhiteSpace(request.Query["municipalityId"]))
            || (request.Query.ContainsKey("municipalitySlug") && string.IsNullOrWhiteSpace(request.Query["municipalitySlug"])))
        { Fail(400, "Municipality selectors cannot be empty."); return; }
        int? requested = query.MunicipalityId;
        var slug = query.MunicipalitySlug;
        if (context.Controller is Controllers.MunicipalitiesController)
        {
            if (context.ActionArguments.TryGetValue("id", out var routeId) && routeId is int id)
            {
                if (requested.HasValue && requested != id) { Fail(400, "Route and municipality filter disagree."); return; }
                requested = id;
            }
            if (context.ActionArguments.TryGetValue("slug", out var routeSlug)) slug = routeSlug as string;
        }
        var bodyTenant = context.ActionArguments.Values.Select(value => value switch
        {
            CandidateRequest c => (int?)c.MunicipalityId,
            PositionRequest p => p.MunicipalityId,
            _ => null
        }).FirstOrDefault(id => id.HasValue);
        if (bodyTenant.HasValue)
        {
            if (requested.HasValue && requested != bodyTenant) { Fail(400, "Body and municipality filter disagree."); return; }
            requested = bodyTenant;
        }
        if (requested is <= 0) { Fail(400, "Municipality identifiers must be positive."); return; }

        MunicipalityScope? administrator = null;
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            administrator = await administrators.ResolveAsync(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", ct);
            if (administrator is null) { Fail(403, "An active administrator assignment is required."); return; }
        }

        // Only selector metadata is resolved without filters; election records remain scoped.
        if (!string.IsNullOrWhiteSpace(slug))
        {
            var selected = await db.Municipalities.IgnoreQueryFilters().AsNoTracking()
                .Where(m => m.Slug == slug).Select(m => (int?)m.MunicipalityId).SingleOrDefaultAsync(ct);
            if (!selected.HasValue) { Fail(404, "Municipality not found."); return; }
            if (requested.HasValue && requested != selected) { Fail(400, "Municipality ID and slug disagree."); return; }
            requested = selected;
        }
        if (administrator is not null && requested.HasValue && !administrator.Allows(requested.Value))
        {
            Fail(403, "The requested municipality is outside your assignment."); return;
        }
        if (requested.HasValue && !await db.Municipalities.IgnoreQueryFilters().AnyAsync(m => m.MunicipalityId == requested.Value, ct))
        {
            Fail(404, "Municipality not found."); return;
        }
        if (!requested.HasValue && administrator is { IsSuperAdmin: false }) requested = administrator.MunicipalityId;
        var directory = context.Controller is Controllers.MunicipalitiesController;
        if (administrator is null && !directory && !requested.HasValue)
        {
            Fail(400, "Supply municipalityId or municipalitySlug for public election data."); return;
        }
        access.Scope = administrator ?? (requested.HasValue
            ? MunicipalityScope.ForMunicipality(requested.Value) : MunicipalityScope.ForSuperAdmin());
        access.ReadScope = requested.HasValue ? MunicipalityScope.ForMunicipality(requested.Value) : access.Scope;
        if (requested.HasValue) queries.RestrictTo(requested.Value); else queries.AllowAll();
        await next();

        void Fail(int status, string detail) => context.Result = new ObjectResult(new ProblemDetails
        {
            Status = status, Title = status == 403 ? "Forbidden" : status == 404 ? "Not found" : "Invalid request",
            Detail = detail, Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
        }) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
