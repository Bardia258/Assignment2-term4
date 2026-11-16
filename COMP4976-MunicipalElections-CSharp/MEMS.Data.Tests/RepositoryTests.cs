using System.ComponentModel.DataAnnotations;
using MEMS.Data.Entities;
using MEMS.Data.Seeding;
using MEMS.Data.Services;
using MEMS.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data.Tests;

public sealed class RepositoryTests
{
    [Fact]
    public async Task ReadsResolveIdAndSlugAndCannotReturnOtherTenantsRecords()
    {
        await using var db = TestDatabase.InMemory();
        await DevelopmentSeeder.RunAsync(db, TestDatabase.SeedOptions());
        var repository = new ElectionRepository(db);
        var all = MunicipalityScope.ForSuperAdmin();
        var burnaby = (await repository.GetMunicipalityBySlugAsync(all, " BURNABY "))!;
        var surrey = (await repository.GetMunicipalityBySlugAsync(all, "surrey"))!;
        var scope = MunicipalityScope.ForMunicipality(burnaby.MunicipalityId);
        Assert.Equal(burnaby.MunicipalityId, (await repository.GetMunicipalityByIdAsync(scope, burnaby.MunicipalityId))!.MunicipalityId);
        Assert.Null(await repository.GetMunicipalityByIdAsync(scope, surrey.MunicipalityId));
        Assert.Null(await repository.GetMunicipalityBySlugAsync(scope, "surrey"));
        Assert.Single(await repository.GetMunicipalitiesAsync(scope));
        Assert.Equal(5, (await repository.GetMunicipalitiesAsync(all)).Count);
        var positions = await repository.GetPositionsAsync(scope);
        Assert.Equal(3, positions.Count);
        Assert.All(positions, p => Assert.Equal(burnaby.MunicipalityId, p.MunicipalityId));
        var candidates = await repository.GetCandidatesAsync(scope);
        Assert.Equal(8, candidates.Count);
        Assert.All(candidates, c => Assert.Equal(burnaby.MunicipalityId, c.MunicipalityId));
        var surreyCandidate = await db.Candidates.FirstAsync(c => c.MunicipalityId == surrey.MunicipalityId);
        Assert.Null(await repository.GetCandidateAsync(scope, surreyCandidate.CandidateId));
        Assert.Null(await repository.GetPositionAsync(scope, surreyCandidate.PositionId));
        Assert.Empty(await repository.GetCandidatesAsync(scope, surreyCandidate.PositionId));
        Assert.All(await repository.GetCandidatesAsync(scope, search: "Reese"), c =>
            Assert.Equal(burnaby.MunicipalityId, c.MunicipalityId));
    }

    [Fact]
    public async Task WritesRejectForeignScopesAndCrossMunicipalityPositions()
    {
        await using var db = TestDatabase.InMemory();
        await DevelopmentSeeder.RunAsync(db, TestDatabase.SeedOptions());
        var burnaby = await db.Municipalities.SingleAsync(m => m.Slug == "burnaby");
        var surrey = await db.Municipalities.SingleAsync(m => m.Slug == "surrey");
        var foreign = await db.Candidates.FirstAsync(c => c.MunicipalityId == surrey.MunicipalityId);
        var repository = new ElectionRepository(db);
        var scope = MunicipalityScope.ForMunicipality(burnaby.MunicipalityId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.AddPositionAsync(scope,
            new Position { Name = "Foreign position", MunicipalityId = surrey.MunicipalityId, SeatsAvailable = 1 }));
        await Assert.ThrowsAsync<ValidationException>(() => repository.AddCandidateAsync(scope, new Candidate
        {
            MunicipalityId = burnaby.MunicipalityId, PositionId = foreign.PositionId,
            Name = "New candidate", Profile = "A profile."
        }));
        Assert.False(await repository.DeleteCandidateAsync(scope, foreign.CandidateId, foreign.Revision));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.UpdateCandidateAsync(scope, foreign));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.AddMunicipalityAsync(scope, new Municipality()));
        Assert.Equal(32, await db.Candidates.CountAsync());
    }

    [Fact]
    public async Task CrudIgnoresUntrustedGraphsAndKeepsCreationDateAndSeedKey()
    {
        await using var db = TestDatabase.InMemory();
        var repository = new ElectionRepository(db);
        var super = MunicipalityScope.ForSuperAdmin();
        var municipality = await repository.AddMunicipalityAsync(super, new Municipality
        {
            Name = "Test Municipality", Slug = "test-municipality", Province = "BC", SeedKey = "untrusted"
        });
        Assert.Null(municipality.SeedKey);
        var scope = MunicipalityScope.ForMunicipality(municipality.MunicipalityId);
        var position = await repository.AddPositionAsync(scope, new Position
        {
            MunicipalityId = municipality.MunicipalityId, Name = "Mayor", SeatsAvailable = 1,
            Municipality = new Municipality { Name = "Untrusted navigation", Slug = "untrusted", Province = "BC" }
        });
        var candidate = await repository.AddCandidateAsync(scope, new Candidate
        {
            MunicipalityId = municipality.MunicipalityId, PositionId = position.PositionId, Name = "Candidate",
            Profile = "A profile.", SeedKey = "untrusted", DateCreated = DateTime.MinValue
        });
        Assert.Null(candidate.SeedKey);
        Assert.NotEqual(DateTime.MinValue, candidate.DateCreated);
        var creationDate = candidate.DateCreated;
        var oldRevision = candidate.Revision;
        var edit = (await repository.GetCandidateAsync(scope, candidate.CandidateId))!;
        edit.Name = "Edited candidate";
        edit.DateCreated = DateTime.MinValue;
        edit.SeedKey = "untrusted";
        Assert.True(await repository.UpdateCandidateAsync(scope, edit));
        Assert.NotEqual(oldRevision, candidate.Revision);
        Assert.Equal(creationDate, candidate.DateCreated);
        Assert.Null(candidate.SeedKey);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => repository.UpdateCandidateAsync(scope, edit));
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ValidationException>(() => repository.DeletePositionAsync(scope, position.PositionId, position.Revision));
        db.ChangeTracker.Clear();
        var saved = (await repository.GetCandidateAsync(scope, candidate.CandidateId))!;
        Assert.True(await repository.DeleteCandidateAsync(scope, saved.CandidateId, saved.Revision));
        Assert.True(await repository.DeletePositionAsync(scope, position.PositionId, position.Revision));
        Assert.True(await repository.DeleteMunicipalityAsync(super, municipality.MunicipalityId, municipality.Revision));
        Assert.Empty(await repository.GetMunicipalitiesAsync(super));
    }
}
