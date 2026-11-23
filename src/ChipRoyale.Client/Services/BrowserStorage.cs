using System.Text.Json;
using Microsoft.JSInterop;

namespace ChipRoyale.Client.Services;

/// <summary>Small wrapper around localStorage (see window.chipRoyale in index.html).</summary>
public sealed class BrowserStorage(IJSRuntime js)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key)
    {
        var text = await js.InvokeAsync<string?>("chipRoyale.storageGet", key);
        if (string.IsNullOrEmpty(text)) return default;
        try { return JsonSerializer.Deserialize<T>(text, Json); }
        catch (JsonException) { return default; }
    }

    public ValueTask SetAsync<T>(string key, T value) =>
        js.InvokeVoidAsync("chipRoyale.storageSet", key, JsonSerializer.Serialize(value, Json));

    public ValueTask<string?> GetStringAsync(string key) => js.InvokeAsync<string?>("chipRoyale.storageGet", key);

    public ValueTask SetStringAsync(string key, string value) => js.InvokeVoidAsync("chipRoyale.storageSet", key, value);

    public ValueTask RemoveAsync(string key) => js.InvokeVoidAsync("chipRoyale.storageRemove", key);
}
