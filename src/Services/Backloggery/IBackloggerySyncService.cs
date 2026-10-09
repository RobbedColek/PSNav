using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PSNav.Models.Backloggery;
using PSNav.Models.Common;
using PSNav.Models.Entitlements;

namespace PSNav.Services.Backloggery;

public interface IBackloggerySyncService
{
    BackloggerySession? Session { get; }

    Task<BackloggerySession> LoginAsync(string username, string password);

    Task<BackloggerySession> LoginWithTokenAsync(string logToken);

    Task<bool> ValidateSessionAsync();

    Task<BackloggerySyncData> FetchDataAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<BackloggerySyncItem> BuildPlan(BackloggerySyncData data, BackloggeryCrossPlatformMode mode);

    void SetIgnored(IEnumerable<BackloggerySyncItem> items, bool ignored);

    Task ApplyAsync(IReadOnlyList<BackloggerySyncItem> items, BackloggerySaveOptions saveOptions,
        IProgress<ProgressReport> progress,
        CancellationToken cancellationToken);
}

public sealed record BackloggerySyncData(IReadOnlyList<Entitlement> Entitlements, IReadOnlyList<BackloggeryGame> Library);

public sealed class BackloggerySessionExpiredException()
    : Exception("The Backloggery session has expired. Log in again to continue.");
