using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PSNav.Models.Common;
using PSNav.Models.Entitlements;
using SkiaSharp;

namespace PSNav.Infrastructure.PlayStationApi;

public sealed class PlayStationApiClient : IPlayStationApiClient
{
    private const string OAuthClientAuthorization =
        "Basic MDk1MTUxNTktNzIzNy00MzcwLTliNDAtMzgwNmU2N2MwODkxOnVjUGprYTV0bnRCMktxc1A=";

    private readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

    public async Task<PlayStationAccountIdentity> GetAccountIdentityFromNpssoAsync(string npsso)
    {
        string code = await GetAuthorizationCodeAsync(npsso);
        string accessToken = await GetAccessTokenAsync(code);
        string accountId = await GetAccountIdAsync(accessToken);
        PlayStationAccountProfile profile = await GetAccountProfileAsync(accessToken, accountId);

        return new PlayStationAccountIdentity(
            ConvertAccountIdToHex(accountId),
            profile.OnlineId,
            profile.ProfilePicture,
            profile.AvatarPicture);
    }

    public async Task<string> GetAuthorizationCodeAsync(string npsso)
    {
        const string authorizeUrl =
            "https://ca.account.sony.com/api/authz/v3/oauth/authorize" +
            "?access_type=offline" +
            "&client_id=09515159-7237-4370-9b40-3806e67c0891" +
            "&redirect_uri=com.scee.psxandroid.scecompcall://redirect" +
            "&response_type=code" +
            "&scope=psn:mobile.v2.core%20psn:clientapp";

        using HttpRequestMessage request = new(HttpMethod.Get, authorizeUrl);
        request.Headers.Add("Cookie", $"npsso={npsso}");

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead);

        if (response.StatusCode is not HttpStatusCode.Found and not HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException("Could not obtain authorization code from NPSSO token.");
        }

        Uri? location = response.Headers.Location;

        if (location is null)
        {
            throw new InvalidOperationException("Authorization response did not contain a redirect location.");
        }

        string? code = GetQueryParameter(location, "code");

