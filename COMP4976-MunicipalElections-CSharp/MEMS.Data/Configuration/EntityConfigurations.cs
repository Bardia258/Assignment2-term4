using MEMS.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MEMS.Data.Configuration;

internal sealed class MunicipalityConfiguration : IEntityTypeConfiguration<Municipality>
{
    public void Configure(EntityTypeBuilder<Municipality> builder)
    {
        builder.Property(m => m.Slug).IsUnicode(false);
        builder.Property(m => m.ElectionDate).HasColumnType("date");
        builder.HasIndex(m => m.Slug).IsUnique();
        builder.HasIndex(m => new { m.Name, m.Province }).IsUnique();
        builder.HasIndex(m => m.SeedKey).IsUnique();
        builder.ToTable("Municipalities", t =>
        {
            t.HasCheckConstraint("CK_Municipalities_Name", SqlConstraints.NotBlank("Name"));
            t.HasCheckConstraint("CK_Municipalities_Province", SqlConstraints.NotBlank("Province"));
            t.HasCheckConstraint("CK_Municipalities_Slug", """
                LEN([Slug]) > 0 AND DATALENGTH([Slug]) = LEN([Slug])
                AND [Slug] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^a-z0-9-]%'
                AND LEFT([Slug], 1) <> '-' AND RIGHT([Slug], 1) <> '-'
                AND [Slug] NOT LIKE '%--%'
                """);
        });
    }
}

internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.HasIndex(p => new { p.MunicipalityId, p.Name }).IsUnique();
        builder.HasIndex(p => p.SeedKey).IsUnique();
        builder.HasOne(p => p.Municipality).WithMany(m => m.Positions)
            .HasForeignKey(p => p.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasAlternateKey(p => new { p.PositionId, p.MunicipalityId });
        builder.ToTable("Positions", t =>
        {
            t.HasCheckConstraint("CK_Positions_SeatsAvailable", "[SeatsAvailable] > 0");
            t.HasCheckConstraint("CK_Positions_Name", SqlConstraints.NotBlank("Name"));
        });
    }
}

internal sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.HasIndex(c => c.SeedKey).IsUnique();
        builder.HasIndex(c => new { c.MunicipalityId, c.Name });
        builder.HasOne(c => c.Municipality).WithMany(m => m.Candidates)
            .HasForeignKey(c => c.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        // The database itself rejects a candidate assigned to another tenant's position.
        builder.HasOne(c => c.Position).WithMany(p => p.Candidates)
            .HasForeignKey(c => new { c.PositionId, c.MunicipalityId })
            .HasPrincipalKey(p => new { p.PositionId, p.MunicipalityId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(c => c.DateCreated).HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.ToTable("Candidates", t =>
        {
            t.HasCheckConstraint("CK_Candidates_Name", SqlConstraints.NotBlank("Name"));
            t.HasCheckConstraint("CK_Candidates_Profile", SqlConstraints.NotBlank("Profile"));
        });
    }
}

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.HasOne(u => u.Municipality).WithMany()
            .HasForeignKey(u => u.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
        builder.HasIndex(u => u.SeedKey).IsUnique();
        builder.ToTable("AspNetUsers", t => t.HasCheckConstraint("CK_AspNetUsers_AdministratorScope", """
            ([AdministratorType] IN (0, 1) AND [MunicipalityId] IS NULL)
            OR ([AdministratorType] = 2 AND [MunicipalityId] IS NOT NULL)
            """));
    }
}

internal sealed class SavedCandidateConfiguration : IEntityTypeConfiguration<SavedCandidate>
{
    public void Configure(EntityTypeBuilder<SavedCandidate> builder)
    {
        builder.HasIndex(s => new { s.UserId, s.CandidateId }).IsUnique();
        builder.HasOne(s => s.User).WithMany(u => u.SavedCandidates)
            .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Candidate).WithMany(c => c.SavedCandidates)
            .HasForeignKey(s => s.CandidateId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(s => s.DateSaved).HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    }
}

internal static class SqlConstraints
{
    // Identifiers are fixed model property names, never user input.
    public static string NotBlank(string column) =>
        $"LEN(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([{column}])), CHAR(9), ''), CHAR(10), ''), CHAR(13), '')) > 0";
}
