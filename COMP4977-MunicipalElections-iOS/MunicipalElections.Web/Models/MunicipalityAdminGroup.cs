using Microsoft.AspNetCore.Identity;

namespace MunicipalElections.Web.Models;

// Historical SQLite administrator-role mapping retained for migration compatibility.
public class MunicipalityAdminGroup
{
    public int MunicipalityId { get; set; }
    public Municipality Municipality { get; set; } = null!;
    public string RoleId { get; set; } = string.Empty;
    public IdentityRole Role { get; set; } = null!;
}
