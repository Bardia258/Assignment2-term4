using Microsoft.Extensions.Configuration;

namespace MEMS.Data.Seeding;

public sealed record DevelopmentSeedOptions(
    bool Enabled, string EnvironmentName, IReadOnlyDictionary<string, string?> Passwords)
{
    public static DevelopmentSeedOptions FromConfiguration(IConfiguration configuration, string environmentName) => new(
        configuration.GetValue<bool>("Mems:Seed:Enabled"), environmentName,
        DevelopmentSeeder.AccountNames.ToDictionary(name => name,
            name => configuration[$"Mems:Seed:Passwords:{name}"]));

    public void ValidateEnvironment()
    {
        if (Enabled && !string.Equals(EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MEMS sample data and initial accounts can only be seeded in Development.");
    }
}
