using System.Security.Claims;
using MEMS.Data.Entities;
using MEMS.Data.Services;
using MEMS.Shared.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MEMS.Admin.Security;

public sealed record AdminSession(string UserId, string Username, MunicipalityScope Scope)
{
    public bool IsSuperAdmin => Scope.IsSuperAdmin;
    public int? MunicipalityId => Scope.MunicipalityId;
}

public sealed class AdminPrincipalValidator(IServiceScopeFactory scopes, IOptions<IdentityOptions> options)
{
    public const string ExpiresClaim = "mems-admin:expires";
    public async Task<AdminSession?> ValidateAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await ValidateInScopeAsync(principal, scope.ServiceProvider, ct);
    }

    public async Task<AdminSession?> ValidateInScopeAsync(ClaimsPrincipal principal, IServiceProvider services, CancellationToken ct = default)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        if (!long.TryParse(principal.FindFirstValue(ExpiresClaim), out var expires)
            || expires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;
        var id = principal.FindFirstValue(options.Value.ClaimsIdentity.UserIdClaimType);
        var stamp = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(stamp)) return null;
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(id);
        if (user is null || !string.Equals(stamp, await users.GetSecurityStampAsync(user), StringComparison.Ordinal)) return null;
        var access = await services.GetRequiredService<AdministratorScopeResolver>().ResolveAsync(id, ct);
        return access is null ? null : new AdminSession(user.Id, user.UserName ?? "", access);
    }
}
