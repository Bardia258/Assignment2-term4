namespace MEMS.Public.Services.Api;

public sealed class MunicipalityApiClient(ElectionApiTransport api) : IMunicipalityApiClient
{
    public async Task<IReadOnlyList<MunicipalityResponse>> GetMunicipalitiesAsync(CancellationToken cancellationToken = default) =>
        (await api.GetAsync("api/municipalities", PublicApiJsonContext.Default.ListMunicipalityResponse, cancellationToken))!;

    public Task<MunicipalityResponse?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        api.GetAsync("api/municipalities/slug/" + ElectionApiQuery.Slug(slug),
            PublicApiJsonContext.Default.MunicipalityResponse, cancellationToken, allowNotFound: true);
}
