namespace MEMS.Public.Services.Api;

/// <summary>
/// Read-only access to the candidates of one municipality.
/// Real API routes (MEMS.Api, anonymous):
///   GET /api/candidates?municipalitySlug={slug}[&amp;positionId={id}][&amp;name={text}]
///       -> CandidateResponse[] ordered by name; 404 if positionId is not in that municipality
///   GET /api/candidates/{id}?municipalitySlug={slug}
///       -> CandidateResponse, or 404 if the candidate does not exist in that municipality
/// </summary>
public interface ICandidateApiClient
{
    /// <summary>
    /// Candidates in <see cref="MunicipalityQuery.MunicipalitySlug"/> (required), optionally narrowed by
    /// position and by a case-insensitive name fragment. Ordered by name.
    /// An unknown position returns an empty list.
    /// </summary>
    /// <exception cref="ArgumentException">The query has no municipality slug.</exception>
    /// <exception cref="ApiRequestException">The API could not be reached or returned an error.</exception>
    Task<IReadOnlyList<CandidateResponse>> GetCandidatesAsync(CandidateQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>The candidate with this id in the given municipality, or <c>null</c> if there is none.</summary>
    /// <exception cref="ApiRequestException">The API could not be reached or returned an error other than 404.</exception>
    Task<CandidateResponse?> GetCandidateAsync(string municipalitySlug, int candidateId,
        CancellationToken cancellationToken = default);
}
