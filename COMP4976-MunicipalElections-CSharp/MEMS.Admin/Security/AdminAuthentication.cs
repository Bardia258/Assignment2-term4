using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.RateLimiting;
using MEMS.Data.Entities;
using MEMS.Data.Services;
using MEMS.Data.Tenancy;
using MEMS.Shared.Contracts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace MEMS.Admin.Security;

public static class AdminAuthentication
{
    public static IServiceCollection AddAdminAuthentication(this IServiceCollection services)
    {
        services.AddScoped<TenantQueryContext>();
        services.AddSingleton<AdminPrincipalValidator>();
        services.AddScoped<IAuthorizationHandler, CurrentAdministratorHandler>();
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, AdminAuthenticationStateProvider>();
        services.AddHttpContextAccessor();
        new IdentityBuilder(typeof(ApplicationUser), typeof(IdentityRole), services).AddSignInManager();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AdminPolicies.Administrator, p => p.RequireAuthenticatedUser().AddRequirements(new CurrentAdministratorRequirement(false)));
            options.AddPolicy(AdminPolicies.SuperAdmin, p => p.RequireAuthenticatedUser().AddRequirements(new CurrentAdministratorRequirement(true)));
        });
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
        services.ConfigureApplicationCookie(o =>
        {
            o.Cookie.Name = "__Host-MEMS.AdminUI"; o.Cookie.Path = "/"; o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Strict;
            o.LoginPath = "/admin/login"; o.AccessDeniedPath = "/admin/access-denied";
            o.ExpireTimeSpan = TimeSpan.FromMinutes(30); o.SlidingExpiration = false;
            o.Events.OnValidatePrincipal = async c =>
            {
                var expires = c.Principal?.FindFirst(AdminPrincipalValidator.ExpiresClaim);
                await SecurityStampValidator.ValidatePrincipalAsync(c);
                // Identity refreshes the principal from its store; retain the original absolute circuit expiry.
                if (expires is not null && c.Principal?.Identity is ClaimsIdentity identity && identity.IsAuthenticated
                    && !identity.HasClaim(claim => claim.Type == AdminPrincipalValidator.ExpiresClaim)) identity.AddClaim(expires);
                if (c.Principal?.Identity?.IsAuthenticated == true
                    && await c.HttpContext.RequestServices.GetRequiredService<AdminPrincipalValidator>()
                        .ValidateAsync(c.Principal, c.HttpContext.RequestAborted) is null)
                {
                    c.RejectPrincipal();
                    await c.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            };
        });
        services.AddAntiforgery(o =>
        {
            o.Cookie.Name = "__Host-MEMS.AdminUI.Csrf"; o.Cookie.Path = "/"; o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Strict;
        });
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy("admin-login", c => RateLimitPartition.GetFixedWindowLimiter(
                c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 10, QueueLimit = 0, AutoReplenishment = true, Window = TimeSpan.FromMinutes(1) }));
        });
        return services;
    }

    public static void MapAdminAuthentication(this WebApplication app)
    {
        app.MapPost("/admin/account/sign-in", async (HttpContext context, IAntiforgery csrf,
            UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
            AdministratorScopeResolver resolver, ILogger<AdminSession> logger) =>
        {
            try { await csrf.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.BadRequest("The sign-in request expired. Reload the sign-in page."); }
            if (!context.Request.HasFormContentType) return Results.BadRequest("Submit the sign-in form.");
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var input = new AdminLoginRequest { Username = form["Username"].ToString(), Password = form["Password"].ToString() };
            if (Validator.TryValidateObject(input, new ValidationContext(input), [], true))
            {
                var name = input.Username.Trim();
                var user = await users.FindByNameAsync(name) ?? await users.FindByEmailAsync(name);
                if (user is not null)
                {
                    var result = await signIn.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true);
                    if (result.Succeeded && !await users.GetTwoFactorEnabledAsync(user)
                        && await resolver.ResolveAsync(user.Id, context.RequestAborted) is not null)
                    {
                        await signIn.SignInWithClaimsAsync(user, isPersistent: false,
                            [new Claim(AdminPrincipalValidator.ExpiresClaim, DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds().ToString())]);
                        logger.LogInformation("Admin sign-in succeeded, trace {TraceId}", context.TraceIdentifier);
                        return Results.LocalRedirect(SafeReturnUrl(form["ReturnUrl"]));
                    }
                }
            }
            logger.LogWarning("Admin sign-in denied, trace {TraceId}", context.TraceIdentifier);
            return Results.LocalRedirect("/admin/login?error=credentials");
        }).AllowAnonymous().RequireRateLimiting("admin-login");

        app.MapPost("/admin/account/sign-out", async (HttpContext context, IAntiforgery csrf,
            UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) =>
        {
            try { await csrf.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.BadRequest("The sign-out request expired. Reload the page."); }
            var user = await users.GetUserAsync(context.User);
            if (user is not null && !(await users.UpdateSecurityStampAsync(user)).Succeeded)
                return Results.Problem("Sign-out could not be completed. Please retry.", statusCode: 503);
            await signIn.SignOutAsync();
            return Results.LocalRedirect("/admin/login?signedOut=1");
        }).RequireAuthorization(AdminPolicies.Administrator);
    }

    public static string SafeReturnUrl(string? value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
        && (value.Length == 6 || value[6] is '/' or '?' or '#')
        && !value.Contains('\\') && !value.Any(char.IsControl)
        && Uri.TryCreate(value, UriKind.Relative, out _) ? value : "/admin";
}
