using System.Net;
using System.Net.Http.Json;
using MEMS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MEMS.Api.Tests;

public sealed class AdminApiTests
{
    [Theory]
    [InlineData("1999-12-31")]
    [InlineData("2101-01-01")]
    public async Task ApiEnforcesTheOriginalAdministrativeElectionDateRange(string date)
    {
        await using var factory = new MemsFactory();
        using var client = await factory.LoginAsync("Super");
        var response = await client.PostAsJsonAsync("/api/municipalities", new MunicipalityRequest
        {
            Name = "Invalid election date", Slug = "invalid-date", Province = "BC", ElectionDate = DateOnly.Parse(date)
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("2000 and 2100", await response.Content.ReadAsStringAsync());
    }

    private static CandidateRequest Candidate(int municipalityId, int positionId) => new()
    {
        Name = "HTTP test candidate", Profile = "A test candidate profile.",
        MunicipalityId = municipalityId, PositionId = positionId,
        Website = "https://example.com/test", Email = "candidate@example.com", PhotoFileName = "test.png"
    };

    [Fact]
    public async Task SuperAdminCanCreateUpdateAndDeleteAllThreeResources()
    {
        await using var factory = new MemsFactory();
        using var client = await factory.LoginAsync("Super");
        var municipalityInput = new MunicipalityRequest
        {
            Name = "API Test City", Slug = "api-test-city", Province = "BC", ElectionDate = new DateOnly(2026, 10, 17)
        };
        var created = await client.PostAsJsonAsync("/api/municipalities", municipalityInput);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var municipality = (await created.Content.ReadFromJsonAsync<MunicipalityResponse>())!;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/municipalities", municipalityInput)).StatusCode);
        var update = await client.PutAsJsonAsync($"/api/municipalities/{municipality.Id}", new MunicipalityUpdateRequest
        {
            Name = "Edited City", Slug = municipality.Slug, Province = "BC", ElectionDate = municipalityInput.ElectionDate,
            Revision = municipality.Revision
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        municipality = (await update.Content.ReadFromJsonAsync<MunicipalityResponse>())!;
        var positionCreated = await client.PostAsJsonAsync("/api/positions", new PositionRequest
            { Name = "Mayor", SeatsAvailable = 1, MunicipalityId = municipality.Id });
        Assert.Equal(HttpStatusCode.Created, positionCreated.StatusCode);
        var position = (await positionCreated.Content.ReadFromJsonAsync<PositionResponse>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/positions", new PositionRequest
            { Name = "mayor", SeatsAvailable = 1, MunicipalityId = municipality.Id })).StatusCode);
        var positionUpdated = await client.PutAsJsonAsync($"/api/positions/{position.Id}", new PositionUpdateRequest
            { Name = "Council", SeatsAvailable = 2, MunicipalityId = municipality.Id, Revision = position.Revision });
        Assert.Equal(HttpStatusCode.OK, positionUpdated.StatusCode);
        position = (await positionUpdated.Content.ReadFromJsonAsync<PositionResponse>())!;
        var candidateCreated = await client.PostAsJsonAsync("/api/candidates", Candidate(municipality.Id, position.Id));
        Assert.Equal(HttpStatusCode.Created, candidateCreated.StatusCode);
        var candidate = (await candidateCreated.Content.ReadFromJsonAsync<CandidateResponse>())!;
        Assert.EndsWith("/uploads/candidates/test.png", candidate.PhotoUrl);
        var candidateInput = new CandidateUpdateRequest
        {
            Name = "Edited candidate", Profile = "Edited profile", MunicipalityId = municipality.Id,
            PositionId = position.Id, Revision = candidate.Revision
        };
        var candidateUpdated = await client.PutAsJsonAsync($"/api/candidates/{candidate.Id}", candidateInput);
        Assert.Equal(HttpStatusCode.OK, candidateUpdated.StatusCode);
        var current = (await candidateUpdated.Content.ReadFromJsonAsync<CandidateResponse>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/candidates/{candidate.Id}", candidateInput)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/positions/{position.Id}?revision={position.Revision}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/candidates/{candidate.Id}?revision={current.Revision}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/positions/{position.Id}?revision={position.Revision}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/municipalities/{municipality.Id}?revision={municipality.Revision}")).StatusCode);
    }

    [Fact]
    public async Task MunicipalityAdminCannotForgeFiltersOwnersOrIds()
    {
        await using var factory = new MemsFactory();
        using var anonymous = factory.Client();
        var directory = (await anonymous.GetFromJsonAsync<List<MunicipalityResponse>>("/api/municipalities"))!;
        var burnaby = directory.Single(m => m.Slug == "burnaby");
        var surrey = directory.Single(m => m.Slug == "surrey");
        var foreign = (await anonymous.GetFromJsonAsync<List<CandidateResponse>>("/api/candidates?municipalitySlug=surrey"))![0];
        using var client = await factory.LoginAsync("Burnabyadmin");
        var own = (await client.GetFromJsonAsync<List<CandidateResponse>>("/api/candidates"))!;
        Assert.Equal(8, own.Count);
        Assert.All(own, c => Assert.Equal(burnaby.Id, c.MunicipalityId));
        Assert.Single((await client.GetFromJsonAsync<List<MunicipalityResponse>>("/api/municipalities"))!);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/candidates?municipalitySlug=surrey")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/municipalities/{surrey.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/candidates/{foreign.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/candidates", Candidate(surrey.Id, foreign.PositionId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/candidates", Candidate(burnaby.Id, foreign.PositionId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/candidates/{foreign.Id}", new CandidateUpdateRequest
        { Name = "Forged ID", Profile = "Profile", MunicipalityId = burnaby.Id, PositionId = own[0].PositionId, Revision = foreign.Revision })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/candidates/{foreign.Id}?revision={foreign.Revision}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/municipalities", new MunicipalityRequest
        { Name = "Forbidden", Slug = "forbidden", Province = "BC", ElectionDate = new DateOnly(2026, 10, 17) })).StatusCode);
        var created = await client.PostAsJsonAsync("/api/candidates", Candidate(burnaby.Id, own[0].PositionId));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task ValidationRejectsBadEmailUrlLengthsAndMissingRevision()
    {
        await using var factory = new MemsFactory();
        using var client = await factory.LoginAsync("Burnabyadmin");
        var candidates = (await client.GetFromJsonAsync<List<CandidateResponse>>("/api/candidates"))!;
        var input = Candidate(candidates[0].MunicipalityId, candidates[0].PositionId);
        foreach (var invalid in new Action<CandidateRequest>[] { r => r.Email = "bad", r => r.Website = "javascript:bad",
            r => r.Name = new string('x', 101), r => r.Profile = "", r => r.PhotoFileName = "../escape.png" })
        {
            var value = Candidate(input.MunicipalityId, input.PositionId); invalid(value);
            var result = await client.PostAsJsonAsync("/api/candidates", value);
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
            Assert.Contains("errors", await result.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/candidates/{candidates[0].Id}", input)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/candidates/{candidates[0].Id}")).StatusCode);
    }
}
