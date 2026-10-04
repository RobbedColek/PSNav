using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PSNav.Infrastructure.PlayStationApi;
using PSNav.Models.Accounts;
using PSNav.Models.Common;
using PSNav.Models.Entitlements;
using PSNav.Repositories.Entitlements;

namespace PSNav.Services.Accounts;

public sealed class AccountService(IEntitlementsRepository entitlementsRepository, IPlayStationApiClient apiClient)
    : IAccountService
{
    private const int NpssoTokenLength = 64;

    public async Task<Account> CreateAccountFromSsoCookieJsonAsync(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Paste the JSON output.");

        SonySsoCookieResponse? response;

        try
        {
            response = JsonSerializer.Deserialize<SonySsoCookieResponse>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("The pasted content is not valid JSON.");
        }

        if (response is null)
            throw new InvalidOperationException("The pasted JSON could not be read.");

        if (string.IsNullOrWhiteSpace(response.Npsso))
            throw new InvalidOperationException("The JSON must contain a non-empty \"npsso\" value.");

        if (response.ExpiresIn <= 0)
            throw new InvalidOperationException("The JSON must contain an \"expires_in\" value greater than zero.");

        if (response.Npsso.Length != NpssoTokenLength)
            throw new InvalidOperationException($"The \"npsso\" value must be exactly {NpssoTokenLength} characters long.");

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (response.ExpiresIn > (long)(DateTimeOffset.MaxValue - now).TotalSeconds)
            throw new InvalidOperationException("The \"expires_in\" value is too large.");

        PlayStationAccountIdentity identity =
            await apiClient.GetAccountIdentityFromNpssoAsync(response.Npsso);

        return new Account
        {
            AccountId = identity.AccountId,
            OnlineId = identity.OnlineId,
            ProfilePicture = identity.ProfilePicture,
            AvatarPicture = identity.AvatarPicture,
            NpssoToken = response.Npsso,
            NpssoTokenExpiration = now.AddSeconds(response.ExpiresIn),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public async Task<string> GetAccessTokenAsync(Account account)
    {
        if (string.IsNullOrWhiteSpace(account.NpssoToken))
            throw new InvalidOperationException("Account does not have an NPSSO token.");

        if (!string.IsNullOrWhiteSpace(account.AccessToken))
            return account.AccessToken;

        string authCode = await apiClient.GetAuthorizationCodeAsync(account.NpssoToken);
        string accessToken = await apiClient.GetAccessTokenAsync(authCode);

        account.AccessToken = accessToken;

        return accessToken;
    }

    public async Task SynchronizeAccountEntitlementsAsync(Account account, Func<Task>? onBatchProcessed = null,
        IProgress<ProgressReport>? progress = null)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[DEBUG_LOG] Synchronizing entitlements for account: {account.OnlineId} ({account.AccountId})");

        progress?.Report(new ProgressReport("Obtaining access token..."));
        string accessToken = await GetAccessTokenAsync(account);
        System.Diagnostics.Debug.WriteLine($"[DEBUG_LOG] Obtained access token for {account.OnlineId}");

        int totalProcessedCount = 0;

        await apiClient.GetEntitlementsAsync(accessToken, async (pageEntitlements, totalCount) =>
        {
            if (totalCount <= 0 && pageEntitlements.Count > 0)
            {
                totalCount = pageEntitlements.Count;
            }

            var processingTasks = pageEntitlements.Select(async entitlement =>
            {
                if (entitlementsRepository.Exists(entitlement.EntitlementId))
                {
                    entitlementsRepository.LinkAccountEntitlement(account.AccountId, entitlement);
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(entitlement.ImageUrl))
                {
                    entitlement.Image = await apiClient.DownloadImageOrNullAsync(entitlement.ImageUrl);
                }

                return entitlement;
            });

            var processedResults = await Task.WhenAll(processingTasks);
            var entitlementsToUpsert = processedResults.Where(e => e != null).Cast<Entitlement>().ToList();

            if (entitlementsToUpsert.Count > 0)
            {
                entitlementsRepository.UpsertBatch(account.AccountId, entitlementsToUpsert);
            }

            totalProcessedCount += pageEntitlements.Count;
            progress?.Report(new ProgressReport($"Processing entitlement {totalProcessedCount}/{totalCount}...",
                (double)totalProcessedCount / totalCount * 100));

            if (onBatchProcessed != null)
            {
                await onBatchProcessed();
            }
        }, progress);
    }

    private sealed class SonySsoCookieResponse
    {
        [JsonPropertyName("npsso")] public string? Npsso { get; init; }

        [JsonPropertyName("expires_in")] public long ExpiresIn { get; init; }
    }
}