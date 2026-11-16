using MEMS.Data.Entities;
using MEMS.Data.Seeding;
using MEMS.Data.Services;
using MEMS.Shared.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data.Tests;

public sealed class SeedingTests
{
    [Fact]
    public async Task SeedIsRepeatableAndPreservesEditsPasswordHashesAndRenamedAccounts()
    {
        await using var db = TestDatabase.InMemory();
        var options = TestDatabase.SeedOptions();
        await DevelopmentSeeder.RunAsync(db, options);
        Assert.Equal(5, await db.Municipalities.CountAsync());
        Assert.Equal(15, await db.Positions.CountAsync());
        Assert.Equal(32, await db.Candidates.CountAsync());
        Assert.Equal(5, await db.Users.CountAsync());
        Assert.Equal(2, await db.Roles.CountAsync());
        Assert.Equal(5, await db.UserRoles.CountAsync());
        foreach (var slug in new[] { "burnaby", "surrey", "richmond" })
            Assert.Equal(8, await db.Candidates.CountAsync(c => c.Municipality.Slug == slug));

        var admin = await db.Users.SingleAsync(u => u.NormalizedUserName == "SUPER");
        Assert.Null(admin.MunicipalityId);
        Assert.Equal(AdministratorType.SuperAdmin, admin.AdministratorType);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<ApplicationUser>()
            .VerifyHashedPassword(admin, admin.PasswordHash!, options.Passwords["Super"]!));
        var originalHash = admin.PasswordHash;
        admin.UserName = "RenamedSuper";
        admin.NormalizedUserName = "RENAMEDSUPER";
        var burnaby = await db.Municipalities.SingleAsync(m => m.Slug == "burnaby");
        burnaby.Description = "Administrator edited this description.";
        burnaby.Name = "Edited Burnaby";
        var candidate = await db.Candidates.FirstAsync();
        candidate.Profile = "Administrator edited this profile.";
        await db.SaveChangesAsync();
        await DevelopmentSeeder.RunAsync(db, options with { Passwords = new Dictionary<string, string?>() });

        Assert.Equal(5, await db.Municipalities.CountAsync());
        Assert.Equal(15, await db.Positions.CountAsync());
        Assert.Equal(32, await db.Candidates.CountAsync());
        Assert.Equal(5, await db.Users.CountAsync());
        Assert.Equal(5, await db.UserRoles.CountAsync());
        Assert.Equal(originalHash, admin.PasswordHash);
        Assert.Equal("RenamedSuper", admin.UserName);
        Assert.Equal("Edited Burnaby", burnaby.Name);
        Assert.Equal("Administrator edited this profile.", candidate.Profile);
        var pm = await db.Users.Include(u => u.Municipality).SingleAsync(u => u.UserName == "pm");
        Assert.Equal("pitt-meadows", pm.Municipality!.Slug);
    }

    [Fact]
    public async Task SeedRefusesToAdoptAnUnrecognizedExistingSuperAccount()
    {
        await using var db = TestDatabase.InMemory();
        var existing = new ApplicationUser { UserName = "super", NormalizedUserName = "SUPER" };
        db.Users.Add(existing);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => DevelopmentSeeder.RunAsync(db, TestDatabase.SeedOptions()));
        Assert.Equal(AdministratorType.None, existing.AdministratorType);
        Assert.Empty(await db.UserRoles.ToListAsync());
        Assert.Empty(await db.Municipalities.ToListAsync());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task SeedRejectsNonDevelopmentEnvironmentsBeforeWriting(string environment)
    {
        await using var db = TestDatabase.InMemory();
        await Assert.ThrowsAsync<InvalidOperationException>(() => DevelopmentSeeder.RunAsync(db,
            TestDatabase.SeedOptions() with { EnvironmentName = environment }));
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Municipalities.ToListAsync());
    }

    [Fact]
    public async Task SeedRequiresPasswordsOnlyForNewAccountsAndDisabledSeedingDoesNothing()
    {
        await using var db = TestDatabase.InMemory();
        var options = TestDatabase.SeedOptions() with { Passwords = new Dictionary<string, string?>() };
        await DevelopmentSeeder.RunAsync(db, options with { Enabled = false });
        Assert.Empty(await db.Municipalities.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => DevelopmentSeeder.RunAsync(db, options));
        Assert.Empty(await db.Municipalities.ToListAsync());
    }

    [Fact]
    public async Task ScopeResolverUsesCurrentRolesAndRejectsConflictingRoles()
    {
        await using var db = TestDatabase.InMemory();
        await DevelopmentSeeder.RunAsync(db, TestDatabase.SeedOptions());
        var resolver = new AdministratorScopeResolver(db);
        var super = await db.Users.SingleAsync(u => u.UserName == "Super");
        Assert.True((await resolver.ResolveAsync(super.Id))!.IsSuperAdmin);
        foreach (var name in new[] { "Burnabyadmin", "Surreyadmin", "Richmondadmin", "pm" })
        {
            var user = await db.Users.SingleAsync(u => u.UserName == name);
            Assert.Equal(user.MunicipalityId, (await resolver.ResolveAsync(user.Id))!.MunicipalityId);
        }
        var role = await db.Roles.SingleAsync(r => r.Name == AdminRoles.MunicipalityAdministrator);
        var extra = new IdentityUserRole<string> { UserId = super.Id, RoleId = role.Id };
        db.UserRoles.Add(extra);
        await db.SaveChangesAsync();
        Assert.Null(await resolver.ResolveAsync(super.Id));
        db.UserRoles.Remove(extra);
        await db.SaveChangesAsync();
        var membership = await db.UserRoles.SingleAsync(r => r.UserId == super.Id);
        db.UserRoles.Remove(membership);
        await db.SaveChangesAsync();
        Assert.Null(await resolver.ResolveAsync(super.Id));
        Assert.Null(await resolver.ResolveAsync("missing-user"));
    }
}
