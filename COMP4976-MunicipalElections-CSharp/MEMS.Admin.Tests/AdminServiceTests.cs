using System.ComponentModel.DataAnnotations;
using MEMS.Admin.Models;
using MEMS.Data;
using MEMS.Data.Entities;
using MEMS.Data.Services;
using MEMS.Shared.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MEMS.Admin.Tests;

public sealed class AdminServiceTests
{

    [Fact]
    public async Task SuperAdminCanReassignCandidatesOnlyToMatchingDestinationPositions()
    {
        await using var factory = new AdminFactory();
        await factory.AsUserAsync("Super", async (data, _) =>
        {
            var municipalities = await data.MunicipalitiesAsync();
            var source = municipalities.Single(m => m.Slug == "burnaby");
            var target = municipalities.Single(m => m.Slug == "surrey");
            var candidate = (await data.CandidatesAsync(source.Id))[0];
            var destination = (await data.PositionsAsync(target.Id))[0];
            var input = new CandidateRequest { Name = candidate.Name, Profile = candidate.Profile,
                MunicipalityId = target.Id, PositionId = candidate.PositionId };
            await Assert.ThrowsAsync<ValidationException>(() => data.SaveCandidateAsync(candidate.Id, input, candidate.Revision, null));
            input.PositionId = destination.Id;
            await data.SaveCandidateAsync(candidate.Id, input, candidate.Revision, null);
            var moved = await data.CandidateAsync(candidate.Id);
            Assert.Equal(target.Id, moved.MunicipalityId);
            Assert.Equal(destination.Id, moved.PositionId);
            Assert.Equal(candidate.DateCreated, moved.DateCreated);
            return true;
        });
    }
    [Theory]
    [InlineData("Super", 5, 15, 32)]
    [InlineData("Burnabyadmin", 1, 3, 8)]
    [InlineData("Surreyadmin", 1, 3, 8)]
    [InlineData("Richmondadmin", 1, 3, 8)]
    public async Task DashboardCountsAndQueriesRespectCurrentTenant(string user, int municipalities, int positions, int candidates)
    {
        await using var factory = new AdminFactory();
        await factory.AsUserAsync(user, async (data, _) =>
        {
            var counts = await data.DashboardAsync();
            Assert.Equal(municipalities, counts.Municipalities); Assert.Equal(positions, counts.Positions); Assert.Equal(candidates, counts.Candidates);
            await Task.WhenAll(data.CandidatesAsync(), data.PositionsAsync());
            Assert.Equal(candidates, (await data.CandidatesAsync()).Count);
            return true;
        });
    }

