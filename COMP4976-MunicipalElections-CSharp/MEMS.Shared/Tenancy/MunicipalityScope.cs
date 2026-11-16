namespace MEMS.Shared.Tenancy;

/// <summary>A trusted server-side scope. Task 2 must derive it from current Identity data, never request input.</summary>
public sealed record MunicipalityScope
{
    public int? MunicipalityId { get; }
    public bool IsSuperAdmin { get; }

    private MunicipalityScope(int? municipalityId, bool isSuperAdmin)
    {
        MunicipalityId = municipalityId;
        IsSuperAdmin = isSuperAdmin;
    }

    public static MunicipalityScope ForMunicipality(int municipalityId) => municipalityId > 0
        ? new(municipalityId, false)
        : throw new ArgumentOutOfRangeException(nameof(municipalityId));

    public static MunicipalityScope ForSuperAdmin() => new(null, true);

    public bool Allows(int municipalityId) => municipalityId > 0
        && (IsSuperAdmin || MunicipalityId == municipalityId);

    public void Demand(int municipalityId)
    {
        if (!Allows(municipalityId))
            throw new UnauthorizedAccessException("The municipality is outside the current scope.");
    }
}
