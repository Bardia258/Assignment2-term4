using System.ComponentModel.DataAnnotations;
using System.Globalization;
using MEMS.Public.Services.Tenancy;

namespace MEMS.Public.Services.Api;

public sealed class CandidateApiClient(ElectionApiTransport api) : ICandidateApiClient
{
    public async Task<IReadOnlyList<CandidateResponse>> GetCandidatesAsync(CandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        Validator.ValidateObject(query, new ValidationContext(query), true);
        var path = "api/candidates?municipalitySlug=" + ElectionApiQuery.Slug(query.MunicipalitySlug);
        if (query.MunicipalityId is { } tenantId) path += "&municipalityId=" + tenantId.ToString(CultureInfo.InvariantCulture);
        if (query.PositionId is { } positionId) path += "&positionId=" + positionId.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(query.Name)) path += "&name=" + Uri.EscapeDataString(query.Name.Trim());
        return (await api.GetAsync(path, PublicApiJsonContext.Default.ListCandidateResponse, cancellationToken))!;
    }

    public Task<CandidateResponse?> GetCandidateAsync(string municipalitySlug, int candidateId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidateId);
        return api.GetAsync("api/candidates/" + candidateId.ToString(CultureInfo.InvariantCulture)
            + "?municipalitySlug=" + ElectionApiQuery.Slug(municipalitySlug),
            PublicApiJsonContext.Default.CandidateResponse, cancellationToken, allowNotFound: true);
    }
}

internal static class ElectionApiQuery
{
    public static string Slug(string? value)
    {
        if (value is null || !TenantSlug.IsValid(value)) throw new ArgumentException("A valid municipality slug is required.");
        return Uri.EscapeDataString(value);
    }
}
