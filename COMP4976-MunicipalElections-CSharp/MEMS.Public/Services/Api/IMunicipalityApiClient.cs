namespace MEMS.Public.Services.Api;

/// <summary>
/// Read-only access to the public municipality (tenant) directory.
/// Real API routes (MEMS.Api, anonymous):
///   GET /api/municipalities              -> MunicipalityResponse[]
///   GET /api/municipalities/slug/{slug}  -> MunicipalityResponse, or 404 for an unknown tenant
/// </summary>
public interface IMunicipalityApiClient
{
    /// <summary>All municipalities, ordered by name.</summary>
    /// <exception cref="ApiRequestException">The API could not be reached or returned an error.</exception>
    Task<IReadOnlyList<MunicipalityResponse>> GetMunicipalitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>The municipality with this URL slug, or <c>null</c> if no such tenant exists.</summary>
    /// <exception cref="ApiRequestException">The API could not be reached or returned an error other than 404.</exception>
    Task<MunicipalityResponse?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
