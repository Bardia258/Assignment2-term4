using System.Security.Claims;
using System.Threading.RateLimiting;
using MEMS.Data.Entities;
using MEMS.Data.Services;
using MEMS.Data.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
namespace MEMS.Api.Security;

public static class ApiSecurity
{
    public static IServiceCollection AddMemsApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TenantQueryContext>();
        services.AddScoped<TenantAccess>();
        services.AddScoped<TenantActionFilter>();
        services.AddHttpContextAccessor();
        new IdentityBuilder(typeof(ApplicationUser), typeof(IdentityRole), services).AddSignInManager();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddAuthorization();
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
        services.ConfigureApplicationCookie(o =>
        {
            o.Cookie.Name = "__Host-MEMS.Admin"; o.Cookie.Path = "/"; o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Strict;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(30); o.SlidingExpiration = false;
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
            o.Events.OnValidatePrincipal = async c =>
            {
                await SecurityStampValidator.ValidatePrincipalAsync(c);
                if (c.Principal?.Identity?.IsAuthenticated != true) return;
                var resolver = c.HttpContext.RequestServices.GetRequiredService<AdministratorScopeResolver>();
                if (await resolver.ResolveAsync(c.Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                    c.HttpContext.RequestAborted) is null)
                {
                    c.RejectPrincipal();
                    await c.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            };
        });
        services.AddAntiforgery(o =>
        {
            o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "__Host-MEMS.Csrf"; o.Cookie.Path = "/";
            o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Strict;
        });
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(
                c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        });
        services.AddCors(o => o.AddPolicy("Frontends", policy =>
        {
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }));
        return services;
    }
}
