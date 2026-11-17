using MEMS.Public.Services.Api;

namespace MEMS.Public.Services.Mock;

/// <summary>Mock <see cref="ICandidateApiClient"/> backed by <see cref="MockElectionData"/>.</summary>
public sealed class MockCandidateApiClient(ILogger<MockCandidateApiClient> logger) : ICandidateApiClient
{
    private static readonly TimeSpan SimulatedLatency = TimeSpan.FromMilliseconds(300);

    public async Task<IReadOnlyList<CandidateResponse>> GetCandidatesAsync(CandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var slug = query.MunicipalitySlug;
        ArgumentException.ThrowIfNullOrWhiteSpace(slug, nameof(query.MunicipalitySlug));
        await Task.Delay(SimulatedLatency, cancellationToken);

        // Matches the real API: tenant-scoped, optional position, case-insensitive "contains" on name,
        // ordered by name then id.
        var candidates = MockElectionData.Candidates.Where(c => MockTenant.Owns(slug, c.MunicipalityId));
        if (query.PositionId is { } positionId)
            candidates = candidates.Where(c => c.PositionId == positionId);
        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            var name = query.Name.Trim();
            candidates = candidates.Where(c => c.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        var result = candidates.OrderBy(c => c.Name).ThenBy(c => c.Id).ToList();
        logger.MockCandidatesReturned(result.Count, slug);
        return result;
    }

    public async Task<CandidateResponse?> GetCandidateAsync(string municipalitySlug, int candidateId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(municipalitySlug);
        await Task.Delay(SimulatedLatency, cancellationToken);

        // A candidate from another municipality is "not found", never leaked across tenants.
        var match = MockElectionData.Candidates.SingleOrDefault(c =>
            c.Id == candidateId && MockTenant.Owns(municipalitySlug, c.MunicipalityId));
        if (match is null)
            logger.MockCandidateNotFound(candidateId, municipalitySlug);
        return match;
    }
}
