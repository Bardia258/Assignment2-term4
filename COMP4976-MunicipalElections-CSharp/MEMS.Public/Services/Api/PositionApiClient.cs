namespace MEMS.Public.Services.Api;

public sealed class PositionApiClient(ElectionApiTransport api) : IPositionApiClient
{
    public async Task<IReadOnlyList<PositionResponse>> GetPositionsAsync(string municipalitySlug,
        CancellationToken cancellationToken = default) =>
        (await api.GetAsync("api/positions?municipalitySlug=" + ElectionApiQuery.Slug(municipalitySlug),
            PublicApiJsonContext.Default.ListPositionResponse, cancellationToken))!;
}
