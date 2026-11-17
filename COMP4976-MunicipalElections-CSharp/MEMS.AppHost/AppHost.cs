using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Aspire generates/stores the SQL password as a secret parameter, outside source control.
var sql = builder.AddSqlServer("sql").WithDataVolume();
var database = sql.AddDatabase("MEMS");

var api = builder.AddProject<Projects.MEMS_Api>("api", launchProfileName: "https")
    .WithReference(database)
    .WaitFor(database)
    .WithEnvironment("Mems__Database__Initialize", builder.ExecutionContext.IsRunMode ? "true" : "false")
    .WithHttpHealthCheck("/health", endpointName: "http");

var admin = builder.AddProject<Projects.MEMS_Admin>("admin", launchProfileName: "https")
    .WithReference(database)
    .WithReference(api)
    .WaitFor(api)
    .WithHttpHealthCheck("/health", endpointName: "http");

var publicSite = builder.AddProject<Projects.MEMS_Public>("public", launchProfileName: "https")
    .WithReference(api)
    .WaitFor(api);

api.WithEnvironment("Cors__AllowedOrigins__0", publicSite.GetEndpoint("https"))
    .WithEnvironment("Images__BaseUrl", admin.GetEndpoint("https"));

// Browser WebAssembly cannot read Aspire's server environment/service-discovery variables.
// Its owner configures an HttpClient with the API's browser-reachable HTTPS URL (see report).

// Optional development-only seed. Values are supplied via AppHost user secrets or environment.
if (builder.Configuration.GetValue<bool>("Mems:Seed:Enabled"))
{
    if (!builder.ExecutionContext.IsRunMode)
        throw new InvalidOperationException("Development seed parameters must not be published.");
    api.WithEnvironment("Mems__Seed__Enabled", "true");
    foreach (var (account, parameter) in new[]
    {
        ("Super", "seed-super-password"), ("Burnabyadmin", "seed-burnaby-password"),
        ("Surreyadmin", "seed-surrey-password"), ("Richmondadmin", "seed-richmond-password"),
        ("pm", "seed-pm-password")
    })
        api.WithEnvironment($"Mems__Seed__Passwords__{account}", builder.AddParameter(parameter, secret: true));
}

builder.Build().Run();
