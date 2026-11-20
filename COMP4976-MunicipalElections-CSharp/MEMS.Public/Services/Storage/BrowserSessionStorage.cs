using Microsoft.JSInterop;

namespace MEMS.Public.Services.Storage;

/// <summary><see cref="IBrowserSessionStorage"/> over JS interop. Calls the built-in Storage API directly; no custom JS file needed.</summary>
public sealed class BrowserSessionStorage(IJSRuntime js) : IBrowserSessionStorage
{
    public async ValueTask<string?> GetItemAsync(string key)
    {
        try { return await js.InvokeAsync<string?>("sessionStorage.getItem", key); }
        catch (JSException ex) { throw new BrowserStorageUnavailableException(ex); }
    }

    public async ValueTask SetItemAsync(string key, string value)
    {
        try { await js.InvokeVoidAsync("sessionStorage.setItem", key, value); }
        catch (JSException ex) { throw new BrowserStorageUnavailableException(ex); }
    }

    public async ValueTask RemoveItemAsync(string key)
    {
        try { await js.InvokeVoidAsync("sessionStorage.removeItem", key); }
        catch (JSException ex) { throw new BrowserStorageUnavailableException(ex); }
    }
}
