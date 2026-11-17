using System.Text.Json;
using MEMS.Data.Configuration;
using MEMS.Shared.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace MEMS.Admin.Tests;

public sealed class AspireFactAttribute : FactAttribute
{
    public AspireFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MEMS_TEST_ASPIRE") != "true")
            Skip = "Start the development AppHost, then configure MEMS_TEST_ASPIRE and the documented test credentials/URLs.";
    }
}

[Collection("Browser verification")]
public sealed class AspireBackendTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Setting(string key) => Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Configure {key} securely to run the live Aspire test.");

    [Fact]
    public async Task AutomaticDatabaseInitializationRefusesProduction()
    {
        var host = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Production" });
        var initializer = new MemsDatabaseInitializer(new ConfigurationBuilder().Build(), host.Environment,
            NullLogger<MemsDatabaseInitializer>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.StartAsync(default));
    }

    [AspireFact]
    public async Task LiveAdminSqlServerApiImagesAndAllAdministratorTenantsWorkTogether()
    {
        var admin = Setting("MEMS_TEST_ADMIN_URL").TrimEnd('/');
        var api = Setting("MEMS_TEST_API_URL").TrimEnd('/');
        var publicUrl = Setting("MEMS_TEST_PUBLIC_URL").TrimEnd('/');
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        await using var anonymous = await playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var slug = "integration-" + suffix;
        int? municipalityId = null, positionId = null, candidateId = null;
        var superPassword = Setting("MEMS_TEST_SUPER_PASSWORD");
        var municipalities = await Get<List<MunicipalityResponse>>(anonymous, api + "/api/municipalities");
        foreach (var city in new[] { "burnaby", "surrey", "richmond" })
        {
            Assert.Contains(municipalities, m => m.Slug == city);
            Assert.Equal(8, (await Get<List<CandidateResponse>>(anonymous, api + "/api/candidates?municipalitySlug=" + city)).Count);
            Assert.Equal(3, (await Get<List<PositionResponse>>(anonymous, api + "/api/positions?municipalitySlug=" + city)).Count);
        }
        Assert.Equal(401, (await anonymous.PostAsync(api + "/api/candidates", new() { DataObject = new { } })).Status);
        var publicPage = await context.NewPageAsync();
        await publicPage.GotoAsync(publicUrl);
        Assert.True(await publicPage.EvaluateAsync<bool>("""
            async url => {
                const response = await fetch(url + '/api/candidates?municipalitySlug=burnaby');
                const rows = await response.json();
                return response.ok && rows.length === 8 && rows.every(c => c.municipality.slug === 'burnaby');
            }
            """, api)); // Real browser CORS check from the unmodified Public template's origin.
        await publicPage.CloseAsync();
        try
        {
            await BrowserLogin(page, admin, "Super", superPassword);
            await Assertions.Expect(page.GetByTestId("municipality-count")).ToHaveTextAsync(municipalities.Count.ToString());
            await Assertions.Expect(page.GetByTestId("position-count")).ToHaveTextAsync("15");
            await Assertions.Expect(page.GetByTestId("candidate-count")).ToHaveTextAsync("32");
            await page.GotoAsync(admin + "/admin/municipalities/create");
            await page.GetByLabel("Name", new() { Exact = true }).FillAsync("Integration " + suffix);
            await page.GetByLabel("Tenant URL slug").FillAsync(slug);
            await page.GetByLabel("Province", new() { Exact = true }).FillAsync("BC");
            await page.GetByLabel("Election date").FillAsync("2026-10-17");
            await page.GetByLabel("Logo", new() { Exact = true }).SetInputFilesAsync(new FilePayload
                { Name = "logo.png", MimeType = "image/png", Buffer = ImageTests.Png() });
            await Save(page, "municipality", "municipalities");
            municipalityId = Id(page);
            var municipality = await Get<MunicipalityResponse>(anonymous, api + "/api/municipalities/slug/" + slug);
            Assert.Equal(municipalityId, municipality.Id);
            Assert.StartsWith(admin + "/uploads/logos/", municipality.LogoUrl);
            Assert.Equal(200, (await anonymous.GetAsync(municipality.LogoUrl!)).Status);
            await page.GetByRole(AriaRole.Link, new() { Name = "Edit municipality", Exact = true }).ClickAsync();
            await page.GetByLabel("Description", new() { Exact = true }).FillAsync("Live SQL Server verification.");
            await Save(page, "municipality", "municipalities");
            Assert.Equal("Live SQL Server verification.", (await Get<MunicipalityResponse>(anonymous, api + "/api/municipalities/slug/" + slug)).Description);

            await page.GotoAsync(admin + "/admin/positions/create");
            await page.GetByLabel("Name", new() { Exact = true }).FillAsync("Integration Mayor");
            await page.GetByLabel("Available seats").FillAsync("1");
            await page.GetByLabel("Municipality", new() { Exact = true }).SelectOptionAsync(municipalityId.Value.ToString());
            await Save(page, "position", "positions"); positionId = Id(page);
            await page.GetByRole(AriaRole.Link, new() { Name = "Edit position", Exact = true }).ClickAsync();
            await page.GetByLabel("Available seats").FillAsync("2");
            await Save(page, "position", "positions");
            Assert.Equal(2, (await Get<List<PositionResponse>>(anonymous, api + "/api/positions?municipalitySlug=" + slug)).Single().SeatsAvailable);

            await page.GotoAsync(admin + "/admin/candidates/create");
            await page.GetByLabel("Name", new() { Exact = true }).FillAsync("Integration Candidate " + suffix);
            await page.GetByLabel("Municipality", new() { Exact = true }).SelectOptionAsync(municipalityId.Value.ToString());
            await page.GetByLabel("Position", new() { Exact = true }).SelectOptionAsync(positionId.Value.ToString());
            await page.GetByLabel("Biography", new() { Exact = true }).FillAsync("Live integration biography.");
            await page.GetByLabel("Photograph", new() { Exact = true }).SetInputFilesAsync(new FilePayload
                { Name = "photo.png", MimeType = "image/png", Buffer = ImageTests.Png() });
            await Save(page, "candidate", "candidates"); candidateId = Id(page);
            var candidate = (await Get<List<CandidateResponse>>(anonymous,
                api + $"/api/candidates/search?municipalitySlug={slug}&positionId={positionId}&name={suffix}")).Single();
            Assert.Equal(candidateId, candidate.Id);
            Assert.Equal(municipalityId, candidate.Position.MunicipalityId);
            Assert.StartsWith(admin + "/uploads/candidates/", candidate.PhotoUrl);
            Assert.Equal(200, (await anonymous.GetAsync(candidate.PhotoUrl!)).Status);
            await page.GetByRole(AriaRole.Link, new() { Name = "Edit candidate", Exact = true }).ClickAsync();
            await page.GetByLabel("Biography", new() { Exact = true }).FillAsync("Updated through Blazor and SQL Server.");
            await Save(page, "candidate", "candidates");
            Assert.Equal("Updated through Blazor and SQL Server.", (await Get<CandidateResponse>(anonymous,
                api + $"/api/candidates/{candidateId}?municipalitySlug={slug}")).Profile);

            await Delete(page, admin, "candidates", candidateId.Value, "candidate", "Integration Candidate " + suffix); candidateId = null;
            Assert.Equal(404, (await anonymous.GetAsync(candidate.PhotoUrl!)).Status);
            await Delete(page, admin, "positions", positionId.Value, "position", "Integration Mayor"); positionId = null;
            await Delete(page, admin, "municipalities", municipalityId.Value, "municipality", "Integration " + suffix); municipalityId = null;
            Assert.Equal(404, (await anonymous.GetAsync(api + "/api/municipalities/slug/" + slug)).Status);
            await page.GetByRole(AriaRole.Button, new() { Name = "Sign out", Exact = true }).ClickAsync();
            await page.WaitForURLAsync("**/admin/login?signedOut=1");

            foreach (var (username, city, passwordKey) in new[]
            {
                ("Burnabyadmin", "burnaby", "MEMS_TEST_BURNABY_PASSWORD"),
                ("Surreyadmin", "surrey", "MEMS_TEST_SURREY_PASSWORD"),
                ("Richmondadmin", "richmond", "MEMS_TEST_RICHMOND_PASSWORD")
            })
            {
                await using var member = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
                var memberPage = await member.NewPageAsync();
                var password = Setting(passwordKey);
                await BrowserLogin(memberPage, admin, username, password);
                await Assertions.Expect(memberPage.GetByTestId("municipality-count")).ToHaveTextAsync("1");
                await Assertions.Expect(memberPage.GetByTestId("position-count")).ToHaveTextAsync("3");
                await Assertions.Expect(memberPage.GetByTestId("candidate-count")).ToHaveTextAsync("8");
                await memberPage.GotoAsync(admin + "/admin/municipalities/create");
                await memberPage.WaitForURLAsync("**/admin/access-denied*");
                var foreignCity = city == "burnaby" ? "surrey" : "burnaby";
                var foreign = (await Get<List<CandidateResponse>>(anonymous, api + "/api/candidates?municipalitySlug=" + foreignCity))[0];
                await memberPage.GotoAsync(admin + $"/admin/candidates/{foreign.Id}/edit");
                await Assertions.Expect(memberPage.GetByRole(AriaRole.Alert)).ToContainTextAsync("not found");
                await ApiLogin(member.APIRequest, api, username, password);
                Assert.Equal(8, (await Get<List<CandidateResponse>>(member.APIRequest, api + "/api/candidates")).Count);
                Assert.Equal(403, (await member.APIRequest.GetAsync(api + "/api/candidates?municipalitySlug=" + foreignCity)).Status);
                Assert.Equal(404, (await member.APIRequest.GetAsync(api + $"/api/candidates/{foreign.Id}")).Status);
                var csrf = await Get<CsrfResponse>(member.APIRequest, api + "/api/auth/csrf");
                Assert.Equal(403, (await member.APIRequest.PostAsync(api + "/api/candidates", new()
                {
                    Headers = new Dictionary<string, string> { [csrf.HeaderName] = csrf.RequestToken },
                    DataObject = new CandidateRequest { Name = "Forbidden", Profile = "Profile",
                        MunicipalityId = foreign.MunicipalityId, PositionId = foreign.PositionId }
                })).Status);
            }
        }
        finally
        {
            // Clean only records created by this test. Existing seeded/domain records are never modified.
            if (municipalityId.HasValue)
            {
                await ApiLogin(context.APIRequest, api, "Super", superPassword);
                var csrf = await Get<CsrfResponse>(context.APIRequest, api + "/api/auth/csrf");
                var headers = new Dictionary<string, string> { [csrf.HeaderName] = csrf.RequestToken };
                if (candidateId.HasValue) await Cleanup<CandidateResponse>(context.APIRequest, api + "/api/candidates/" + candidateId, headers, x => x.Revision);
                if (positionId.HasValue) await Cleanup<PositionResponse>(context.APIRequest, api + "/api/positions/" + positionId, headers, x => x.Revision);
                await Cleanup<MunicipalityResponse>(context.APIRequest, api + "/api/municipalities/" + municipalityId, headers, x => x.Revision);
            }
        }
    }

    private static async Task<T> Get<T>(IAPIRequestContext request, string url)
    {
        var response = await request.GetAsync(url);
        Assert.Equal(200, response.Status);
        return JsonSerializer.Deserialize<T>(await response.TextAsync(), Json)!;
    }
    private static int Id(IPage page) => int.Parse(new Uri(page.Url).Segments.Last().Trim('/'));
    private static async Task BrowserLogin(IPage page, string admin, string username, string password)
    {
        await page.GotoAsync(admin + "/admin/login");
        await page.GetByLabel("Username or email").FillAsync(username);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin");
    }
    private static async Task ApiLogin(IAPIRequestContext request, string api, string username, string password)
    {
        var csrf = await Get<CsrfResponse>(request, api + "/api/auth/csrf");
        var response = await request.PostAsync(api + "/api/auth/login", new()
        {
            Headers = new Dictionary<string, string> { [csrf.HeaderName] = csrf.RequestToken },
            DataObject = new { username, password }
        });
        Assert.Equal(200, response.Status);
    }
    private static async Task Save(IPage page, string singular, string plural)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Save " + singular, Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin/" + plural + "/*?saved=true");
    }
    private static async Task Delete(IPage page, string admin, string plural, int id, string singular, string name)
    {
        await page.GotoAsync(admin + $"/admin/{plural}/{id}/delete");
        await page.GetByLabel("I confirm deletion of " + name + ".").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete " + singular, Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin/" + plural + "?deleted=true");
    }
    private static async Task Cleanup<T>(IAPIRequestContext request, string url, Dictionary<string, string> headers, Func<T, string> revision)
    {
        var record = await request.GetAsync(url);
        if (record.Status == 404) return;
        Assert.Equal(200, record.Status);
        var value = JsonSerializer.Deserialize<T>(await record.TextAsync(), Json)!;
        Assert.Equal(204, (await request.DeleteAsync(url + "?revision=" + revision(value), new() { Headers = headers })).Status);
    }
}
