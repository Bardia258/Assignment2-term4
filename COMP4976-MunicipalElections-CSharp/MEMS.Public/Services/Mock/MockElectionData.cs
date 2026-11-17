namespace MEMS.Public.Services.Mock;

/// <summary>
/// In-memory sample data used by the mock API clients until the real MEMS.Api is merged.
/// Fictional data shaped like the backend's development seed (Burnaby, Surrey, Richmond).
/// Positions and candidates are added in Phase 2.
/// </summary>
internal static class MockElectionData
{
    private static readonly DateTime ElectionDate = new(2026, 10, 17);

    public static readonly IReadOnlyList<MunicipalityResponse> Municipalities =
    [
        new(1, "Burnaby", "burnaby", "BC", ElectionDate,
            "Fictional classroom election dataset for Burnaby, featuring housing, sustainable transportation, and inclusive public spaces.",
            null, Revision(1)),
        new(2, "Richmond", "richmond", "BC", ElectionDate,
            "Fictional classroom election dataset for Richmond, featuring flood protection, farmland preservation, and community safety.",
            null, Revision(2)),
        new(3, "Surrey", "surrey", "BC", ElectionDate,
            "Fictional classroom election dataset for Surrey, featuring rapid transit, policing, and fast-growing neighbourhoods.",
            null, Revision(3)),
    ];

    /// <summary>Fake 32-character lowercase hex revision, matching the real API's format.</summary>
    private static string Revision(int seed) => seed.ToString("x32");
}
