using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LegacyUser = MunicipalElections.Web.Models.ApplicationUser;
using LegacyDb = MunicipalElections.Web.Data.ApplicationDbContext;

namespace MEMS.Api.Tests;

public sealed class LegacyApiTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    [Fact]
    public async Task CityFilteringPreservesSwiftResponseShapeAndSupportsChangingCity()
    {
        await using var factory = new LegacyFactory();
        using var client = await factory.SeededClientAsync();
        var directory = await Json(await client.GetAsync("/api/municipalities"));
        Assert.Equal(3, directory.GetArrayLength());
        var burnaby = directory.EnumerateArray().Single(m => m.GetProperty("name").GetString() == "Burnaby");
        var burnabyId = burnaby.GetProperty("id").GetInt32();
        Assert.Equal(JsonValueKind.String, burnaby.GetProperty("electionDate").ValueKind);
        Assert.Equal(1, (await Json(await client.GetAsync("/api/municipalities?city=Burnaby"))).GetArrayLength());
        Assert.Equal(12, (await Json(await client.GetAsync("/api/candidates"))).GetArrayLength());
        foreach (var city in new[] { "burnaby", "pitt-meadows", "maple-ridge" })
        {
            var municipality = (await Json(await client.GetAsync("/api/municipalities?city=" + city)))[0];
            var id = municipality.GetProperty("id").GetInt32();
            var positions = await Json(await client.GetAsync("/api/positions?city=" + city));
            Assert.Equal(3, positions.GetArrayLength());
            Assert.All(positions.EnumerateArray(), p => Assert.Equal(id, p.GetProperty("municipalityId").GetInt32()));
            var candidates = await Json(await client.GetAsync("/api/candidates?city=" + city));
            Assert.Equal(4, candidates.GetArrayLength());
            foreach (var candidate in candidates.EnumerateArray())
            {
                foreach (var property in new[] { "id", "name", "profile", "photoUrl", "website", "email", "dateCreated",
                    "municipalityId", "municipalityName", "positionId", "positionTitle", "municipality", "position" })
                    Assert.True(candidate.TryGetProperty(property, out _), "Missing Swift-compatible property: " + property);
                Assert.Equal(id, candidate.GetProperty("municipalityId").GetInt32());
                Assert.Equal(id, candidate.GetProperty("municipality").GetProperty("id").GetInt32());
                Assert.Equal(id, candidate.GetProperty("position").GetProperty("municipalityId").GetInt32());
                Assert.Equal(JsonValueKind.String, candidate.GetProperty("profile").ValueKind);
            }
        }
        var search = await Json(await client.GetAsync($"/api/candidates/search?municipalityId={burnabyId}&name=reese"));
        Assert.Single(search.EnumerateArray());
        var candidateId = search[0].GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/candidates/{candidateId}?city=pitt-meadows")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/municipalities/{burnabyId}?city=pitt-meadows")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/positions?municipalityId={burnabyId}&city=pitt-meadows")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/candidates?city=burnaby&positionId=9999")).StatusCode);
    }

    [Theory]
    [InlineData("/api/candidates?municipalityId=-1", 400)]
    [InlineData("/api/candidates?municipalityId=", 400)]
    [InlineData("/api/candidates?city=", 400)]
    [InlineData("/api/positions?municipalityId=text", 400)]
    [InlineData("/api/municipalities?municipalityId=9999", 404)]
    [InlineData("/api/candidates?city=unknown-city", 404)]
    [InlineData("/api/positions/0", 400)]
    public async Task InvalidLegacySelectorsAreRejected(string path, int status)
    {
        await using var factory = new LegacyFactory();
        using var client = await factory.SeededClientAsync();
        Assert.Equal(status, (int)(await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task RegistrationLoginJwtAndSavedCandidateOwnershipRemainCompatible()
    {
        await using var factory = new LegacyFactory();
        using var client = await factory.SeededClientAsync();
        var password = "Aa9!" + Guid.NewGuid().ToString("N");
        async Task<(string Token, string Id)> RegisterLogin(string suffix)
        {
            var email = Guid.NewGuid().ToString("N") + suffix + "@example.com";
            var register = await client.PostAsJsonAsync("/api/auth/register",
                new { username = email, email, password, fullName = "HTTP test user" });
            Assert.Equal(HttpStatusCode.Created, register.StatusCode);
            var id = (await Json(register)).GetProperty("id").GetString()!;
            var login = await client.PostAsJsonAsync("/api/auth/login", new { username = email, password });
            var token = (await Json(login)).GetProperty("token").GetString()!;
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            Assert.Equal(id, jwt.Subject);
            Assert.DoesNotContain(jwt.Claims, c => c.Type is "role" or "roles");
            return (token, id);
        }
        var first = await RegisterLogin("first");
        var second = await RegisterLogin("second");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/savedcandidates")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.Token);
        var candidates = await Json(await client.GetAsync("/api/candidates?city=burnaby"));
        var candidateId = candidates[0].GetProperty("id").GetInt32();
        var save = await client.PostAsJsonAsync("/api/savedcandidates", new { candidateId });
        Assert.Equal(HttpStatusCode.Created, save.StatusCode);
        var saved = await Json(save);
        Assert.Equal(candidateId, saved.GetProperty("candidateId").GetInt32());
        Assert.True(saved.GetProperty("candidate").TryGetProperty("profile", out _));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.Token);
        Assert.Equal(0, (await Json(await client.GetAsync("/api/savedcandidates"))).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/savedcandidates/" + saved.GetProperty("id").GetInt32())).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<LegacyUser>>();
        Assert.True((await users.UpdateSecurityStampAsync((await users.FindByIdAsync(first.Id))!)).Succeeded);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/savedcandidates")).StatusCode);

        await using var mems = new MemsFactory();
        using var memsClient = mems.Client();
        memsClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await memsClient.GetAsync("/api/auth/me")).StatusCode);
        var bad = await client.PostAsJsonAsync("/api/auth/login", new { username = "unknown", password });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
    }
}
