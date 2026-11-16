using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MEMS.Data.Configuration;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<ApplicationDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();
        var environment = configuration["DOTNET_ENVIRONMENT"]
            ?? configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        DataServiceCollectionExtensions.ConfigureSqlServer(options, configuration, environment);
        return new ApplicationDbContext(options.Options);
    }
}
