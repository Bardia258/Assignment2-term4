using MEMS.Public.Services.Api;

namespace MEMS.Public.Services.Ballot;

/// <summary>One position in the voting guide with the voter's picks for it.</summary>
public sealed record GuideSection(PositionResponse Position, IReadOnlyList<CandidateResponse> Chosen, bool HasMoreCandidates)
{
    public int OpenSeats => Math.Max(0, Position.SeatsAvailable - Chosen.Count);
}

/// <summary>
/// Display data for a municipality's guide. Which candidates are chosen always comes from the
/// sessionStorage ballot (<see cref="BallotSelections"/>); the API only supplies the public
/// directory (names, positions, seats) used to show them.
/// </summary>
public sealed record GuideData(IReadOnlyList<PositionResponse> Positions, IReadOnlyList<CandidateResponse> Candidates,
    int DroppedCount)
{
    /// <summary>Picks grouped by position, in ballot order, each group in the order the voter added them.</summary>
    public IReadOnlyList<GuideSection> BuildSections(BallotSelections selections)
    {
        var byId = Candidates.ToDictionary(c => c.Id);
        var chosen = selections.Entries
            .Where(e => byId.ContainsKey(e.CandidateId))
            .Select(e => byId[e.CandidateId])
            .ToList();

        return
        [
            .. Positions.Select(p => new GuideSection(p,
                [.. chosen.Where(c => c.PositionId == p.Id)],
                Candidates.Any(c => c.PositionId == p.Id && !selections.Contains(c.Id))))
        ];
    }
}

/// <summary>Loads <see cref="GuideData"/> for the guide page and its print view.</summary>
public sealed class VotingGuideLoader(IPositionApiClient positionApi, ICandidateApiClient candidateApi,
    IBallotSessionService ballot)
{
    /// <exception cref="ApiRequestException">The public directory could not be loaded.</exception>
    public async Task<GuideData> LoadAsync(string municipalitySlug, CancellationToken cancellationToken = default)
    {
        // Two tenant-scoped calls for the public directory; the voter's picks are never sent.
        var positionsTask = positionApi.GetPositionsAsync(municipalitySlug, cancellationToken);
        var candidatesTask = candidateApi.GetCandidatesAsync(
            new CandidateQuery { MunicipalitySlug = municipalitySlug }, cancellationToken);
        await Task.WhenAll(positionsTask, candidatesTask);

        var candidates = candidatesTask.Result;
        var dropped = await ballot.ReconcileAsync(municipalitySlug, candidates);
        return new GuideData(PositionOrder.Sort(positionsTask.Result), candidates, dropped);
    }
}
