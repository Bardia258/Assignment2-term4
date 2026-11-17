using MEMS.Public.Services.Api;

namespace MEMS.Public.Services.Mock;

/// <summary>Mock <see cref="IMunicipalityApiClient"/> backed by <see cref="MockElectionData"/>.</summary>
public sealed class MockMunicipalityApiClient(ILogger<MockMunicipalityApiClient> logger) : IMunicipalityApiClient
{
    // Small artificial delay so loading states are visible during development.
    private static readonly TimeSpan SimulatedLatency = TimeSpan.FromMilliseconds(300);

    public async Task<IReadOnlyList<MunicipalityResponse>> GetMunicipalitiesAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(SimulatedLatency, cancellationToken);
        var result = MockElectionData.Municipalities.OrderBy(m => m.Name).ToList();
        logger.LogInformation("Mock API returned {Count} municipalities", result.Count);
        return result;
    }

    public async Task<MunicipalityResponse?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        await Task.Delay(SimulatedLatency, cancellationToken);
        var match = MockElectionData.Municipalities.SingleOrDefault(m => m.Slug == slug);
        if (match is null)
            logger.LogWarning("Mock API: municipality {Slug} not found", slug);
        return match;
    }
}
