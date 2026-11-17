using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using MEMS.Admin;
using MEMS.Admin.Security;
using MEMS.Admin.Services;
using MEMS.Data;
using MEMS.Data.Entities;
using MEMS.Data.Seeding;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MEMS.Admin.Tests;

public sealed class AdminFactory : WebApplicationFactory<AdminAssemblyMarker>
{
    private readonly InMemoryDatabaseRoot databaseRoot = new();
    private readonly string databaseName = Guid.NewGuid().ToString("N");
    private readonly string generatedRoot = Path.Combine(Path.GetTempPath(), "MEMS-AdminTests-" + Guid.NewGuid().ToString("N"));
    private X509Certificate2? certificate;
    public string Password { get; } = "Aa9!" + Guid.NewGuid().ToString("N");

    public AdminFactory(bool browser = false)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName, databaseRoot).Options);
        DevelopmentSeeder.RunAsync(db, new DevelopmentSeedOptions(true, "Development",
            DevelopmentSeeder.AccountNames.ToDictionary(n => n, _ => (string?)Password))).GetAwaiter().GetResult();
        if (browser)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder(); names.AddIpAddress(IPAddress.Loopback); names.AddDnsName("localhost");
            request.CertificateExtensions.Add(names.Build());
            using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            // Reimport for Windows Schannel rather than retaining a transient CertificateRequest key handle.
            certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pkcs12), null, X509KeyStorageFlags.Exportable);
            UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listener => listener.UseHttps(certificate)));
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseStaticWebAssets();
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Uploads:RootPath"] = Path.Combine(generatedRoot, "uploads"),
            ["Logging:LogLevel:Default"] = "Warning"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>(); services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(databaseName, databaseRoot));
        });
    }

    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), HandleCookies = true, AllowAutoRedirect = false });

    public string BrowserAddress
    {
        get
        {
            StartServer();
            return Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        }
    }

    public static string AntiforgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success) throw new InvalidOperationException("Antiforgery token missing from page.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public async Task<HttpClient> SignInAsync(string name)
    {
        var client = Client();
        var html = await client.GetStringAsync("/admin/login");
        var response = await client.PostAsync("/admin/account/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = name, ["Password"] = Password, ["__RequestVerificationToken"] = AntiforgeryToken(html)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin", response.Headers.Location!.OriginalString);
        return client;
    }

    public async Task<T> AsUserAsync<T>(string username, Func<AdminDataService, IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByNameAsync(username))!;
        var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(AdminPrincipalValidator.ExpiresClaim,
            DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds().ToString()));
        var state = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        ((IHostEnvironmentAuthenticationStateProvider)state).SetAuthenticationState(Task.FromResult(new AuthenticationState(principal)));
        return await action(scope.ServiceProvider.GetRequiredService<AdminDataService>(), scope.ServiceProvider);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        certificate?.Dispose();
        var absolute = Path.GetFullPath(generatedRoot);
        if (absolute.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(absolute).StartsWith("MEMS-AdminTests-", StringComparison.Ordinal)
            && Directory.Exists(absolute)) Directory.Delete(absolute, recursive: true);
    }
}
