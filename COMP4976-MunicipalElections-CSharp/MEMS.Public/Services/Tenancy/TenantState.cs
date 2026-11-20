namespace MEMS.Public.Services.Tenancy;

/// <summary>
/// Holds the active municipality (tenant) for the current browser tab.
///
/// The tenant always comes from the URL (e.g. /burnaby/...). <see cref="Components.TenantGuard"/>
/// resolves the slug through the API and calls <see cref="SetCurrent"/>; layout components
/// (navigation, header) subscribe to <see cref="Changed"/> to show tenant-specific links.
/// Pages must pass the active tenant to every API call so only that tenant's data is shown.
/// </summary>
public sealed class TenantState(ILogger<TenantState> logger)
{
    public MunicipalityResponse? Current { get; private set; }

    public event Action? Changed;

    public void SetCurrent(MunicipalityResponse municipality)
    {
        if (Current?.Id == municipality.Id) return;
        logger.TenantChanged(municipality.Slug);
        Current = municipality;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (Current is null) return;
        logger.TenantCleared();
        Current = null;
        Changed?.Invoke();
    }
}
