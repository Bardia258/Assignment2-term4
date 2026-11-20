using System.ComponentModel.DataAnnotations;
using MEMS.Data.Entities;
using MEMS.Shared.Identity;
using MEMS.Shared.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data.Services;

/// <summary>Creates municipality-owned Identity accounts inside the caller's database transaction.</summary>
public sealed class MunicipalityAdministratorProvisioner(ApplicationDbContext db,
    UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, string? initialPassword)
{
    private static string AccountKey(int municipalityId) => $"municipality-admin:{municipalityId}";

    public async Task<ApplicationUser> CreateAsync(MunicipalityScope scope, Municipality municipality,
        CancellationToken ct = default)
    {
        DemandSuperAdmin(scope);
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(initialPassword))
            throw new ValidationException("Configure an initial municipality administrator password before creating a municipality.");

        var key = AccountKey(municipality.MunicipalityId);
        var existing = await db.Users.SingleOrDefaultAsync(u => u.SeedKey == key, ct);
        if (existing is not null)
        {
            if (existing.MunicipalityId != municipality.MunicipalityId
                || existing.AdministratorType != AdministratorType.MunicipalityAdministrator
                || !(await users.GetRolesAsync(existing)).SequenceEqual([AdminRoles.MunicipalityAdministrator]))
                throw new RecordConflictException("The existing municipality administrator assignment needs review.");
            return existing;
        }

        var username = $"{municipality.Slug}admin";
        var email = $"{municipality.Slug}-admin@example.com";
        if (await users.FindByNameAsync(username) is not null || await users.FindByEmailAsync(email) is not null)
            throw new RecordConflictException("An administrator account for this slug already exists. Choose a different municipality slug.");

        var user = new ApplicationUser
        {
            UserName = username, Email = email, FullName = $"{municipality.Name} Administrator",
            MunicipalityId = municipality.MunicipalityId,
            AdministratorType = AdministratorType.MunicipalityAdministrator,
            SeedKey = key, LockoutEnabled = true
        };
        var validation = new List<IdentityError>();
        foreach (var validator in users.PasswordValidators)
            validation.AddRange((await validator.ValidateAsync(users, user, initialPassword)).Errors);
        if (validation.Count > 0)
            throw new ValidationException("The configured initial administrator password does not meet the password policy.");

        if (!await roles.RoleExistsAsync(AdminRoles.MunicipalityAdministrator))
            RequireSuccess(await roles.CreateAsync(new IdentityRole(AdminRoles.MunicipalityAdministrator)));
        RequireSuccess(await users.CreateAsync(user, initialPassword));
        RequireSuccess(await users.AddToRoleAsync(user, AdminRoles.MunicipalityAdministrator));
        return user;
    }

    public async Task RemoveGeneratedAccountsAsync(MunicipalityScope scope, int municipalityId,
        CancellationToken ct = default)
    {
        DemandSuperAdmin(scope);
        var assigned = await db.Users.Where(u => u.MunicipalityId == municipalityId).ToListAsync(ct);
        foreach (var user in assigned)
        {
            // Never remove a seeded, manually assigned or elevated administrator implicitly.
            if (user.SeedKey != AccountKey(municipalityId)
                || user.AdministratorType != AdministratorType.MunicipalityAdministrator
                || !(await users.GetRolesAsync(user)).SequenceEqual([AdminRoles.MunicipalityAdministrator]))
                throw new RecordConflictException("Reassign existing administrators before deleting this municipality.");
        }
        foreach (var user in assigned)
        {
            ct.ThrowIfCancellationRequested();
            RequireSuccess(await users.DeleteAsync(user));
        }
    }

    private static void DemandSuperAdmin(MunicipalityScope scope)
    {
        if (!scope.IsSuperAdmin) throw new UnauthorizedAccessException("Only Super Admin can provision municipality administrators.");
    }

    private static void RequireSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new RecordConflictException("The municipality administrator account could not be saved. No municipality changes were applied.");
    }
}
