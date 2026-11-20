using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MunicipalElections.Web.Data;

public sealed class DatabaseInitializationException(string message) : Exception(message);

/// <summary>Explicit sample election-data initialization for the SQLite API.</summary>
public static class DatabaseInitialization
{
    public const string SeedDemoDataCommand = "--seed-demo-data";
    public static bool IsInitializationCommand(string argument) => argument == SeedDemoDataCommand;

    public static async Task<int> RunAsync(IServiceProvider services, string[] args,
        TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        try
        {
            if (args.Count(IsInitializationCommand) != 1 || args.Contains("--migrate-database"))
                throw new DatabaseInitializationException("Run --seed-demo-data separately from --migrate-database.");

            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var connection = new SqliteConnectionStringBuilder(db.Database.GetConnectionString());
            if (connection.Mode == SqliteOpenMode.Memory || string.IsNullOrWhiteSpace(connection.DataSource)
                || connection.DataSource == ":memory:" || !File.Exists(connection.DataSource))
                throw new DatabaseInitializationException("Initialization requires an existing SQLite database file. Run the reviewed migration command separately first.");

            connection.Mode = SqliteOpenMode.ReadWrite;
            db.Database.SetConnectionString(connection.ToString());
            if (!db.Database.GetMigrations().Any()
                || (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any()
                || db.Database.HasPendingModelChanges())
                throw new DatabaseInitializationException("Database migrations must be current before initialization. Run the reviewed migration command separately first.");

            await DbSeeder.SeedAsync(db, cancellationToken);
            await output.WriteLineAsync("Demo election data initialized. Existing records and users were preserved.");
            return 0;
        }
        catch (DatabaseInitializationException exception)
        {
            await error.WriteLineAsync(exception.Message);
        }
        catch (Exception)
        {
            await error.WriteLineAsync("Initialization failed. Check database availability, schema, and configuration. No credentials were logged.");
        }
        return 1;
    }
}
