using MEMS.Api.Infrastructure;
using MEMS.Api.Security;
using MEMS.Data.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddMemsData(builder.Configuration);
builder.Services.AddMemsApiSecurity(builder.Configuration);
builder.Services.AddDataProtection().SetApplicationName("MEMS.Api");
builder.Services.AddControllers(o => o.Filters.Add<ApiAntiforgeryFilter>());
builder.Services.AddScoped<DirectoryMapper>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
    c.ProblemDetails.Extensions["traceId"] = c.HttpContext.TraceIdentifier);
builder.Services.AddOpenApi(ApiDocumentation.Configure);

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    await Results.Problem(statusCode: context.HttpContext.Response.StatusCode,
        title: context.HttpContext.Response.StatusCode switch
        {
            401 => "Authentication required", 403 => "Forbidden", 404 => "Not found",
            405 => "Method not allowed", 429 => "Too many requests", _ => "Invalid request"
        }, extensions: new Dictionary<string, object?> { ["traceId"] = context.HttpContext.TraceIdentifier })
        .ExecuteAsync(context.HttpContext);
});
app.UseHttpsRedirection();
app.UseCors("Frontends");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled")) app.MapOpenApi();
app.MapDefaultEndpoints();
app.MapControllers();
app.Run();

namespace MEMS.Api
{
    public sealed class ApiAssemblyMarker;
}
