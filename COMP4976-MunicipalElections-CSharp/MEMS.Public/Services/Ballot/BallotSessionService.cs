using System.Text.Json;
using System.Text.Json.Serialization;
using MEMS.Public.Services.Storage;
using MEMS.Public.Services.Tenancy;

namespace MEMS.Public.Services.Ballot;

/// <summary>
/// <see cref="IBallotSessionService"/> backed by sessionStorage.
///
/// Registered as scoped, which in Blazor WebAssembly means one instance per browser tab, matching
/// sessionStorage's own per-tab lifetime. Each municipality's guide is read once and cached; every
/// change updates the cache first (so seat-limit checks are never racing an await) and then writes
/// the whole guide back. Stored data is treated as untrusted on read: malformed, duplicate, or
/// excess entries are discarded.
/// </summary>
public sealed class BallotSessionService(IBrowserSessionStorage storage, ILogger<BallotSessionService> logger)
    : IBallotSessionService
{
    public const string KeyPrefix = "ballot:";
    // SQL-backed candidate IDs differ from the partner's mock IDs; never adopt prototype selections.
    private const int CurrentVersion = 2;

    // Far above any real ballot; stops a tampered value from bloating memory.
    private const int MaxEntries = 100;

    private readonly Dictionary<string, Task<BallotSelections>> cache = new(StringComparer.Ordinal);

    public event Action<string>? Changed;

    public bool IsPersistent { get; private set; } = true;

    public static string StorageKey(string municipalitySlug) => KeyPrefix + municipalitySlug;

    public async ValueTask<BallotSelections> GetAsync(string municipalitySlug)
    {
        RequireValidSlug(municipalitySlug);
        if (!cache.TryGetValue(municipalitySlug, out var load))
        {
            // Cache the task, not the result, so components that ask at the same time share one read.
            load = LoadAsync(municipalitySlug);
            cache[municipalitySlug] = load;
        }
        return await load;
    }

    public async Task<BallotAddResult> AddAsync(string municipalitySlug, CandidateResponse candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Municipality.Slug != municipalitySlug)
            throw new ArgumentException("Candidate belongs to a different municipality.", nameof(candidate));

        var current = await LatestAsync(municipalitySlug);
        if (current.Contains(candidate.Id)) return BallotAddResult.AlreadySelected;
        if (current.CountFor(candidate.PositionId) >= candidate.Position.SeatsAvailable)
            return BallotAddResult.LimitReached;

        await SaveAsync(municipalitySlug, [.. current.Entries, new BallotEntry(candidate.Id, candidate.PositionId)]);
        return BallotAddResult.Added;
    }

    public async Task RemoveAsync(string municipalitySlug, int candidateId)
    {
        var current = await LatestAsync(municipalitySlug);
        if (!current.Contains(candidateId)) return;
        await SaveAsync(municipalitySlug, [.. current.Entries.Where(e => e.CandidateId != candidateId)]);
    }

    public async Task ClearAsync(string municipalitySlug)
    {
        var current = await LatestAsync(municipalitySlug);
        if (current.Count == 0) return;
        await SaveAsync(municipalitySlug, []);
    }

    public async Task<int> ReconcileAsync(string municipalitySlug, IReadOnlyCollection<CandidateResponse> currentCandidates)
    {
        var current = await LatestAsync(municipalitySlug);
        if (current.Count == 0) return 0;

        var byId = currentCandidates
            .Where(c => c.Municipality.Slug == municipalitySlug)
            .ToDictionary(c => c.Id);
        var running = current.Entries
            .Where(e => byId.ContainsKey(e.CandidateId))
            .Select(e => e with { PositionId = byId[e.CandidateId].PositionId })
            .ToList();
        var dropped = current.Count - running.Count;

        // Storage can be edited by hand, and seat counts can shrink: keep at most SeatsAvailable
        // picks per position (earliest picks win).
        var kept = running
            .GroupBy(e => e.PositionId)
            .SelectMany(g => g.Take(byId[g.First().CandidateId].Position.SeatsAvailable))
            .ToHashSet();
        var trimmed = running.Where(kept.Contains).ToList();

        if (dropped > 0 || !trimmed.SequenceEqual(current.Entries))
        {
            logger.GuideReconciled(municipalitySlug, dropped, running.Count - trimmed.Count);
            await SaveAsync(municipalitySlug, trimmed);
        }
        return dropped;
    }

    /// <summary>
    /// The newest snapshot after the initial read. Mutators must use this rather than the value
    /// GetAsync returned: two clicks awaiting the same first read would otherwise both resume with
    /// the same stale snapshot and could together exceed a seat limit.
    /// </summary>
    private async Task<BallotSelections> LatestAsync(string slug)
    {
        await GetAsync(slug);
        return cache[slug].Result; // completed: the finished first read, or the latest saved snapshot
    }

    private async Task<BallotSelections> LoadAsync(string slug)
    {
        string? json;
        try
        {
            json = await storage.GetItemAsync(StorageKey(slug));
        }
        catch (BrowserStorageUnavailableException ex)
        {
            MarkNotPersistent(ex);
            return BallotSelections.Empty;
        }

        if (string.IsNullOrEmpty(json)) return BallotSelections.Empty;

        try
        {
            var stored = JsonSerializer.Deserialize(json, BallotJsonContext.Default.StoredBallot);
            if (stored is not { Version: CurrentVersion, Selections: not null })
                throw new JsonException($"Unsupported voting guide format (version {stored?.Version}).");

            var entries = stored.Selections
                .Where(s => s is { CandidateId: > 0, PositionId: > 0 })
                .DistinctBy(s => s.CandidateId)
                .Take(MaxEntries)
                .Select(s => new BallotEntry(s.CandidateId, s.PositionId))
                .ToList();
            return new BallotSelections(entries);
        }
        catch (JsonException ex)
        {
            // Corrupt or hand-edited value: start fresh rather than break the site.
            logger.GuideUnreadable(ex, slug);
            return BallotSelections.Empty;
        }
    }

    private async Task SaveAsync(string slug, IReadOnlyList<BallotEntry> entries)
    {
        // Update the in-memory copy synchronously so the next limit check already sees this change.
        var updated = new BallotSelections(entries);
        cache[slug] = Task.FromResult(updated);
        Changed?.Invoke(slug);

        if (!IsPersistent) return;
        try
        {
            if (entries.Count == 0)
            {
                await storage.RemoveItemAsync(StorageKey(slug));
            }
            else
            {
                var stored = new StoredBallot(CurrentVersion,
                    [.. entries.Select(e => new StoredSelection(e.CandidateId, e.PositionId))]);
                await storage.SetItemAsync(StorageKey(slug), JsonSerializer.Serialize(stored, BallotJsonContext.Default.StoredBallot));
            }
        }
        catch (BrowserStorageUnavailableException ex)
        {
            MarkNotPersistent(ex);
        }
    }

    private void MarkNotPersistent(Exception ex)
    {
        if (!IsPersistent) return;
        logger.SessionStorageUnavailable(ex);
        IsPersistent = false;
        foreach (var slug in cache.Keys.ToList()) Changed?.Invoke(slug);
    }

    private static void RequireValidSlug(string slug)
    {
        if (!TenantSlug.IsValid(slug))
            throw new ArgumentException("Invalid municipality slug.", nameof(slug));
    }
}

// Storage format: {"version":2,"selections":[{"candidateId":3,"positionId":1}]}
internal sealed record StoredBallot(int Version, List<StoredSelection>? Selections);

internal sealed record StoredSelection(int CandidateId, int PositionId);

// Source-generated serializer: trimming-safe in published WebAssembly builds.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(StoredBallot))]
internal sealed partial class BallotJsonContext : JsonSerializerContext;
