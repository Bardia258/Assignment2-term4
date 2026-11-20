using MEMS.Public.Services.Api;
using MEMS.Public.Services.Ballot;
using MEMS.Public.Services.Storage;
using MEMS.Public.Services.Tenancy;

namespace MEMS.Public.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>Browser-only tenant state and voting-guide storage.</summary>
    public static IServiceCollection AddPublicSiteServices(this IServiceCollection services)
    {
        // Scoped in Blazor WebAssembly = one instance per browser tab.
        services.AddScoped<TenantState>();

        // Voting guide: browser sessionStorage only, never sent to the API.
        services.AddScoped<IBrowserSessionStorage, BrowserSessionStorage>();
        services.AddScoped<IBallotSessionService, BallotSessionService>();
        services.AddScoped<VotingGuideLoader>();
        return services;
    }

    /// <summary>Anonymous read clients for the SQL-backed MEMS API. Configuration is public browser data.</summary>
    public static IServiceCollection AddElectionApi(this IServiceCollection services, IConfiguration configuration)
    {
        var configured = configuration["ApiBaseUrl"];
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var address)
            || address.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(address.UserInfo)
            || !string.IsNullOrEmpty(address.Query) || !string.IsNullOrEmpty(address.Fragment))
            throw new InvalidOperationException("Configure ApiBaseUrl as the public HTTP(S) URL of MEMS.Api.");
        var baseAddress = new Uri(address.AbsoluteUri.TrimEnd('/') + "/");
        services.AddScoped(_ => new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(30) });
        services.AddScoped<ElectionApiTransport>();
        services.AddScoped<IMunicipalityApiClient, MunicipalityApiClient>();
        services.AddScoped<IPositionApiClient, PositionApiClient>();
        services.AddScoped<ICandidateApiClient, CandidateApiClient>();
        return services;
    }
}
