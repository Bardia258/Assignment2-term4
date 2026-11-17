using MEMS.Data.Configuration;
using MEMS.Data.Entities;
using MEMS.Data.Seeding;
using MEMS.Data.Services;
using MEMS.Data.Tenancy;
using MEMS.Shared.Tenancy;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace MEMS.Data.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMS_TEST_SQLSERVER")))
            Skip = "Set MEMS_TEST_SQLSERVER to run against SQL Server; requires permission to create/drop isolated test databases.";
    }
}

public sealed class SqlServerIntegrationTests
{
    [SqlServerFact]
    public async Task MigrationsSeedingConstraintsAndTenantQueriesWorkOnSqlServer()
    {
        // Always create an isolated database; never migrate/drop the database named in the supplied connection.
        var databaseName = "MEMS_Task1Tests_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MEMS_TEST_SQLSERVER"))
        {
            InitialCatalog = databaseName
        };
        var seed = TestDatabase.SeedOptions();
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:MEMS"] = connection.ConnectionString, ["Mems:Seed:Enabled"] = "true"
        };
        foreach (var (name, password) in seed.Passwords) values[$"Mems:Seed:Passwords:{name}"] = password;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        DataServiceCollectionExtensions.ConfigureSqlServer(options, configuration, "Development");
        await using var db = new ApplicationDbContext(options.Options);
        try
        {
            await db.Database.MigrateAsync(); // Async seed hook.
            Assert.Single(await db.Database.GetAppliedMigrationsAsync());
            db.Database.Migrate(); // Sync seed hook, including the no-pending-migrations case.
            Assert.Equal(32, await db.Candidates.CountAsync());
            Assert.Equal(15, await db.Positions.CountAsync());
            Assert.Equal(5, await db.Users.CountAsync());
            var burnaby = await db.Municipalities.SingleAsync(m => m.Slug == "burnaby");
            var surrey = await db.Municipalities.SingleAsync(m => m.Slug == "surrey");
            var foreignPosition = await db.Positions.FirstAsync(p => p.MunicipalityId == surrey.MunicipalityId);
            var scope = MunicipalityScope.ForMunicipality(burnaby.MunicipalityId);
            Assert.Equal(8, (await new ElectionRepository(db).GetCandidatesAsync(scope)).Count);
            var boundary = new TenantQueryContext();
            boundary.RestrictTo(burnaby.MunicipalityId);
            await using (var tenantDb = new ApplicationDbContext(options.Options, boundary))
            {
                Assert.Equal(8, await tenantDb.Candidates.CountAsync());
                Assert.Equal(3, await tenantDb.Positions.CountAsync());
                Assert.False(await tenantDb.Candidates.AnyAsync(c => c.MunicipalityId == surrey.MunicipalityId));
                var forbidden = await tenantDb.Candidates.IgnoreQueryFilters().FirstAsync(c => c.MunicipalityId == surrey.MunicipalityId);
                forbidden.Profile = "A forged cross-tenant write.";
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenantDb.SaveChangesAsync());
            }
            // Raw SQL bypasses model/service validation, proving the database constraints themselves.
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Candidates (Name, Profile, Revision, PositionId, MunicipalityId)
                VALUES ('Invalid cross-tenant candidate', 'Profile', {Guid.NewGuid().ToString("N")}, {foreignPosition.PositionId}, {burnaby.MunicipalityId})
                """));
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Positions (Name, Revision, SeatsAvailable, MunicipalityId)
                VALUES ('Invalid seats', {Guid.NewGuid().ToString("N")}, 0, {burnaby.MunicipalityId})
                """));
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE AspNetUsers SET MunicipalityId = {burnaby.MunicipalityId} WHERE NormalizedUserName = 'SUPER'
                """));
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE AspNetUsers SET MunicipalityId = NULL WHERE NormalizedUserName = 'BURNABYADMIN'"));
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Municipalities SET Slug = 'INVALID SLUG' WHERE MunicipalityId = {burnaby.MunicipalityId}"));
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Municipalities SET Slug = 'burnaby' WHERE MunicipalityId = {surrey.MunicipalityId}"));
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Municipalities SET Name = '' WHERE MunicipalityId = {burnaby.MunicipalityId}"));
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM Municipalities WHERE MunicipalityId = {burnaby.MunicipalityId}"));
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM Positions WHERE PositionId = {foreignPosition.PositionId}"));
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE AspNetUsers SET NormalizedUserName = 'SUPER' WHERE NormalizedUserName = 'BURNABYADMIN'"));

            // Preserve optimistic concurrency on SQL Server as well as in offline tests.
            var tracked = await db.Candidates.FirstAsync(c => c.MunicipalityId == burnaby.MunicipalityId);
            await using var other = new ApplicationDbContext(options.Options);
            var competing = await other.Candidates.SingleAsync(c => c.CandidateId == tracked.CandidateId);
            competing.Profile = "Concurrent update.";
            await other.SaveChangesAsync();
            tracked.Profile = "Stale update.";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            await using var schemaOnly = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connection.ConnectionString).Options);
            await schemaOnly.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase);
            Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
            await db.Database.MigrateAsync();
            Assert.Equal(32, await db.Candidates.CountAsync());
        }
        finally
        {
            if (new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog != databaseName)
                throw new InvalidOperationException("Refusing to clean a database other than this test's generated database.");
            await db.Database.EnsureDeletedAsync();
        }
    }
}
