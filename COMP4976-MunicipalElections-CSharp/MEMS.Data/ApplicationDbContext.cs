using System.ComponentModel.DataAnnotations;
using MEMS.Data.Entities;
using MEMS.Shared.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<SavedCandidate> SavedCandidates => Set<SavedCandidate>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateAndRefreshRevisions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateAndRefreshRevisions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateAndRefreshRevisions()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            Validator.ValidateObject(entry.Entity, new ValidationContext(entry.Entity), validateAllProperties: true);
            if (entry.Entity is ApplicationUser user
                && (user.AdministratorType == AdministratorType.MunicipalityAdministrator
                    ? user.MunicipalityId is null or <= 0
                    : user.MunicipalityId is not null))
                throw new ValidationException("Only municipality administrators must have exactly one municipality.");

            if (entry.State == EntityState.Modified && entry.Entity is Municipality or Position or Candidate)
                entry.Property("Revision").CurrentValue = Guid.NewGuid().ToString("N");
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Keep composite Identity keys below SQL Server's clustered-index byte limit.
        builder.Entity<ApplicationUser>().Property(u => u.Id).HasMaxLength(128);
        builder.Entity<IdentityRole>().Property(r => r.Id).HasMaxLength(128);
        builder.Entity<IdentityUserLogin<string>>().Property(l => l.LoginProvider).HasMaxLength(128);
        builder.Entity<IdentityUserLogin<string>>().Property(l => l.ProviderKey).HasMaxLength(128);
        builder.Entity<IdentityUserToken<string>>().Property(t => t.LoginProvider).HasMaxLength(128);
        builder.Entity<IdentityUserToken<string>>().Property(t => t.Name).HasMaxLength(128);
        builder.UseCollation("Latin1_General_100_CI_AS");
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
