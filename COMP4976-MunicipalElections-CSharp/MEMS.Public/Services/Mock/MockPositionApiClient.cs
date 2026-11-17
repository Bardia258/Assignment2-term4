using MEMS.Public.Services.Api;

namespace MEMS.Public.Services.Mock;

/// <summary>Mock <see cref="IPositionApiClient"/> backed by <see cref="MockElectionData"/>.</summary>
public sealed class MockPositionApiClient(ILogger<MockPositionApiClient> logger) : IPositionApiClient
{
    private static readonly TimeSpan SimulatedLatency = TimeSpan.FromMilliseconds(300);

    public async Task<IReadOnlyList<PositionResponse>> GetPositionsAsync(string municipalitySlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(municipalitySlug);
        await Task.Delay(SimulatedLatency, cancellationToken);

        // Same ordering as the real API: by name, then id.
        var result = MockElectionData.Positions
            .Where(p => MockTenant.Owns(municipalitySlug, p.MunicipalityId))
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .ToList();
        var count = result.Count;
        logger.LogInformation("Mock API returned {Count} positions for {Slug}", count, municipalitySlug);
        return result;
    }
}
