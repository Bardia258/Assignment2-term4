using MEMS.Data.Seeding;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data.Tests;

internal static class TestDatabase
{
    public static ApplicationDbContext InMemory() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    public static ApplicationDbContext SqlModel() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer("Server=localhost;Database=MEMS_ModelOnly;Integrated Security=True;Encrypt=True").Options);

    // Generated per test; no reusable account credentials are stored in source control.
    public static DevelopmentSeedOptions SeedOptions() => new(true, "Development",
        DevelopmentSeeder.AccountNames.ToDictionary(n => n, _ => (string?)("Aa9!" + Guid.NewGuid().ToString("N"))));
}
