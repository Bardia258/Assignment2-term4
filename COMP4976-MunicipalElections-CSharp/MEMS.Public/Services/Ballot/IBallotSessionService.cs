namespace MEMS.Public.Services.Ballot;

/// <summary>
/// The voter's personal voting guide, one per municipality, kept ONLY in the browser tab's
/// sessionStorage under "ballot:{slug}". Selections are never sent to any API or server.
///
/// Seat limits come from <see cref="PositionResponse.SeatsAvailable"/>: a voter can't pick more
/// candidates for a position than it has seats (e.g. one Mayor, up to eight Councillors).
/// </summary>
public interface IBallotSessionService
{
    /// <summary>Raised with the municipality slug whenever that municipality's guide changes.</summary>
    event Action<string>? Changed;

    /// <summary>False once the browser refused sessionStorage; the guide then lasts only until the page reloads.</summary>
    bool IsPersistent { get; }

    ValueTask<BallotSelections> GetAsync(string municipalitySlug);

    /// <summary>Adds a candidate if their position still has a free seat in the guide.</summary>
    /// <exception cref="ArgumentException">The candidate isn't in this municipality.</exception>
    Task<BallotAddResult> AddAsync(string municipalitySlug, CandidateResponse candidate);

    Task RemoveAsync(string municipalitySlug, int candidateId);

    Task ClearAsync(string municipalitySlug);

    /// <summary>
    /// Syncs the guide with the municipality's current candidate list: drops candidates who are no
    /// longer running, updates anyone whose position changed, and trims any position that has more picks
    /// than seats (possible if storage was edited by hand). Returns how many were no longer running.
    /// </summary>
    Task<int> ReconcileAsync(string municipalitySlug, IReadOnlyCollection<CandidateResponse> currentCandidates);
}
