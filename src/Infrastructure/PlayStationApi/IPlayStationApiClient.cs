using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PSNav.Models.Common;
using PSNav.Models.Entitlements;

namespace PSNav.Infrastructure.PlayStationApi;

public interface IPlayStationApiClient
{
    Task<PlayStationAccountIdentity> GetAccountIdentityFromNpssoAsync(string npsso);
    Task<string> GetAuthorizationCodeAsync(string npsso);
    Task<string> GetAccessTokenAsync(string code);

    Task GetEntitlementsAsync(string accessToken, Func<IReadOnlyList<Entitlement>, int, Task> onPageReceived,
        IProgress<ProgressReport>? progress = null);

    Task<byte[]?> DownloadImageOrNullAsync(string? url);
}