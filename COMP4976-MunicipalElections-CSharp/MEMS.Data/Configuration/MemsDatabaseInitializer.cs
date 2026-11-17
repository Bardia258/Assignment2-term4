using MEMS.Data.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MEMS.Data.Configuration;

/// <summary>Opt-in local development initialization; production uses reviewed EF deployment migrations.</summary>
public sealed class MemsDatabaseInitializer(IConfiguration configuration, IHostEnvironment environment,
    ILogger<MemsDatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Automatic MEMS migration startup is restricted to Development. Apply production migrations separately.");

        using var activity = DataTelemetry.Source.StartActivity("database.initialize");
        try
        {
            // Maintenance is explicitly privileged. Never lift the API request's tenant context.
            var options = new DbContextOptionsBuilder<ApplicationDbContext>();
            DataServiceCollectionExtensions.ConfigureSqlServer(options, configuration, environment.EnvironmentName);
            await using var db = new ApplicationDbContext(options.Options);
            await db.Database.MigrateAsync(cancellationToken); // EF locks migrations and invokes the idempotent seed hook.
            logger.LogInformation("MEMS database migrations completed; development seed enabled: {SeedEnabled}",
                configuration.GetValue<bool>("Mems:Seed:Enabled"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
            logger.LogError("MEMS initialization failed with {ErrorType}. Check SQL readiness and secure seed configuration.", error.GetType().Name);
            // Host startup logging must not print provider exception messages/connection strings.
            throw new InvalidOperationException("MEMS database initialization failed. Check SQL readiness and secure seed configuration.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
