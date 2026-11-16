using MEMS.Shared.Identity;
using MEMS.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data.Services;

/// <summary>Reads current persisted assignments, so stale role claims cannot retain tenant access.</summary>
public sealed class AdministratorScopeResolver(ApplicationDbContext db)
{
    public async Task<MunicipalityScope?> ResolveAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || (user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow)) return null;
        var roles = await (from membership in db.UserRoles.AsNoTracking()
                           join role in db.Roles.AsNoTracking() on membership.RoleId equals role.Id
                           where membership.UserId == userId
                               && (role.Name == AdminRoles.SuperAdmin || role.Name == AdminRoles.MunicipalityAdministrator)
                           select role.Name).ToListAsync(cancellationToken);
        if (roles.Count != 1) return null;
        if (roles[0] == AdminRoles.SuperAdmin)
            return user.AdministratorType == AdministratorType.SuperAdmin && user.MunicipalityId is null
                ? MunicipalityScope.ForSuperAdmin() : null;
        return user.AdministratorType == AdministratorType.MunicipalityAdministrator && user.MunicipalityId is > 0
            ? MunicipalityScope.ForMunicipality(user.MunicipalityId.Value) : null;
    }
}
