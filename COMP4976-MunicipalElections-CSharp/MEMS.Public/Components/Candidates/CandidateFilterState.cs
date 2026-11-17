namespace MEMS.Public.Components.Candidates;

/// <summary>
/// Search text and position filter for the candidate list. The candidates page keeps this in the
/// URL query string (?q=...&amp;position=...) so filtered views can be bookmarked and shared.
/// </summary>
public sealed record CandidateFilterState(string? Search, int? PositionId)
{
    /// <summary>Same limit the API applies to <see cref="CandidateQuery.Name"/>.</summary>
    public const int MaxSearchLength = 100;

    public static readonly CandidateFilterState Empty = new(null, null);

    /// <summary>Builds a state from raw query-string values, ignoring anything malformed.</summary>
    public static CandidateFilterState FromQuery(string? search, string? position)
    {
        var text = string.IsNullOrWhiteSpace(search) ? null : search;
        if (text is { Length: > MaxSearchLength }) text = text[..MaxSearchLength];
        int? positionId = int.TryParse(position, out var id) && id > 0 ? id : null;
        return new CandidateFilterState(text, positionId);
    }
}
