using System.ComponentModel.DataAnnotations;

namespace MEMS.Shared.Contracts;

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

public class MunicipalityRequest : IValidatableObject
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(100), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    public string Slug { get; set; } = "";
    [Required, StringLength(100)] public string Province { get; set; } = "";
    [Required] public DateOnly? ElectionDate { get; set; }
    [StringLength(2000)] public string Description { get; set; } = "";
    [StringLength(255), RegularExpression("^[a-zA-Z0-9][a-zA-Z0-9_.-]*$")] public string? LogoFileName { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ElectionDate.HasValue && ElectionDate.Value.Year is < 2000 or > 2100)
            yield return new ValidationResult("Choose an election date between 2000 and 2100.", [nameof(ElectionDate)]);
    }
}

public sealed class MunicipalityUpdateRequest : MunicipalityRequest
{
    [Required, RegularExpression("^[a-f0-9]{32}$")] public string Revision { get; set; } = "";
}

public class PositionRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Range(1, int.MaxValue)] public int SeatsAvailable { get; set; }
    [Range(1, int.MaxValue)] public int MunicipalityId { get; set; }
}

public sealed class PositionUpdateRequest : PositionRequest
{
    [Required, RegularExpression("^[a-f0-9]{32}$")] public string Revision { get; set; } = "";
}

public class CandidateRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(4000)] public string Profile { get; set; } = "";
    [StringLength(255), RegularExpression("^[a-zA-Z0-9][a-zA-Z0-9_.-]*$")] public string? PhotoFileName { get; set; }
    [Url, StringLength(2048)] public string? Website { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }
    [Range(1, int.MaxValue)] public int MunicipalityId { get; set; }
    [Range(1, int.MaxValue)] public int PositionId { get; set; }
}

public sealed class CandidateUpdateRequest : CandidateRequest
{
    [Required, RegularExpression("^[a-f0-9]{32}$")] public string Revision { get; set; } = "";
}

public sealed record MunicipalityResponse(int Id, string Name, string Slug, string Province, DateTime ElectionDate,
    string Description, string? LogoUrl, string Revision);
public sealed record PositionResponse(int Id, string Name, int SeatsAvailable, int MunicipalityId,
    string MunicipalityName, string Revision);
public sealed record CandidateResponse(int Id, string Name, string Profile, string? PhotoUrl, string? Website,
    string? Email, DateTime DateCreated, int MunicipalityId, string MunicipalityName, int PositionId,
    string PositionTitle, MunicipalityResponse Municipality, PositionResponse Position, string Revision);
