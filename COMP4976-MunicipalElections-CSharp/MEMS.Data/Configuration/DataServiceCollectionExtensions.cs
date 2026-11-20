using MEMS.Data.Entities;
using MEMS.Data.Seeding;
using MEMS.Data.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MEMS.Data.Configuration;

public static class DataServiceCollectionExtensions
{
    /// <summary>Registers storage and Identity stores only; adds no authentication schemes or endpoints.</summary>
    public static IServiceCollection AddMemsData(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            ConfigureSqlServer(options, configuration,
                provider.GetRequiredService<IHostEnvironment>().EnvironmentName));
        services.AddMemsIdentity();
        services.AddOptions<IdentityOptions>().Configure<IHostEnvironment>((options, environment) =>
        {
            if (environment.IsDevelopment())
                options.Password.RequiredLength = DevelopmentSeedOptions.DefaultPassword.Length;
        });
        services.AddScoped(provider => new MunicipalityAdministratorProvisioner(
            provider.GetRequiredService<ApplicationDbContext>(),
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            provider.GetRequiredService<RoleManager<IdentityRole>>(),
            configuration["Mems:MunicipalityAdministrator:InitialPassword"]
                ?? (provider.GetRequiredService<IHostEnvironment>().IsDevelopment()
                    ? DevelopmentSeedOptions.DefaultPassword : null)));
        services.AddScoped<ElectionRepository>();
        services.AddScoped<AdministratorScopeResolver>();
        return services;
    }

    public static void ConfigureSqlServer(DbContextOptionsBuilder options, IConfiguration configuration, string environmentName)
    {
        var connection = configuration.GetConnectionString("MEMS");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException(
                "Configure ConnectionStrings:MEMS using user secrets or ConnectionStrings__MEMS. The Assignment 1 connection is never used.");

        var seedOptions = DevelopmentSeedOptions.FromConfiguration(configuration, environmentName);
        seedOptions.ValidateEnvironment();
        options.UseSqlServer(connection, sql => sql.EnableRetryOnFailure())
            .UseSeeding((context, _) => DevelopmentSeeder.RunAsync((ApplicationDbContext)context, seedOptions)
                .GetAwaiter().GetResult())
            .UseAsyncSeeding((context, _, cancellationToken) =>
                DevelopmentSeeder.RunAsync((ApplicationDbContext)context, seedOptions, cancellationToken));
    }

    internal static IdentityBuilder AddMemsIdentity(this IServiceCollection services,
        bool useDefaultDevelopmentPassword = false) =>
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = useDefaultDevelopmentPassword
                ? DevelopmentSeedOptions.DefaultPassword.Length : 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
}