        return string.IsNullOrWhiteSpace(code)
            ? throw new InvalidOperationException("Authorization redirect did not contain a valid code.")
            : code;
    }

    public async Task<string> GetAccessTokenAsync(string code)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            "https://ca.account.sony.com/api/authz/v3/oauth/token");

        request.Headers.TryAddWithoutValidation("Authorization", OAuthClientAuthorization);

        request.Content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", "com.scee.psxandroid.scecompcall://redirect"),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("token_format", "jwt")
        ]);

        using HttpResponseMessage response = await _httpClient.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Could not exchange authorization code for access token.");
        }

        OAuthTokenResponse? tokenResponse = JsonSerializer.Deserialize<OAuthTokenResponse>(
            content,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return string.IsNullOrWhiteSpace(tokenResponse?.AccessToken)
            ? throw new InvalidOperationException("Token response did not contain an access token.")
            : tokenResponse.AccessToken;
    }

    private async Task<string> GetAccountIdAsync(string accessToken)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "https://dms.api.playstation.com/api/v1/devices/accounts/me");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using HttpResponseMessage response = await _httpClient.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Could not obtain Account ID.");
        }

        AccountIdResponse? accountResponse = JsonSerializer.Deserialize<AccountIdResponse>(
            content,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return string.IsNullOrWhiteSpace(accountResponse?.AccountId)
            ? throw new InvalidOperationException("Account response did not contain an Account ID.")
            : accountResponse.AccountId;
    }

    private async Task<PlayStationAccountProfile> GetAccountProfileAsync(string accessToken, string accountId)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"https://m.np.playstation.com/api/userProfile/v1/internal/users/{Uri.EscapeDataString(accountId)}/profiles");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage response = await _httpClient.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Could not obtain account profile.");
        }

        AccountProfileResponse? profileResponse = JsonSerializer.Deserialize<AccountProfileResponse>(
            content,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (string.IsNullOrWhiteSpace(profileResponse?.OnlineId))
        {
            throw new InvalidOperationException("Profile response did not contain an Online ID.");
        }

        string? profilePictureUrl = GetLargestImageUrl(profileResponse.PersonalDetail?.ProfilePictures);
        string? avatarPictureUrl = GetLargestImageUrl(profileResponse.Avatars);

        byte[]? profilePicture = await DownloadImageOrNullAsync(profilePictureUrl);
        byte[]? avatarPicture = await DownloadImageOrNullAsync(avatarPictureUrl);

        return new PlayStationAccountProfile(
            profileResponse.OnlineId,
            profilePicture,
            avatarPicture);
    }

    public async Task GetEntitlementsAsync(string accessToken,
        Func<IReadOnlyList<Entitlement>, int, Task> onPageReceived, IProgress<ProgressReport>? progress = null)
    {
        const string hashGetGames = "827a423f6a8ddca4107ac01395af2ec0eafd8396fc7fa204aaf9b7ed2eefa168";
        const int pageSize = 100;
        int start = 0;

        while (true)
        {
            progress?.Report(new ProgressReport("Fetching entitlements..."));
            var variables = new
            {
                isActive = true,
                platform = new[] { "ps4", "ps5" },
                size = pageSize,
                start,
                sortBy = "ACTIVE_DATE",
                sortDirection = "desc"
            };

            var extensions = new
            {
                persistedQuery = new
                {
                    version = 1,
                    sha256Hash = hashGetGames
                }
            };

            string variablesJson = JsonSerializer.Serialize(variables);
            string extensionsJson = JsonSerializer.Serialize(extensions);

            string url = $"https://web.np.playstation.com/api/graphql/v1/op" +
                         $"?operationName=getPurchasedGameList" +
                         $"&variables={Uri.EscapeDataString(variablesJson)}" +
                         $"&extensions={Uri.EscapeDataString(extensionsJson)}";

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Headers.Add("apollo-require-preflight", "true");
            request.Headers.Add("apollographql-client-name", "my-playstation");
            request.Headers.Add("Referer", "https://library.playstation.com/");

            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                break;
            }

            PurchasedTitlesResponse? apiResponse = JsonSerializer.Deserialize<PurchasedTitlesResponse>(responseBody);
            int totalCount = apiResponse?.Data?.PurchasedTitlesRetrieve?.PageInfo?.TotalCount ?? 0;

            if (apiResponse?.Data?.PurchasedTitlesRetrieve?.Games == null)
            {
                break;
            }

            List<Entitlement> pageEntitlements = [];
            foreach (PurchasedTitlesResponse.GameEntry game in apiResponse.Data.PurchasedTitlesRetrieve.Games)
            {
                pageEntitlements.Add(new Entitlement
                {
                    EntitlementId = game.EntitlementId ?? string.Empty,
                    TitleId = game.TitleId,
                    ProductId = game.ProductId,
                    Name = game.Name,
                    ImageUrl = game.Image?.Url,
                    Platform = game.Platform,
                    Membership = game.Membership,
                    IsActive = game.IsActive
                });
            }

            await onPageReceived(pageEntitlements, totalCount);

            if (apiResponse.Data.PurchasedTitlesRetrieve.PageInfo?.IsLast == true ||
                apiResponse.Data.PurchasedTitlesRetrieve.Games.Count == 0)
            {
                break;
            }

            start += pageSize;
        }
    }

    private sealed class PurchasedTitlesResponse
    {
        [JsonPropertyName("data")] public DataContainer? Data { get; init; }

        public sealed class DataContainer
        {
            [JsonPropertyName("purchasedTitlesRetrieve")]
            public PurchasedTitlesRetrieve? PurchasedTitlesRetrieve { get; set; }
        }

        public sealed class PurchasedTitlesRetrieve
        {
            [JsonPropertyName("games")] public List<GameEntry>? Games { get; set; }

            [JsonPropertyName("pageInfo")] public PageInfo? PageInfo { get; set; }
        }

        public sealed class GameEntry
        {
            [JsonPropertyName("entitlementId")] public string? EntitlementId { get; set; }

            [JsonPropertyName("titleId")] public string? TitleId { get; set; }

            [JsonPropertyName("productId")] public string? ProductId { get; set; }

            [JsonPropertyName("name")] public string? Name { get; set; }

            [JsonPropertyName("image")] public ImageInfo? Image { get; set; }

            [JsonPropertyName("platform")] public string? Platform { get; set; }

            [JsonPropertyName("membership")] public string? Membership { get; set; }

            [JsonPropertyName("isActive")] public bool? IsActive { get; set; }
        }

        public sealed class ImageInfo
        {
            [JsonPropertyName("url")] public string? Url { get; set; }
        }

        public sealed class PageInfo
        {
            [JsonPropertyName("totalCount")] public int TotalCount { get; set; }
            [JsonPropertyName("isLast")] public bool IsLast { get; set; }
        }
    }

    private static string? GetLargestImageUrl(IReadOnlyList<PlayStationImageResponse>? images)
    {
        return images?
            .Where(image => !string.IsNullOrWhiteSpace(image.Url))
            .OrderByDescending(image => GetImageSizeRank(image.Size))
            .Select(image => image.Url)
            .FirstOrDefault();
    }

    private static int GetImageSizeRank(string? size)
    {
        return size?.ToLowerInvariant() switch
        {
            "xl" => 4,
            "l" => 3,
            "m" => 2,
            "s" => 1,
            _ => 0
        };
    }

    public async Task<byte[]?> DownloadImageOrNullAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        using HttpResponseMessage response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        byte[] rawBytes = await response.Content.ReadAsByteArrayAsync();
        return ConvertToWebP(rawBytes);
    }

    private static byte[]? ConvertToWebP(byte[] imageBytes)
    {
        try
        {
            using MemoryStream inputStream = new(imageBytes);
            using SKBitmap? bitmap = SKBitmap.Decode(inputStream);

            if (bitmap == null)
            {
                return imageBytes; // Return original if decoding fails
            }

            using SKImage? image = SKImage.FromBitmap(bitmap);
            using SKData? data = image.Encode(SKEncodedImageFormat.Webp, 80);

            return data == null ? imageBytes : data.ToArray();
        }
        catch
        {
            return imageBytes; // Fallback to original
        }
    }

    private static string? GetQueryParameter(Uri uri, string name)
    {
        string query = uri.Query;

        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        foreach (string part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = part.Split('=', 2);

            if (pair.Length == 2 && pair[0] == name)
            {
                return Uri.UnescapeDataString(pair[1]);
            }
        }

        return null;
    }

    private static string ConvertAccountIdToHex(string accountId)
    {
        return !ulong.TryParse(accountId, NumberStyles.None, CultureInfo.InvariantCulture, out ulong decimalAccountId)
            ? throw new InvalidOperationException("Account ID returned by PlayStation was not a valid decimal number.")
            : decimalAccountId.ToString("X", CultureInfo.InvariantCulture);
    }

    private sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    }

    private sealed class AccountIdResponse
    {
        [JsonPropertyName("accountId")] public string? AccountId { get; set; }
    }

    private sealed class AccountProfileResponse
    {
        [JsonPropertyName("onlineId")] public string? OnlineId { get; set; }

        [JsonPropertyName("personalDetail")] public PersonalDetailResponse? PersonalDetail { get; set; }

        [JsonPropertyName("avatars")] public List<PlayStationImageResponse>? Avatars { get; set; }
    }

    private sealed class PersonalDetailResponse
    {
        [JsonPropertyName("profilePictures")] public List<PlayStationImageResponse>? ProfilePictures { get; set; }
    }

    private sealed class PlayStationImageResponse
    {
        [JsonPropertyName("size")] public string? Size { get; set; }

        [JsonPropertyName("url")] public string? Url { get; set; }
    }
}

public sealed record PlayStationAccountIdentity(
    string AccountId,
    string OnlineId,
    byte[]? ProfilePicture,
    byte[]? AvatarPicture);

public sealed record PlayStationAccountProfile(
    string OnlineId,
    byte[]? ProfilePicture,
    byte[]? AvatarPicture);