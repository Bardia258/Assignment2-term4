namespace MEMS.Public.Services;

/// <summary>
/// Ballot order for positions: Mayor, then Councillor, then School Trustee. Any other position the
/// API returns comes after those, alphabetically. The API itself sorts by name, so pages apply this.
/// </summary>
public static class PositionOrder
{
    private static readonly string[] KnownOrder = ["Mayor", "Councillor", "School Trustee"];

    public static IReadOnlyList<PositionResponse> Sort(IEnumerable<PositionResponse> positions) =>
        [.. positions
            .OrderBy(p => Rank(p.Name))
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Id)];

    private static int Rank(string name)
    {
        var index = Array.FindIndex(KnownOrder, k => string.Equals(k, name.Trim(), StringComparison.OrdinalIgnoreCase));
        return index < 0 ? KnownOrder.Length : index;
    }
}
