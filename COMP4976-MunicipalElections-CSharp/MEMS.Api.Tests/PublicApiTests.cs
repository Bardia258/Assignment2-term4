using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MEMS.Shared.Contracts;

namespace MEMS.Api.Tests;

public sealed class PublicApiTests
{
    [Fact]
    public async Task PublicDirectoryAndElectionReadsStayInsideRequestedMunicipality()
    {
        await using var factory = new MemsFactory();
        using var client = factory.Client();
        var municipalities = (await client.GetFromJsonAsync<List<MunicipalityResponse>>("/api/municipalities"))!;
        var burnaby = municipalities.Single(m => m.Slug == "burnaby");
        var surrey = municipalities.Single(m => m.Slug == "surrey");
        Assert.Equal(5, municipalities.Count);
        Assert.Equal(burnaby.Id, (await client.GetFromJsonAsync<MunicipalityResponse>("/api/municipalities/slug/burnaby"))!.Id);
        var candidates = (await client.GetFromJsonAsync<List<CandidateResponse>>("/api/candidates?municipalitySlug=burnaby"))!;
        Assert.Equal(8, candidates.Count);
        Assert.All(candidates, c =>
        {
            Assert.Equal(burnaby.Id, c.MunicipalityId);
            Assert.Equal(burnaby.Id, c.Municipality.Id);
            Assert.Equal(burnaby.Id, c.Position.MunicipalityId);
        });
        var positions = (await client.GetFromJsonAsync<List<PositionResponse>>($"/api/positions?municipalityId={burnaby.Id}"))!;
        Assert.Equal(3, positions.Count);
        var mayor = positions.Single(p => p.Name == "Mayor");
        var filtered = (await client.GetFromJsonAsync<List<CandidateResponse>>($"/api/candidates/search?municipalityId={burnaby.Id}&positionId={mayor.Id}&name=reese"))!;
        Assert.Single(filtered);
        Assert.Equal("Reese Hazelbrook", filtered[0].Name);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/candidates/{candidates[0].Id}?municipalityId={surrey.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/positions/{mayor.Id}?municipalityId={surrey.Id}")).StatusCode);
        var surreyCandidates = (await client.GetFromJsonAsync<List<CandidateResponse>>("/api/candidates?municipalitySlug=surrey"))!;
        Assert.All(surreyCandidates, c => Assert.Equal(surrey.Id, c.MunicipalityId));
    }

    [Theory]
    [InlineData("/api/candidates", 400)]
    [InlineData("/api/positions", 400)]
    [InlineData("/api/candidates?municipalityId=", 400)]
    [InlineData("/api/candidates?municipalitySlug=", 400)]
    [InlineData("/api/candidates?municipalityId=0", 400)]
    [InlineData("/api/candidates?municipalityId=not-a-number", 400)]
    [InlineData("/api/candidates?municipalityId=99999", 404)]
    [InlineData("/api/candidates?municipalitySlug=missing-city", 404)]
    [InlineData("/api/candidates?municipalitySlug=INVALID", 400)]
    [InlineData("/api/candidates?municipalitySlug=burnaby&positionId=99999", 404)]
    [InlineData("/api/candidates?municipalitySlug=burnaby&name=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 400)]
    [InlineData("/api/municipalities/0", 400)]
    public async Task InvalidSelectorsReturnSafeProblemDetails(string path, int expected)
    {
        await using var factory = new MemsFactory();
        using var client = factory.Client();
        var response = await client.GetAsync(path);
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        var problem = JsonDocument.Parse(json).RootElement;
        Assert.Equal(expected, problem.GetProperty("status").GetInt32());
        Assert.DoesNotContain("StackTrace", json);
        Assert.DoesNotContain("ConnectionStrings", json);
    }

    [Fact]
    public async Task AnonymousWritesAreDeniedAndOpenApiPublishesContracts()
    {
        await using var factory = new MemsFactory();
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/candidates", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var spec = await client.GetStringAsync("/openapi/v1.json");
        Assert.Contains("/api/candidates", spec);
        Assert.Contains("/api/auth/login", spec);
        Assert.Contains("CandidateRequest", spec);
        Assert.Contains("AdminCookie", spec);
        Assert.Contains("CsrfHeader", spec);
        Assert.Contains("stale revision", spec, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MunicipalitySlug", spec, StringComparison.OrdinalIgnoreCase);
    }
}
