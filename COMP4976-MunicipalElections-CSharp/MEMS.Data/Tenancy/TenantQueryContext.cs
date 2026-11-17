namespace MEMS.Data.Tenancy;

/// <summary>Request-scoped query boundary. Hosts that register it start with no tenant access.</summary>
public sealed class TenantQueryContext
{
    public bool IsRestricted { get; private set; } = true;
    public int? MunicipalityId { get; private set; }

    public void RestrictTo(int municipalityId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(municipalityId);
        IsRestricted = true;
        MunicipalityId = municipalityId;
    }

    public void AllowAll()
    {
        IsRestricted = false;
        MunicipalityId = null;
    }
}
