namespace MEMS.Public.Services.Logging;

/// <summary>
/// Every log message the public site writes, in one place, using compile-time logging
/// (<see cref="LoggerMessageAttribute"/>). The generated methods check the log level before doing
/// any work, so disabled levels cost nothing.
///
/// Rules: never log a voter's guide selections or anything typed into search; log ids and
/// municipality slugs only. Levels are configured in wwwroot/appsettings*.json.
///
/// Event id ranges: 1xxx tenancy · 2xxx public directory · 3xxx voting guide · 4xxx UI · 9xxx mock API.
/// </summary>
internal static partial class Log
{
    // Longest slug we'll echo back into logs; anything longer is malformed anyway.
    private const int MaxLoggedSlugLength = 100;

    // ---- Tenancy ----
    [LoggerMessage(1001, LogLevel.Information, "Active municipality changed to {Slug}")]
    public static partial void TenantChanged(this ILogger logger, string slug);

    [LoggerMessage(1002, LogLevel.Debug, "Active municipality cleared")]
    public static partial void TenantCleared(this ILogger logger);

    [LoggerMessage(1003, LogLevel.Warning, "Rejected malformed municipality slug {Slug}")]
    private static partial void MalformedSlugCore(this ILogger logger, string slug);

    /// <summary>The slug comes straight from the URL, so it's length-limited before logging.</summary>
    public static void MalformedSlug(this ILogger logger, string slug) =>
        logger.MalformedSlugCore(slug.Length <= MaxLoggedSlugLength ? slug : slug[..MaxLoggedSlugLength] + "…");

    [LoggerMessage(1004, LogLevel.Error, "Failed to load municipality {Slug}")]
    public static partial void MunicipalityLoadFailed(this ILogger logger, Exception exception, string slug);

    // ---- Public directory ----
    [LoggerMessage(2001, LogLevel.Error, "Failed to load the municipality list")]
    public static partial void MunicipalityListLoadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(2002, LogLevel.Error, "Failed to load candidates for {Slug}")]
    public static partial void CandidatesLoadFailed(this ILogger logger, Exception exception, string slug);

    [LoggerMessage(2003, LogLevel.Error, "Failed to load candidate {CandidateId} for {Slug}")]
    public static partial void CandidateLoadFailed(this ILogger logger, Exception exception, int candidateId, string slug);

    [LoggerMessage(2004, LogLevel.Warning, "Failed to load positions for {Slug}; showing the municipality without them")]
    public static partial void PositionsLoadFailed(this ILogger logger, Exception exception, string slug);

    // ---- Voting guide ----
    [LoggerMessage(3001, LogLevel.Error, "Failed to load the voting guide for {Slug}")]
    public static partial void GuideLoadFailed(this ILogger logger, Exception exception, string slug);

    [LoggerMessage(3002, LogLevel.Error, "Failed to refresh voting guide display ({Component}) for {Slug}")]
    public static partial void GuideRefreshFailed(this ILogger logger, Exception exception, string component, string slug);

    [LoggerMessage(3003, LogLevel.Information, "Seat limit reached for position {PositionId}")]
    public static partial void SeatLimitReached(this ILogger logger, int positionId);

    [LoggerMessage(3004, LogLevel.Information,
        "Voting guide for {Slug}: removed {Dropped} candidates no longer running and {OverLimit} over a seat limit")]
    public static partial void GuideReconciled(this ILogger logger, string slug, int dropped, int overLimit);

    [LoggerMessage(3005, LogLevel.Warning, "Discarding unreadable voting guide for {Slug}")]
    public static partial void GuideUnreadable(this ILogger logger, Exception exception, string slug);

    [LoggerMessage(3006, LogLevel.Warning, "sessionStorage unavailable; the voting guide will only last until the page reloads")]
    public static partial void SessionStorageUnavailable(this ILogger logger, Exception exception);

    [LoggerMessage(3007, LogLevel.Error, "Failed to prepare the printable guide for {Slug}")]
    public static partial void PrintableGuideLoadFailed(this ILogger logger, Exception exception, string slug);

    // ---- UI ----
    [LoggerMessage(4001, LogLevel.Warning, "window.print() failed")]
    public static partial void PrintFailed(this ILogger logger, Exception exception);

    [LoggerMessage(4002, LogLevel.Warning, "Could not apply the colour theme")]
    public static partial void ThemeFailed(this ILogger logger, Exception exception);

    // ---- Mock API (development only) ----
    [LoggerMessage(9001, LogLevel.Debug, "Mock API returned {Count} municipalities")]
    public static partial void MockMunicipalitiesReturned(this ILogger logger, int count);

    [LoggerMessage(9002, LogLevel.Debug, "Mock API: municipality {Slug} not found")]
    public static partial void MockMunicipalityNotFound(this ILogger logger, string slug);

    [LoggerMessage(9003, LogLevel.Debug, "Mock API returned {Count} positions for {Slug}")]
    public static partial void MockPositionsReturned(this ILogger logger, int count, string slug);

    [LoggerMessage(9004, LogLevel.Debug, "Mock API returned {Count} candidates for {Slug}")]
    public static partial void MockCandidatesReturned(this ILogger logger, int count, string slug);

    [LoggerMessage(9005, LogLevel.Debug, "Mock API: candidate {CandidateId} not found in {Slug}")]
    public static partial void MockCandidateNotFound(this ILogger logger, int candidateId, string slug);
}
