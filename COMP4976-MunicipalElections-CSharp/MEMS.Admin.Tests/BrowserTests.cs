using Microsoft.Playwright;

namespace MEMS.Admin.Tests;

[CollectionDefinition("Browser verification", DisableParallelization = true)]
public sealed class BrowserVerificationCollection;

[Collection("Browser verification")]
public sealed class BrowserTests
{
    [Fact]
    public async Task InteractiveAdminCrudUploadsValidationPermissionsAndResponsiveLayout()
    {
        await using var factory = new AdminFactory(browser: true);
        var address = factory.BrowserAddress;
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            { Headless = true, ExecutablePath = Environment.GetEnvironmentVariable("MEMS_TEST_CHROMIUM") });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            { IgnoreHTTPSErrors = true, ViewportSize = new ViewportSize { Width = 1366, Height = 900 } });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(20000);
        await page.GotoAsync(address + "/admin/login");
        await page.GetByLabel("Username or email").FillAsync("Super");
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(factory.Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin");
        await Assertions.Expect(page.GetByTestId("municipality-count")).ToHaveTextAsync("5");
        await Assertions.Expect(page.GetByTestId("candidate-count")).ToHaveTextAsync("32");
        var artifacts = Path.Combine("TestResults", "browser");
        Directory.CreateDirectory(artifacts);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifacts, "admin-desktop.png"), FullPage = true });
        await page.SetViewportSizeAsync(390, 844);
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifacts, "admin-mobile.png"), FullPage = true });
        await page.SetViewportSizeAsync(1366, 900);

        await page.GotoAsync(address + "/admin/municipalities/create");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save municipality", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator(".validation-errors")).ToContainTextAsync("required");
        await page.GetByLabel("Name", new() { Exact = true }).FillAsync("Browser Test City");
        await page.GetByLabel("Tenant URL slug").FillAsync("browser-test-city");
        await page.GetByLabel("Province", new() { Exact = true }).FillAsync("BC");
        await page.GetByLabel("Election date").FillAsync("2026-10-17");
        await page.GetByLabel("Logo", new() { Exact = true }).SetInputFilesAsync(new FilePayload
            { Name = "logo.png", MimeType = "image/png", Buffer = ImageTests.Png() });
        await page.GetByRole(AriaRole.Button, new() { Name = "Save municipality", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin/municipalities/*?saved=true");
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByText("Changes saved successfully.", new() { Exact = true })).ToBeVisibleAsync();
        var municipalityId = new Uri(page.Url).Segments.Last().Trim('/');
        await page.GetByRole(AriaRole.Link, new() { Name = "Edit municipality", Exact = true }).ClickAsync();
        await page.GetByLabel("Description", new() { Exact = true }).FillAsync("Edited from the real browser.");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save municipality", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin/municipalities/*?saved=true");
        await Assertions.Expect(page.GetByText("Edited from the real browser.", new() { Exact = true })).ToBeVisibleAsync();

        await page.GotoAsync(address + "/admin/positions/create");
        await page.GetByLabel("Name", new() { Exact = true }).FillAsync("Browser Mayor");
        await page.GetByLabel("Available seats").FillAsync("1");
        await page.GetByLabel("Municipality", new() { Exact = true }).SelectOptionAsync(municipalityId);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save position", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin/positions/*?saved=true");
        var positionId = new Uri(page.Url).Segments.Last().Trim('/');

        await page.GotoAsync(address + "/admin/candidates/create");
        await page.GetByLabel("Name", new() { Exact = true }).FillAsync("Browser Candidate");
        await page.GetByLabel("Municipality", new() { Exact = true }).SelectOptionAsync(municipalityId);
        await page.GetByLabel("Position", new() { Exact = true }).SelectOptionAsync(positionId);
        await page.GetByLabel("Biography", new() { Exact = true }).FillAsync("Biography entered through Blazor.");
        await page.GetByLabel("Website URL").FillAsync("https://example.com");
        await page.GetByLabel("Email", new() { Exact = true }).FillAsync("candidate@example.com");
        await page.GetByLabel("Photograph", new() { Exact = true }).SetInputFilesAsync(new FilePayload
            { Name = "candidate.png", MimeType = "image/png", Buffer = ImageTests.Png() });
        await page.GetByRole(AriaRole.Button, new() { Name = "Save candidate", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin/candidates/*?saved=true");
        var candidateId = new Uri(page.Url).Segments.Last().Trim('/');
        var photo = page.GetByRole(AriaRole.Img, new() { Name = "Photograph of Browser Candidate", Exact = true });
        await Assertions.Expect(photo).ToBeVisibleAsync();
        Assert.True(await photo.EvaluateAsync<bool>("img => img.complete && img.naturalWidth > 0"));
        await page.GetByRole(AriaRole.Link, new() { Name = "Edit candidate", Exact = true }).ClickAsync();
        await page.GetByLabel("Biography", new() { Exact = true }).FillAsync("Edited biography.");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save candidate", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin/candidates/*?saved=true");
        await Assertions.Expect(page.GetByText("Edited biography.", new() { Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Delete", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Delete candidate", Exact = true })).ToBeDisabledAsync();
        await page.GetByLabel("I confirm deletion of Browser Candidate.").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete candidate", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin/candidates?deleted=true");

        await page.GotoAsync(address + "/admin/positions/" + positionId + "/edit");
        await page.GetByLabel("Available seats").FillAsync("2");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save position", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin/positions/*?saved=true");
        await page.GetByRole(AriaRole.Link, new() { Name = "Delete", Exact = true }).ClickAsync();
        await page.GetByLabel("I confirm deletion of Browser Mayor.").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete position", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin/positions?deleted=true");
        await page.GotoAsync(address + "/admin/municipalities/" + municipalityId + "/delete");
        await page.GetByLabel("I confirm deletion of Browser Test City.").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete municipality", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin/municipalities?deleted=true");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign out", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin/login?signedOut=1");

        await page.GetByLabel("Username or email").FillAsync("Burnabyadmin");
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(factory.Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/admin");
        await Assertions.Expect(page.GetByTestId("municipality-count")).ToHaveTextAsync("1");
        await Assertions.Expect(page.GetByTestId("candidate-count")).ToHaveTextAsync("8");
        await page.GotoAsync(address + "/admin/municipalities/create");
        await page.WaitForURLAsync("**/admin/access-denied*");
        await page.GotoAsync(address + "/admin/candidates/1/edit");
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("not found");
        await Assertions.Expect(page.GetByLabel("Biography", new() { Exact = true })).ToHaveCountAsync(0);
    }
}
