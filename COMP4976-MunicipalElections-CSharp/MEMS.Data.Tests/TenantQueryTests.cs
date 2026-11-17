using MEMS.Data.Entities;
using MEMS.Data.Seeding;
using MEMS.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MEMS.Data.Tests;

public sealed class TenantQueryTests
{
    [Fact]
    public async Task RequestFiltersDefaultToNoAccessAndApplyAcrossContextInstances()
    {
        var root = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"), root).Options;
        int burnabyId;
        await using (var seed = new ApplicationDbContext(options))
        {
            await DevelopmentSeeder.RunAsync(seed, TestDatabase.SeedOptions());
            burnabyId = (await seed.Municipalities.SingleAsync(m => m.Slug == "burnaby")).MunicipalityId;
        }
        var access = new TenantQueryContext();
        await using var db = new ApplicationDbContext(options, access);
        Assert.Empty(await db.Candidates.ToListAsync());
        Assert.Empty(await db.Positions.ToListAsync());
        access.RestrictTo(burnabyId);
        Assert.Equal(8, await db.Candidates.CountAsync());
        Assert.Equal(3, await db.Positions.CountAsync());
        Assert.Single(await db.Municipalities.ToListAsync());
        Assert.All(await db.Candidates.Include(c => c.Position).Include(c => c.Municipality).ToListAsync(),
            c => Assert.Equal(burnabyId, c.Position.MunicipalityId));
        access.AllowAll();
        Assert.Equal(32, await db.Candidates.CountAsync());
    }

    [Fact]
    public async Task SaveGuardRejectsBypassingFiltersToModifyForeignRecords()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        int burnabyId;
        await using (var seed = new ApplicationDbContext(options))
        {
            await DevelopmentSeeder.RunAsync(seed, TestDatabase.SeedOptions());
            burnabyId = (await seed.Municipalities.SingleAsync(m => m.Slug == "burnaby")).MunicipalityId;
        }
        var access = new TenantQueryContext();
        access.RestrictTo(burnabyId);
        await using var db = new ApplicationDbContext(options, access);
        var foreign = await db.Candidates.IgnoreQueryFilters().FirstAsync(c => c.MunicipalityId != burnabyId);
        foreign.Profile = "This write must never succeed.";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var owned = await db.Candidates.FirstAsync();
        owned.MunicipalityId = foreign.MunicipalityId;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());
    }
}
