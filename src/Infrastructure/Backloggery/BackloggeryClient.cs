using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PSNav.Models.Backloggery;

namespace PSNav.Infrastructure.Backloggery;

// Backloggery has no public API.
// These are the endpoints used by its own website.
// Every endpoint except fetch_library.php wraps the result in { "msg": "...", "payload": ... },
// where a non-empty "msg" is an error.
public sealed class BackloggeryClient : IBackloggeryClient
{
    private const string LogTokenCookieName = "log_token";
    private static readonly Uri BaseUri = new("https://backloggery.com/");

    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _httpClient;

    public BackloggeryClient()
    {
        _httpClient = new HttpClient(new HttpClientHandler { CookieContainer = _cookies })
        {
            BaseAddress = BaseUri
        };
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Add("Origin", "https://backloggery.com");
        _httpClient.DefaultRequestHeaders.Referrer = BaseUri;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PSNav");
    }

    public void SetLogToken(string logToken)
    {
        _cookies.Add(new Cookie(LogTokenCookieName, logToken, "/", ".backloggery.com") { Secure = true, HttpOnly = true });
    }

    public async Task<BackloggerySession> LoginAsync(string username, string password,
        CancellationToken cancellationToken = default)
    {
        JsonElement payload = await PostAsync("api/auth_login.php", new
        {
            usermail = username,
            password,
            remember = true
        }, cancellationToken);

        BackloggeryUser user = ReadUser(payload)
                               ?? throw new BackloggeryApiException("Login failed. Check your username and password.");

        Cookie? cookie = _cookies.GetCookies(BaseUri)[LogTokenCookieName];

        if (cookie is null || string.IsNullOrWhiteSpace(cookie.Value))
            throw new BackloggeryApiException("Login succeeded, but Backloggery did not return a session token.");

        DateTimeOffset? expiresAt = cookie.Expires == DateTime.MinValue ? null : new DateTimeOffset(cookie.Expires);

        return new BackloggerySession(user.Username, user.UserId, cookie.Value, expiresAt);
    }

    public async Task<BackloggeryUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Without a body auth_login.php only reports who is logged in (empty when nobody is)
            JsonElement payload = await PostAsync("api/auth_login.php", null, cancellationToken);
            return ReadUser(payload);
        }
        catch (BackloggeryApiException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<BackloggeryGame>> GetLibraryAsync(string username,
        CancellationToken cancellationToken = default)
    {
        using JsonDocument document = await SendAsync("api/fetch_library.php", new
        {
            type = "load_user_library",
            username
        }, cancellationToken);

        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new BackloggeryApiException("Unexpected response when fetching the Backloggery library.");

        List<BackloggeryGame> games = [];

        foreach (JsonElement game in root.EnumerateArray())
        {
            long? gameId = ReadInt64(game, "game_inst_id");
            string? title = game.TryGetProperty("title", out JsonElement titleElement) ? titleElement.GetString() : null;
            long? platformId = ReadInt64(game, "platform_id");
            long? formatId = ReadInt64(game, "phys_digi");

            if (gameId is null || string.IsNullOrWhiteSpace(title) || platformId is null)
                continue;

            games.Add(new BackloggeryGame(gameId.Value, title, (int)platformId.Value, (int)(formatId ?? 0)));
        }

        return games;
    }

    public async Task<long> AddDigitalGameAsync(string title, string platform, BackloggerySaveOptions saveOptions,
        CancellationToken cancellationToken = default)
    {
        int platformId = BackloggeryPlatforms.GetPlatformId(platform)
                         ?? throw new ArgumentException($"Unsupported platform '{platform}'.", nameof(platform));

        JsonElement payload = await PostAsync("api/add_game.php", new
        {
            platform_id = platformId,
            priority = (int)saveOptions.Priority,
            status = (int)saveOptions.Status,
            phys_digi = BackloggeryPlatforms.DigitalFormatId,
            notes = "",
            own = 1,
            region = 0,
            abbr = platform,
            platform_title = BackloggeryPlatforms.GetPlatformTitle(platform),
            title,
            is_stealth = saveOptions.UseStealthSave,
            update_parent = false
        }, cancellationToken);

        return ReadInt64(payload)
               ?? throw new BackloggeryApiException($"Backloggery did not return an ID for '{title}'.");
    }

    private async Task<JsonElement> PostAsync(string endpoint, object? body, CancellationToken cancellationToken)
    {
        using JsonDocument document = await SendAsync(endpoint, body, cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new BackloggeryApiException($"Unexpected response from {endpoint}.");

        JsonElement payload = UnwrapEnvelope(document.RootElement);
        return payload.ValueKind == JsonValueKind.Undefined ? default : payload.Clone();
    }

    private async Task<JsonDocument> SendAsync(string endpoint, object? body, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, endpoint);

        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new BackloggeryApiException(
                $"Backloggery returned HTTP {(int)response.StatusCode} for {endpoint}.");

        string content = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            return JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            throw new BackloggeryApiException($"Backloggery returned an invalid response for {endpoint}.");
        }
    }

    private static JsonElement UnwrapEnvelope(JsonElement root)
    {
        if (root.TryGetProperty("msg", out JsonElement message) &&
            message.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(message.GetString()))
        {
            throw new BackloggeryApiException(message.GetString()!);
        }

        return root.TryGetProperty("payload", out JsonElement payload) ? payload : default;
    }

    private static BackloggeryUser? ReadUser(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            return null;

        long? userId = ReadInt64(payload, "user_id");
        string? username = payload.TryGetProperty("username", out JsonElement element) ? element.GetString() : null;

        return userId is null || string.IsNullOrWhiteSpace(username) ? null : new BackloggeryUser(userId.Value, username);
    }

    private static long? ReadInt64(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out JsonElement value)
            ? ReadInt64(value)
            : null;
    }

    // Backloggery mixes numbers and numeric strings (e.g. add_game.php returns "25117694")
    private static long? ReadInt64(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out long number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out long number) => number,
            _ => null
        };
    }
}
