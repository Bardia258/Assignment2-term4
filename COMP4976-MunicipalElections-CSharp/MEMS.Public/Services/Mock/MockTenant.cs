namespace MEMS.Public.Services.Mock;

/// <summary>Tenant scoping for the mock clients, mirroring the API's per-municipality read scope.</summary>
internal static class MockTenant
{
    public static bool Owns(string municipalitySlug, int municipalityId) =>
        MockElectionData.Municipalities.Any(m => m.Id == municipalityId && m.Slug == municipalitySlug);
}
