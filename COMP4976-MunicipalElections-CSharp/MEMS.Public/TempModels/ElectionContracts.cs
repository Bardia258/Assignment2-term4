using System.ComponentModel.DataAnnotations;

// TEMPORARY COPY — DELETE THIS FOLDER WHEN THE BACKEND BRANCH IS MERGED.
//
// These types are copied field-for-field from MEMS.Shared/Contracts/ElectionContracts.cs on
// origin/bb-branch (the partner-owned backend). Only the public read DTOs and query classes the
// public site needs are copied; admin request/update classes are intentionally left out.
//
// To switch to the real contracts once MEMS.Shared contains them:
//   1. Delete MEMS.Public/TempModels/.
//   2. In MEMS.Public/GlobalUsings.cs, replace `MEMS.Public.TempModels` with `MEMS.Shared.Contracts`.
// Class and property names are identical, so no other code changes are needed.
namespace MEMS.Public.TempModels;

public class MunicipalityQuery
{
    [Range(1, int.MaxValue)] public int? MunicipalityId { get; set; }
    [StringLength(100), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    public string? MunicipalitySlug { get; set; }
}

public sealed class CandidateQuery : MunicipalityQuery
{
    [Range(1, int.MaxValue)] public int? PositionId { get; set; }
    [StringLength(100)] public string? Name { get; set; }
}

public sealed record MunicipalityResponse(int Id, string Name, string Slug, string Province, DateTime ElectionDate,
    string Description, string? LogoUrl, string Revision);
public sealed record PositionResponse(int Id, string Name, int SeatsAvailable, int MunicipalityId,
    string MunicipalityName, string Revision);
public sealed record CandidateResponse(int Id, string Name, string Profile, string? PhotoUrl, string? Website,
    string? Email, DateTime DateCreated, int MunicipalityId, string MunicipalityName, int PositionId,
    string PositionTitle, MunicipalityResponse Municipality, PositionResponse Position, string Revision);
