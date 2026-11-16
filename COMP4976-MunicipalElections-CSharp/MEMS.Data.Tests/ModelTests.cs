using System.ComponentModel.DataAnnotations;
using MEMS.Data.Configuration;
using MEMS.Data.Entities;
using MEMS.Shared.Identity;
using MEMS.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace MEMS.Data.Tests;

public sealed class ModelTests
{
    [Fact]
    public void CandidatePositionForeignKeyIncludesMunicipalityAndRestrictsDeletion()
    {
        using var db = TestDatabase.SqlModel();
        var candidate = db.Model.FindEntityType(typeof(Candidate))!;
        var foreignKey = Assert.Single(candidate.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(Position));
        Assert.Equal(new[] { "PositionId", "MunicipalityId" }, foreignKey.Properties.Select(p => p.Name));
        Assert.Equal(new[] { "PositionId", "MunicipalityId" }, foreignKey.PrincipalKey.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.All(foreignKey.Properties, p => Assert.False(p.IsNullable));
    }

    [Fact]
    public void SqlServerSchemaHasRequiredConstraintsIndexesAndIdentityTables()
    {
        using var db = TestDatabase.SqlModel();
        var model = db.GetService<IDesignTimeModel>().Model;
        var municipality = model.FindEntityType(typeof(Municipality))!;
        Assert.Contains(municipality.GetIndexes(), i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == "Slug");
        Assert.False(municipality.FindProperty("Slug")!.IsNullable);
        Assert.Equal(100, municipality.FindProperty("Slug")!.GetMaxLength());
        Assert.Contains(municipality.GetCheckConstraints(), c => c.Name == "CK_Municipalities_Slug");
        var user = model.FindEntityType(typeof(ApplicationUser))!;
        Assert.Contains(user.GetCheckConstraints(), c => c.Name == "CK_AspNetUsers_AdministratorScope");
        Assert.Equal(DeleteBehavior.Restrict, Assert.Single(user.GetForeignKeys()).DeleteBehavior);
        Assert.Contains(user.GetIndexes(), i => i.IsUnique && i.GetDatabaseName() == "EmailIndex");
        foreach (var type in new[] { typeof(Municipality), typeof(Position), typeof(Candidate), typeof(ApplicationUser) })
            Assert.Contains(model.FindEntityType(type)!.GetIndexes(), i => i.IsUnique
                && i.Properties.Count == 1 && i.Properties[0].Name == "SeedKey"
                && i.GetFilter() == "[SeedKey] IS NOT NULL");
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("CREATE TABLE [AspNetUserRoles]", sql);
        Assert.Contains("CK_Candidates_Profile", sql);
        Assert.Contains("CK_Positions_SeatsAvailable", sql);
        Assert.Contains("FOREIGN KEY ([PositionId], [MunicipalityId])", sql);
    }

    [Fact]
    public void InitialMigrationGeneratesIdempotentSqlAndMatchesCurrentModel()
    {
        using var db = TestDatabase.SqlModel();
        Assert.EndsWith("_InitialSqlServer", Assert.Single(db.Database.GetMigrations()));
        Assert.False(db.Database.HasPendingModelChanges());
        var migrator = db.GetService<IMigrator>();
        var sql = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("IF NOT EXISTS", sql);
        Assert.Contains("[__EFMigrationsHistory]", sql);
        Assert.Contains("CK_AspNetUsers_AdministratorScope", sql);
        Assert.Contains("Latin1_General_100_BIN2", sql);
        Assert.Contains("DROP TABLE [Candidates]", migrator.GenerateScript(
            fromMigration: db.Database.GetMigrations().Single(), toMigration: Migration.InitialDatabase));
    }

    [Theory]
    [InlineData("Burnaby")]
    [InlineData("-burnaby")]
    [InlineData("burnaby--city")]
    [InlineData("burnaby/")]
    [InlineData("")]
    public async Task InvalidSlugsFailValidation(string slug)
    {
        await using var db = TestDatabase.InMemory();
        db.Municipalities.Add(new Municipality { Name = "Burnaby", Province = "BC", Slug = slug });
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task RequiredFieldsAndPositiveSeatsAreValidated()
    {
        await using var db = TestDatabase.InMemory();
        db.Candidates.Add(new Candidate { Name = "  ", Profile = "" });
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Positions.Add(new Position { Name = "Mayor", SeatsAvailable = 0, MunicipalityId = 1 });
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(AdministratorType.None, 1)]
    [InlineData(AdministratorType.SuperAdmin, 1)]
    [InlineData(AdministratorType.MunicipalityAdministrator, null)]
    [InlineData((AdministratorType)99, null)]
    public async Task InvalidAdministratorScopesFailValidation(AdministratorType type, int? municipalityId)
    {
        await using var db = TestDatabase.InMemory();
        db.Users.Add(new ApplicationUser { AdministratorType = type, MunicipalityId = municipalityId });
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void ConfigurationRequiresAnExplicitNewConnectionAndRejectsProductionSeeding()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        var config = new ConfigurationBuilder().Build();
        Assert.Throws<InvalidOperationException>(() => DataServiceCollectionExtensions.ConfigureSqlServer(options, config, "Development"));
        config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MEMS"] = "Server=localhost;Database=MEMS_ModelOnly;Integrated Security=True",
            ["Mems:Seed:Enabled"] = "true"
        }).Build();
        Assert.Throws<InvalidOperationException>(() => DataServiceCollectionExtensions.ConfigureSqlServer(options, config, "Production"));
    }

    [Fact]
    public void TenantScopesRejectInvalidIdsAndForeignMunicipalities()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MunicipalityScope.ForMunicipality(0));
        Assert.Throws<UnauthorizedAccessException>(() => MunicipalityScope.ForMunicipality(1).Demand(2));
        Assert.True(MunicipalityScope.ForSuperAdmin().Allows(2));
    }
}
