using MEMS.Public.Services.Api;
using MEMS.Public.Services.Mock;
using MEMS.Public.Services.Tenancy;

namespace MEMS.Public.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>Services shared by mock and real modes.</summary>
    public static IServiceCollection AddPublicSiteServices(this IServiceCollection services)
    {
        // Scoped in Blazor WebAssembly = one instance per browser tab.
        services.AddScoped<TenantState>();
        return services;
    }

    /// <summary>
    /// In-memory mock API clients, used until the backend branch is merged.
    /// Swapping to the real API: add HTTP implementations of the same interfaces and register them here instead.
    /// </summary>
    public static IServiceCollection AddMockElectionApi(this IServiceCollection services)
    {
        services.AddScoped<IMunicipalityApiClient, MockMunicipalityApiClient>();
        return services;
    }
}
