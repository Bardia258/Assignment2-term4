namespace MEMS.Public.Services.Ballot;

/// <summary>One candidate in the voting guide, with the position they're running for (used for seat limits).</summary>
public sealed record BallotEntry(int CandidateId, int PositionId);

/// <summary>Immutable snapshot of one municipality's voting guide, in the order candidates were added.</summary>
public sealed class BallotSelections
{
    public static readonly BallotSelections Empty = new([]);

    public BallotSelections(IReadOnlyList<BallotEntry> entries) => Entries = entries;

    public IReadOnlyList<BallotEntry> Entries { get; }

    public int Count => Entries.Count;

    public bool Contains(int candidateId) => Entries.Any(e => e.CandidateId == candidateId);

    public int CountFor(int positionId) => Entries.Count(e => e.PositionId == positionId);
}

public enum BallotAddResult
{
    Added,
    AlreadySelected,

    /// <summary>The position already has as many picks as it has seats.</summary>
    LimitReached,
}
