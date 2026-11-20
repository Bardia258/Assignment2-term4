using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using MunicipalElections.Web.Authentication;
using MunicipalElections.Web.Data;
using MunicipalElections.Web.Infrastructure;
using MunicipalElections.Web.Models;
using MunicipalElections.Web.Services;

if (args.Any(argument => argument is "--provision-super-admin" or "--provision-pm-admin"))
{
    Console.Error.WriteLine("MVC administrator provisioning was removed from the iOS API. Mobile users register through /api/auth/register.");
    Environment.ExitCode = 1;
    return;
}

var builder = WebApplication.CreateBuilder(args);

var configuredConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection in appsettings.Development.json locally or through secure environment configuration in Production.");
var connectionString = DatabaseConfiguration.ResolveConnectionString(configuredConnection, builder.Environment.ContentRootPath);
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
    // Match the existing SQLite Identity schema, without registering Identity UI.
    options.Stores.MaxLengthForKeys = 128;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager();
builder.Services.AddHttpContextAccessor();

var jwt = JwtSettings.Load(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(jwt);
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = jwt.Key,
        ValidateLifetime = true, RequireExpirationTime = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = JwtRegisteredClaimNames.Sub
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var id = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var user = id is null ? null : await users.FindByIdAsync(id);
            if (user is null || await users.IsLockedOutAsync(user)
                || context.Principal?.FindFirst("security_stamp")?.Value != await users.GetSecurityStampAsync(user))
                context.Fail("The account or token is no longer valid.");
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IImageStorage, ImageStorage>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

// Maintenance commands finish before HTTP hosting. Production never migrates
// or seeds automatically, preserving the existing Azure SQLite database.
if (args.Any(DatabaseInitialization.IsInitializationCommand))
{
    Environment.ExitCode = await DatabaseInitialization.RunAsync(
        app.Services, args, Console.Out, Console.Error);
    await app.DisposeAsync();
    return;
}
if (args.Contains("--migrate-database"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await DatabaseMigration.MigrateAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    return;
}

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DatabaseMigration.MigrateAsync(db);
    await DbSeeder.SeedAsync(db);
}
else
{
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    await Results.Problem(statusCode: context.HttpContext.Response.StatusCode,
        title: context.HttpContext.Response.StatusCode switch
        {
            401 => "Authentication required", 403 => "Forbidden", 404 => "Not found",
            405 => "Method not allowed", _ => "Request failed"
        },
        extensions: new Dictionary<string, object?> { ["traceId"] = context.HttpContext.TraceIdentifier })
        .ExecuteAsync(context.HttpContext);
});
app.UseHttpsRedirection();

// Existing public photographs/logos remain available; no website assets or
// upload/write endpoints are hosted by this iOS API.
var uploadRoot = UploadOptions.ResolveRoot(
    builder.Configuration["Uploads:RootPath"] ?? "App_Data/uploads", app.Environment.ContentRootPath);
Directory.CreateDirectory(uploadRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadRoot), RequestPath = "/uploads",
    OnPrepareResponse = context => context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff"
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    service = "Municipal Elections iOS API",
    municipalities = "/api/municipalities",
    candidates = "/api/candidates",
    positions = "/api/positions"
})).AllowAnonymous();

app.Run();

public partial class Program { }
