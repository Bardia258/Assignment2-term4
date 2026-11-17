using System.Security.Claims;
using MEMS.Data.Entities;
using MEMS.Data.Services;
using MEMS.Shared.Contracts;
using MEMS.Shared.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace MEMS.Api.Controllers;

[ApiController, Route("api/auth"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    AdministratorScopeResolver administrators, IAntiforgery antiforgery, ILogger<AuthController> logger) : ControllerBase
{
    [AllowAnonymous, HttpGet("csrf"), ProducesResponseType<CsrfResponse>(200)]
    public IActionResult Csrf()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfResponse(tokens.RequestToken!, tokens.HeaderName!));
    }

    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login"), ProducesResponseType<AdminSessionResponse>(200)]
    public async Task<IActionResult> Login(AdminLoginRequest request, CancellationToken ct)
    {
        var name = request.Username.Trim();
        var user = await users.FindByNameAsync(name) ?? await users.FindByEmailAsync(name);
        if (user is not null)
        {
            var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            var scope = result.Succeeded && !await users.GetTwoFactorEnabledAsync(user)
                ? await administrators.ResolveAsync(user.Id, ct) : null;
            if (scope is not null)
            {
                await signIn.SignInAsync(user, isPersistent: false);
                logger.LogInformation("Administrator login succeeded, trace {TraceId}", HttpContext.TraceIdentifier);
                return Ok(new AdminSessionResponse(user.Id, user.UserName!, scope.IsSuperAdmin
                    ? AdminRoles.SuperAdmin : AdminRoles.MunicipalityAdministrator, scope.MunicipalityId));
            }
        }
        logger.LogWarning("Administrator login denied, trace {TraceId}", HttpContext.TraceIdentifier);
        return Problem(statusCode: 401, title: "Sign-in failed", detail: "Invalid credentials or account unavailable.");
    }

    [Authorize, HttpGet("me"), ProducesResponseType<AdminSessionResponse>(200)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var scope = await administrators.ResolveAsync(id, ct);
        var user = await users.FindByIdAsync(id);
        return scope is null || user is null ? Forbid() : Ok(new AdminSessionResponse(user.Id,
            user.UserName!, scope.IsSuperAdmin ? AdminRoles.SuperAdmin : AdminRoles.MunicipalityAdministrator, scope.MunicipalityId));
    }

    [Authorize, HttpPost("logout"), ProducesResponseType(204)]
    public async Task<IActionResult> Logout()
    {
        var user = await users.GetUserAsync(User);
        if (user is not null && !(await users.UpdateSecurityStampAsync(user)).Succeeded)
            throw new InvalidOperationException("Could not invalidate the session.");
        await signIn.SignOutAsync();
        return NoContent();
    }
}
