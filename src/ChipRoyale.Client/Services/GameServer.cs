using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ChipRoyale.Shared;

namespace ChipRoyale.Client.Services;

/// <summary>Login tokens returned by the server's account API.</summary>
public sealed record AuthTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, string Email);

/// <summary>
/// Connection to the Chip Royale server (database). Knows the server address, whether it is
/// reachable, and the login tokens. If the server cannot be reached the game runs in offline mode.
/// </summary>
public sealed class GameServer(HttpClient http, BrowserStorage storage, IConfiguration config)
{
    private const string ServerKey = "cr-server";
    private const string AuthKey = "cr-auth";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private AuthTokens? tokens;
    private bool initialized;

    /// <summary>Value of <see cref="BaseUrl"/> meaning "no server": the game runs offline (e.g. on GitHub Pages).</summary>
    public const string NoServer = "none";

    /// <summary>Server address, e.g. http://localhost:5132 ("" = same address as the game, "none" = no server).</summary>
    public string BaseUrl { get; private set; } = "";

    /// <summary>False when no server is set up: nothing is requested, the game starts offline.</summary>
    public bool HasServer => !string.Equals(BaseUrl, NoServer, StringComparison.OrdinalIgnoreCase);

    public bool IsOnline { get; private set; }
    public bool IsLoggedIn => tokens is not null;
    public string? Email => tokens?.Email;

    /// <summary>Address shown to the player (the page's own address when BaseUrl is empty).</summary>
    public string DisplayUrl => !HasServer ? "" : string.IsNullOrEmpty(BaseUrl) ? http.BaseAddress?.GetLeftPart(UriPartial.Authority) ?? "" : BaseUrl;

    public async Task InitializeAsync()
    {
        if (initialized) return;
        initialized = true;
        BaseUrl = (await storage.GetStringAsync(ServerKey)) ?? config["ApiBaseUrl"] ?? "";
        tokens = await storage.GetAsync<AuthTokens>(AuthKey);
        await CheckAsync();
    }

    /// <summary>Pings the server (2.5 s timeout) and updates <see cref="IsOnline"/>.</summary>
    public async Task<bool> CheckAsync()
    {
        if (!HasServer) return IsOnline = false;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
            using var response = await http.GetAsync(Url(ApiRoutes.Health), cts.Token);
            IsOnline = response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            IsOnline = false;
        }
        return IsOnline;
    }

    public async Task SetBaseUrlAsync(string url)
    {
        url = url.Trim().TrimEnd('/');
        // An empty field means "no server" wherever the page comes from GitHub Pages and the like.
        BaseUrl = url.Length == 0 && config["ApiBaseUrl"] == NoServer ? NoServer : url;
        await storage.SetStringAsync(ServerKey, BaseUrl);
        await CheckAsync();
    }

    // ---------------------------------------------------------------- accounts

    /// <returns>Error codes from the server (empty on success).</returns>
    public async Task<IReadOnlyList<string>> RegisterAsync(string email, string password)
    {
        using var response = await http.PostAsJsonAsync(Url($"{ApiRoutes.Auth}/register"), new { email, password });
        if (response.IsSuccessStatusCode) return [];
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
            if (problem.TryGetProperty("errors", out var errors))
                return [.. errors.EnumerateObject().Select(e => e.Name)];
        }
        catch (JsonException) { }
        return ["DefaultError"];
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        using var response = await http.PostAsJsonAsync(Url($"{ApiRoutes.Auth}/login?useCookies=false"), new { email, password });
        if (!response.IsSuccessStatusCode) return false;
        await StoreTokensAsync(response, email);
        return true;
    }

    public async Task LogoutAsync()
    {
        tokens = null;
        await storage.RemoveAsync(AuthKey);
    }

    // ---------------------------------------------------------------- game API

    public Task<T?> GetAsync<T>(string route) => SendAsync<T>(HttpMethod.Get, route, null);

    public Task<T?> PostAsync<T>(string route, object? body = null) => SendAsync<T>(HttpMethod.Post, route, body);

    private async Task<T?> SendAsync<T>(HttpMethod method, string route, object? body, bool retried = false)
    {
        using var request = new HttpRequestMessage(method, Url(route));
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        if (tokens is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        using var response = await http.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized && tokens is not null && !retried)
        {
            if (await RefreshAsync()) return await SendAsync<T>(method, route, body, retried: true);
            await LogoutAsync();
            throw new UnauthorizedAccessException();
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(Json);
    }

    private async Task<bool> RefreshAsync()
    {
        if (tokens is null) return false;
        using var response = await http.PostAsJsonAsync(Url($"{ApiRoutes.Auth}/refresh"), new { refreshToken = tokens.RefreshToken });
        if (!response.IsSuccessStatusCode) return false;
        await StoreTokensAsync(response, tokens.Email);
        return true;
    }

    private async Task StoreTokensAsync(HttpResponseMessage response, string email)
    {
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        tokens = new AuthTokens(
            result.GetProperty("accessToken").GetString()!,
            result.GetProperty("refreshToken").GetString()!,
            DateTimeOffset.UtcNow.AddSeconds(result.GetProperty("expiresIn").GetInt32()),
            email);
        await storage.SetAsync(AuthKey, tokens);
    }

    private string Url(string route) => string.IsNullOrEmpty(BaseUrl) ? route : $"{BaseUrl}/{route}";
}
