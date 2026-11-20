using MEMS.Data.Configuration;
using MEMS.Data.Entities;
using MEMS.Shared.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MEMS.Data.Seeding;

public static class DevelopmentSeeder
{
    public static IReadOnlyList<string> AccountNames { get; } =
        Array.AsReadOnly(new[] { "Super", "Burnabyadmin", "Surreyadmin", "Richmondadmin", "pm" });

    private static readonly (string Name, string? MunicipalityKey)[] Accounts =
    [
        ("Super", null), ("Burnabyadmin", "demo:burnaby"), ("Surreyadmin", "demo:surrey"),
        ("Richmondadmin", "demo:richmond"), ("pm", "demo:pitt-meadows")
    ];

    /// <summary>Repeatable development seed. Classroom mode also restores the shared demo password.</summary>
    public static async Task RunAsync(ApplicationDbContext db, DevelopmentSeedOptions options,
        CancellationToken cancellationToken = default)
    {
        options.ValidateEnvironment();
        if (!options.Enabled) return;

        // Identity uses the context provided by EF. DI does not own this external singleton.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddMemsIdentity(useDefaultDevelopmentPassword: options.UseDefaultPasswords);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
                ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

            // Check account ownership/password policies before adding domain records.
            foreach (var (name, _) in Accounts)
            {
                var key = SeedKey(name);
                var marked = await db.Users.SingleOrDefaultAsync(u => u.SeedKey == key, cancellationToken);
                var named = await users.FindByNameAsync(name);
                if (named is not null && named.SeedKey != key)
                    throw new InvalidOperationException($"Seed account '{name}' belongs to an unrecognized account. No privileges were granted.");
                if (marked is not null && !options.UseDefaultPasswords) continue;
                if (marked is null && await users.FindByEmailAsync(Email(name)) is not null)
                    throw new InvalidOperationException($"Seed email for '{name}' is already in use.");
                var password = PasswordFor(options, name);
                if (string.IsNullOrWhiteSpace(password))
                    throw new InvalidOperationException($"Configure Mems:Seed:Passwords:{name} securely before creating initial accounts.");
                foreach (var validator in users.PasswordValidators)
                    RequireSuccess(await validator.ValidateAsync(users, new ApplicationUser { UserName = name }, password),
                        $"validate the initial password for '{name}'");
            }

            await DomainSeeder.SeedAsync(db, cancellationToken);
            foreach (var role in new[] { AdminRoles.SuperAdmin, AdminRoles.MunicipalityAdministrator })
                if (!await roles.RoleExistsAsync(role))
                    RequireSuccess(await roles.CreateAsync(new IdentityRole(role)), $"create role '{role}'");

            foreach (var (name, municipalityKey) in Accounts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = SeedKey(name);
                var existing = await db.Users.SingleOrDefaultAsync(u => u.SeedKey == key, cancellationToken);
                if (existing is not null)
                {
                    if (options.UseDefaultPasswords) await ApplyDefaultPasswordAsync(users, existing);
                    continue;
                }
                var municipalityId = municipalityKey is null ? (int?)null
                    : await db.Municipalities.Where(m => m.SeedKey == municipalityKey)
                        .Select(m => m.MunicipalityId).SingleAsync(cancellationToken);
                var user = new ApplicationUser
                {
                    UserName = name, Email = Email(name), EmailConfirmed = true, LockoutEnabled = true,
                    SeedKey = key, MunicipalityId = municipalityId,
                    AdministratorType = municipalityId is null
                        ? AdministratorType.SuperAdmin : AdministratorType.MunicipalityAdministrator
                };
                RequireSuccess(await users.CreateAsync(user, PasswordFor(options, name)!), $"create account '{name}'");
                RequireSuccess(await users.AddToRoleAsync(user, municipalityId is null
                    ? AdminRoles.SuperAdmin : AdminRoles.MunicipalityAdministrator), $"assign role for '{name}'");
            }

            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        });
    }

    private static string SeedKey(string name) => $"development:{name.ToLowerInvariant()}";
    private static string Email(string name) => $"development-{name.ToLowerInvariant()}@example.com";

    private static string? PasswordFor(DevelopmentSeedOptions options, string name) =>
        options.UseDefaultPasswords ? DevelopmentSeedOptions.DefaultPassword
            : options.Passwords.GetValueOrDefault(name);

    private static async Task ApplyDefaultPasswordAsync(UserManager<ApplicationUser> users, ApplicationUser user)
    {
        if (!await users.CheckPasswordAsync(user, DevelopmentSeedOptions.DefaultPassword))
        {
            if (await users.HasPasswordAsync(user))
                RequireSuccess(await users.RemovePasswordAsync(user), $"replace the demo password for '{user.UserName}'");
            RequireSuccess(await users.AddPasswordAsync(user, DevelopmentSeedOptions.DefaultPassword),
                $"set the demo password for '{user.UserName}'");
        }
        if (user.AccessFailedCount > 0)
            RequireSuccess(await users.ResetAccessFailedCountAsync(user), $"clear demo login failures for '{user.UserName}'");
        if (user.LockoutEnd is not null)
            RequireSuccess(await users.SetLockoutEndDateAsync(user, null), $"unlock demo account '{user.UserName}'");
    }

    private static void RequireSuccess(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not {operation}: {string.Join(", ", result.Errors.Select(e => e.Code))}.");
    }
}
