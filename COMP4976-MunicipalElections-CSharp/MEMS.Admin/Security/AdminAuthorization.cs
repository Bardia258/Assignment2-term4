using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace MEMS.Admin.Security;

public static class AdminPolicies
{
    public const string Administrator = "MemsAdministrator";
    public const string SuperAdmin = "MemsSuperAdmin";
}

public sealed record CurrentAdministratorRequirement(bool SuperOnly) : IAuthorizationRequirement;

public sealed class CurrentAdministratorHandler(AdminPrincipalValidator validator) : AuthorizationHandler<CurrentAdministratorRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CurrentAdministratorRequirement requirement)
    {
        var session = await validator.ValidateAsync(context.User);
        if (session is not null && (!requirement.SuperOnly || session.IsSuperAdmin)) context.Succeed(requirement);
    }
}

public sealed class AdminAuthenticationStateProvider(ILoggerFactory logs, AdminPrincipalValidator validator)
    : RevalidatingServerAuthenticationStateProvider(logs)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);
    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState state, CancellationToken ct)
    {
        try { return await validator.ValidateAsync(state.User, ct) is not null; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return false; }
        catch { return false; } // Database/security failures cannot keep an authenticated circuit alive.
    }
}
