namespace MEMS.Public.Services.Storage;

/// <summary>
/// The browser's <c>window.sessionStorage</c>: string values that live only in the current tab and
/// disappear when it closes. Nothing stored here is ever sent to a server.
/// </summary>
public interface IBrowserSessionStorage
{
    /// <exception cref="BrowserStorageUnavailableException">Storage is blocked or unavailable.</exception>
    ValueTask<string?> GetItemAsync(string key);

    /// <exception cref="BrowserStorageUnavailableException">Storage is blocked, unavailable, or full.</exception>
    ValueTask SetItemAsync(string key, string value);

    /// <exception cref="BrowserStorageUnavailableException">Storage is blocked or unavailable.</exception>
    ValueTask RemoveItemAsync(string key);
}

/// <summary>sessionStorage could not be used (private mode restrictions, disabled site data, quota).</summary>
public sealed class BrowserStorageUnavailableException(Exception inner)
    : Exception("Browser session storage is unavailable.", inner);
