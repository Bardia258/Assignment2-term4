using System.Net;
using MEMS.Admin.Security;

namespace MEMS.Admin.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task HttpLoginAndLogoutRequireAntiforgeryAndIssueSecureCookies()
    {
        await using var factory = new AdminFactory();
        using var client = factory.Client();
        var protectedPage = await client.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
        Assert.Contains("/admin/login", protectedPage.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/admin/account/sign-in",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Username"] = "Super", ["Password"] = factory.Password }))).StatusCode);
        using var signedIn = await factory.SignInAsync("Super");
        var dashboard = await signedIn.GetStringAsync("/admin");
        Assert.Contains("Administrative dashboard", dashboard);
        Assert.Contains("data-testid=\"candidate-count\">32", dashboard);
        var response = await signedIn.PostAsync("/admin/account/sign-out", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = AdminFactory.AntiforgeryToken(dashboard) }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await signedIn.GetAsync("/admin")).StatusCode);
    }

    [Theory]
    [InlineData("https://evil.example/admin")]
    [InlineData("//evil.example/admin")]
    [InlineData("/admin\\evil")]
    [InlineData("/administrator")]
    public void ReturnUrlsCannotRedirectOutsideAdmin(string value) => Assert.Equal("/admin", AdminAuthentication.SafeReturnUrl(value));

    [Fact]
    public async Task MemberCannotNavigateToSuperAdminFormsAndForeignDetailsDoNotLeak()
    {
        await using var factory = new AdminFactory();
        using var client = await factory.SignInAsync("Burnabyadmin");
        var denied = await client.GetAsync("/admin/municipalities/create");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/admin/access-denied", denied.Headers.Location!.OriginalString);
        var foreign = await client.GetStringAsync("/admin/candidates/1");
        Assert.DoesNotContain("Avery Brookfield", foreign);
        Assert.Contains("not found", foreign, StringComparison.OrdinalIgnoreCase);
        var candidates = await client.GetStringAsync("/admin/candidates");
        Assert.Contains("Reese Hazelbrook", candidates);
        Assert.DoesNotContain("Avery Pine", candidates);
    }
}
