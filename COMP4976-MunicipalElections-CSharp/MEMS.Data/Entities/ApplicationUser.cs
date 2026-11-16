using Microsoft.AspNetCore.Identity;

namespace MEMS.Data.Entities;

public class ApplicationUser : IdentityUser
{
    [System.ComponentModel.DataAnnotations.EnumDataType(typeof(MEMS.Shared.Identity.AdministratorType))]
    public MEMS.Shared.Identity.AdministratorType AdministratorType { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string? SeedKey { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(200)]
    public string? FullName { get; set; }
    public ICollection<SavedCandidate> SavedCandidates { get; set; } = new List<SavedCandidate>();
    public int? MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
}