    [Fact]
    public async Task FullCrudPreservesMetadataAndRejectsStaleRevisions()
    {
        await using var factory = new AdminFactory();
        await factory.AsUserAsync("Super", async (data, _) =>
        {
            var input = new MunicipalityForm { Name = "Admin test city", Slug = "admin-test-city", Province = "BC", ElectionDate = new DateOnly(2026, 10, 17) };
            var municipalityId = await data.SaveMunicipalityAsync(null, input, null, null);
            await Assert.ThrowsAsync<RecordConflictException>(() => data.SaveMunicipalityAsync(null, input, null, null));
            var municipality = await data.MunicipalityAsync(municipalityId);
            input.Description = "Edited description";
            await data.SaveMunicipalityAsync(municipalityId, input, municipality.Revision, null);
            municipality = await data.MunicipalityAsync(municipalityId);
            var positionId = await data.SavePositionAsync(null, new PositionRequest { Name = "Mayor", SeatsAvailable = 1, MunicipalityId = municipalityId }, null);
            var position = await data.PositionAsync(positionId);
            await data.SavePositionAsync(positionId, new PositionRequest { Name = "Council", SeatsAvailable = 2, MunicipalityId = municipalityId }, position.Revision);
            position = await data.PositionAsync(positionId);
            var candidateInput = new CandidateRequest { Name = "Test candidate", Profile = "Profile", PositionId = positionId, MunicipalityId = municipalityId, Email = "person@example.com", Website = "https://example.com" };
            var candidateId = await data.SaveCandidateAsync(null, candidateInput, null, null);
            var candidate = await data.CandidateAsync(candidateId);
            var created = candidate.DateCreated;
            candidateInput.Profile = "Edited profile";
            await data.SaveCandidateAsync(candidateId, candidateInput, candidate.Revision, null);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => data.SaveCandidateAsync(candidateId, candidateInput, candidate.Revision, null));
            candidate = await data.CandidateAsync(candidateId);
            Assert.Equal(created, candidate.DateCreated); Assert.Equal("Edited profile", candidate.Profile);
            await Assert.ThrowsAsync<RecordConflictException>(() => data.DeleteMunicipalityAsync(municipalityId, municipality.Revision));
            await Assert.ThrowsAsync<RecordConflictException>(() => data.DeletePositionAsync(positionId, position.Revision));
            await data.DeleteCandidateAsync(candidateId, candidate.Revision);
            await data.DeletePositionAsync(positionId, position.Revision);
            await data.DeleteMunicipalityAsync(municipalityId, municipality.Revision);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => data.CandidateAsync(candidateId));
            return true;
        });
    }

    [Fact]
    public async Task MemberCannotForgeTenantRecordIdsOrSuperAdminOperations()
    {
        await using var factory = new AdminFactory();
        await factory.AsUserAsync("Burnabyadmin", async (data, services) =>
        {
            var own = (await data.MunicipalitiesAsync()).Single();
            await using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var foreign = await db.Candidates.IgnoreQueryFilters().FirstAsync(c => c.MunicipalityId != own.Id);
            var ownPositionId = (await data.PositionsAsync())[0].Id;
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => data.MunicipalityAsync(foreign.MunicipalityId));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => data.CandidateAsync(foreign.CandidateId));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => data.DeleteCandidateAsync(foreign.CandidateId, foreign.Revision));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => data.DeleteMunicipalityAsync(own.Id, own.Revision));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => data.SavePositionAsync(null, new PositionRequest { MunicipalityId = foreign.MunicipalityId, Name = "Foreign", SeatsAvailable = 1 }, null));
            await Assert.ThrowsAsync<ValidationException>(() => data.SaveCandidateAsync(null, new CandidateRequest
                { Name = "Invalid", Profile = "Profile", MunicipalityId = own.Id, PositionId = foreign.PositionId }, null, null));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => data.SaveCandidateAsync(foreign.CandidateId, new CandidateRequest
                { Name = "Forged ID", Profile = "Profile", MunicipalityId = own.Id, PositionId = ownPositionId }, foreign.Revision, null));
            var id = await data.SaveCandidateAsync(null, new CandidateRequest
                { Name = "Allowed candidate", Profile = "Profile", MunicipalityId = own.Id, PositionId = (await data.PositionsAsync())[0].Id }, null, null);
            Assert.Equal(own.Id, (await data.CandidateAsync(id)).MunicipalityId);
            return true;
        });
    }

    [Fact]
    public async Task ActiveCircuitOperationsRejectRevokedIdentityWithoutWaitingForRevalidationTimer()
    {
        await using var factory = new AdminFactory();
        await factory.AsUserAsync("Burnabyadmin", async (data, services) =>
        {
            Assert.Equal(8, (await data.CandidatesAsync()).Count);
            await using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByNameAsync("Burnabyadmin"))!;
            Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => data.DashboardAsync());
            return true;
        });
    }

    [Fact]
    public async Task ServerValidationRejectsBadDatesUrlsEmailsAndRequiredFields()
    {
        await using var factory = new AdminFactory();
        await factory.AsUserAsync("Super", async (data, _) =>
        {
            await Assert.ThrowsAsync<ValidationException>(() => data.SaveMunicipalityAsync(null, new MunicipalityForm
                { Name = "Bad date", Slug = "bad-date", Province = "BC", ElectionDate = new DateOnly(1900, 1, 1) }, null, null));
            var municipality = (await data.MunicipalitiesAsync())[0];
            var position = (await data.PositionsAsync(municipality.Id))[0];
            foreach (var invalid in new[] { new CandidateRequest { Name = "", Profile = "Profile", MunicipalityId = municipality.Id, PositionId = position.Id },
                new CandidateRequest { Name = "Invalid", Profile = "Profile", Email = "bad", MunicipalityId = municipality.Id, PositionId = position.Id },
                new CandidateRequest { Name = "Invalid", Profile = "Profile", Website = "javascript:bad", MunicipalityId = municipality.Id, PositionId = position.Id } })
                await Assert.ThrowsAsync<ValidationException>(() => data.SaveCandidateAsync(null, invalid, null, null));
            return true;
        });
    }
}
