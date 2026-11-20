using Microsoft.Extensions.Configuration;

namespace MEMS.Data.Seeding;

public sealed record DevelopmentSeedOptions(
    bool Enabled, string EnvironmentName, IReadOnlyDictionary<string, string?> Passwords)
{
    // Explicit classroom-only default requested for the local assignment demo.
    public const string DefaultPassword = "P@$$w0rd";
    public bool UseDefaultPasswords { get; init; }

    public static DevelopmentSeedOptions FromConfiguration(IConfiguration configuration, string environmentName)
    {
        var useDefaults = configuration.GetValue<bool>("Mems:Seed:UseDefaultPasswords");
        return new(
            configuration.GetValue<bool>("Mems:Seed:Enabled"), environmentName,
            DevelopmentSeeder.AccountNames.ToDictionary(name => name,
                name => useDefaults ? DefaultPassword : configuration[$"Mems:Seed:Passwords:{name}"]))
        { UseDefaultPasswords = useDefaults };
    }

    public void ValidateEnvironment()
    {
        if ((Enabled || UseDefaultPasswords)
            && !string.Equals(EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MEMS sample data and initial accounts can only be seeded in Development.");
    }
}
