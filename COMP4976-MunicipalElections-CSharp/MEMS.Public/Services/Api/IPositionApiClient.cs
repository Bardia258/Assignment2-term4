namespace MEMS.Public.Services.Api;

/// <summary>
/// Read-only access to the elected positions of one municipality.
/// Real API route (MEMS.Api, anonymous):
///   GET /api/positions?municipalitySlug={slug}  -> PositionResponse[] ordered by name
/// </summary>
public interface IPositionApiClient
{
    /// <summary>All positions on the ballot for the given municipality, ordered by name.</summary>
    /// <exception cref="ApiRequestException">The API could not be reached or returned an error.</exception>
    Task<IReadOnlyList<PositionResponse>> GetPositionsAsync(string municipalitySlug,
        CancellationToken cancellationToken = default);
}
