using MEMS.Admin.Components;
using MEMS.Admin.Security;
using MEMS.Admin.Services;
using MEMS.Data.Configuration;
using MEMS.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddMemsData(builder.Configuration);
builder.EnrichSqlServerDbContext<ApplicationDbContext>();
builder.Services.AddAdminAuthentication();
builder.Services.AddDataProtection().SetApplicationName("MEMS.Admin");
builder.Services.AddScoped<AdminDataService>();
builder.Services.AddSingleton<AdminImageStore>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents(o => o.DetailedErrors = false);
var app = builder.Build();
app.UseExceptionHandler("/admin/error", createScopeForErrors: true);
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseStatusCodePagesWithReExecute("/admin/not-found", createScopeForStatusCodePages: true);
app.UseWhen(c => c.Request.Path != "/health" && c.Request.Path != "/alive", b => b.UseHttpsRedirection());
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/admin"))
    {
        context.Response.Headers.CacheControl = "no-cache, no-store";
        context.Response.Headers.Pragma = "no-cache";
    }
    await next();
});
var images = app.Services.GetRequiredService<AdminImageStore>();
Directory.CreateDirectory(images.RootPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(images.RootPath), RequestPath = "/uploads",
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();
app.MapDefaultEndpoints();
app.MapStaticAssets();
app.MapGet("/", () => Results.LocalRedirect("/admin"));
app.MapAdminAuthentication();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

namespace MEMS.Admin { public sealed class AdminAssemblyMarker; }
