using System.Net;
using System.Net.Http.Json;
using MEMS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MEMS.Api.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task RealCookieLoginNeedsCsrfLogoutRevokesSessionAndNoPasswordsAreReturned()
    {
        await using var factory = new MemsFactory();
        using var client = factory.Client();
        var request = new AdminLoginRequest { Username = "Super", Password = factory.Password };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", request)).StatusCode);
        await MemsFactory.CsrfAsync(client);
        var login = await client.PostAsJsonAsync("/api/auth/login", request);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var headers = string.Join(";", login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-MEMS.Admin=", headers);
        Assert.Contains("secure", headers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", headers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", headers, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(factory.Password, await login.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        await MemsFactory.CsrfAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        using var replay = factory.Client();
        replay.DefaultRequestHeaders.Add("Cookie", headers.Split(';')[0]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task WritesRequireCsrfAndPersistedRoleRevocationTakesEffectImmediately()
    {
        await using var factory = new MemsFactory();
        using var client = await factory.LoginAsync("Burnabyadmin");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/positions", new PositionRequest
            { Name = "Test", SeatsAvailable = 1, MunicipalityId = 3 })).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MEMS.Data.ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.UserName == "Burnabyadmin");
        db.UserRoles.RemoveRange(await db.UserRoles.Where(r => r.UserId == user.Id).ToListAsync());
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task WrongCredentialsAndOrdinaryUsersCannotBecomeAdministrators()
    {
        await using var factory = new MemsFactory();
        using var client = factory.Client();
        await MemsFactory.CsrfAsync(client);
        var bad = await client.PostAsJsonAsync("/api/auth/login", new AdminLoginRequest
            { Username = "Super", Password = "incorrect-" + Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<MEMS.Data.Entities.ApplicationUser>>();
        var ordinary = new MEMS.Data.Entities.ApplicationUser { UserName = "ordinary", Email = "ordinary@example.com" };
        Assert.True((await users.CreateAsync(ordinary, factory.Password)).Succeeded);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new AdminLoginRequest { Username = "ordinary", Password = factory.Password })).StatusCode);
    }
}
