using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using MEMS.Api;
using MEMS.Data;
using MEMS.Data.Seeding;
using MEMS.Shared.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using LegacyContext = MunicipalElections.Web.Data.ApplicationDbContext;

namespace MEMS.Api.Tests;

public sealed class MemsFactory : WebApplicationFactory<ApiAssemblyMarker>
{
    private readonly InMemoryDatabaseRoot root = new();
    private readonly string database = Guid.NewGuid().ToString("N");
    public string Password { get; } = "Aa9!" + Guid.NewGuid().ToString("N");

    public MemsFactory()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(database, root).Options);
        DevelopmentSeeder.RunAsync(db, new DevelopmentSeedOptions(true, "Development",
            DevelopmentSeeder.AccountNames.ToDictionary(n => n, _ => (string?)Password))).GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenApi:Enabled"] = "true",
            ["ConnectionStrings:MEMS"] = "Server=localhost;Database=MEMS_TestOnly;Integrated Security=True"
        }));
        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(FailureController).Assembly);
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(database, root));
        });
    }

    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });

    public static async Task CsrfAsync(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var token = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add(token!.HeaderName, token.RequestToken);
    }

    public async Task<HttpClient> LoginAsync(string username)
    {
        var client = Client();
        await CsrfAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new AdminLoginRequest { Username = username, Password = Password });
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException("Test login failed: " + (int)response.StatusCode + " " + await response.Content.ReadAsStringAsync());
        await CsrfAsync(client);
        return client;
    }
}

public sealed class LegacyFactory : WebApplicationFactory<MunicipalElections.Web.Models.ApplicationUser>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly string uploadRoot = Path.Combine(Path.GetTempPath(), "MEMS-LegacyApiTests-" + Guid.NewGuid().ToString("N"), "uploads");

    public LegacyFactory() => connection.Open();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
            ["Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ["Uploads:RootPath"] = uploadRoot
        }));
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
            ["Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ["Uploads:RootPath"] = uploadRoot
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<LegacyContext>();
            services.RemoveAll<DbContextOptions<LegacyContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<LegacyContext>>();
            services.AddDbContext<LegacyContext>(o => o.UseSqlite(connection));
        });
    }

    public async Task<HttpClient> SeededClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LegacyContext>();
        await db.Database.EnsureCreatedAsync();
        await MunicipalElections.Web.Data.DbSeeder.SeedAsync(db);
        return client;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await connection.DisposeAsync();
        var generatedRoot = Path.GetFullPath(Path.GetDirectoryName(uploadRoot)!);
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        if (generatedRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(generatedRoot).StartsWith("MEMS-LegacyApiTests-", StringComparison.Ordinal)
            && Directory.Exists(generatedRoot)) Directory.Delete(generatedRoot, recursive: true);
    }
}
