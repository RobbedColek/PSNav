using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PSNav.Infrastructure.Backloggery;
using PSNav.Models.Backloggery;
using PSNav.Models.Common;
using PSNav.Models.Entitlements;
using PSNav.Repositories.Backloggery;
using PSNav.Repositories.Entitlements;
using PSNav.Repositories.Settings;

namespace PSNav.Services.Backloggery;

public sealed class BackloggerySyncService : IBackloggerySyncService
{
    private const string SessionSettingKey = "Backloggery:Session";
    private static readonly TimeSpan DelayBetweenAdds = TimeSpan.FromMilliseconds(500);
    private const int FailuresBeforeSessionCheck = 3;

    private readonly IBackloggeryClient _client;
    private readonly IBackloggerySyncRepository _syncRepository;
    private readonly IEntitlementsRepository _entitlementsRepository;
    private readonly ISettingsRepository _settingsRepository;

    public BackloggerySyncService(IBackloggeryClient client, IBackloggerySyncRepository syncRepository,
        IEntitlementsRepository entitlementsRepository, ISettingsRepository settingsRepository)
    {
        _client = client;
        _syncRepository = syncRepository;
        _entitlementsRepository = entitlementsRepository;
        _settingsRepository = settingsRepository;

        Session = _settingsRepository.Get<BackloggerySession>(SessionSettingKey);

        if (Session is not null)
            _client.SetLogToken(Session.LogToken);
    }

    public BackloggerySession? Session { get; private set; }

    public async Task<BackloggerySession> LoginAsync(string username, string password)
    {
        BackloggerySession session;

        try
        {
            session = await _client.LoginAsync(username, password);
        }
        catch
        {
            RestoreLogToken();
            throw;
        }

        SaveSession(session);
        return session;
    }

    public async Task<BackloggerySession> LoginWithTokenAsync(string logToken)
    {
        _client.SetLogToken(logToken);
        BackloggeryUser? user = await _client.GetCurrentUserAsync();

        if (user is null)
        {
            RestoreLogToken();
            throw new BackloggeryApiException("The session token is not valid.");
        }

        BackloggerySession session = new(user.Username, user.UserId, logToken, null);
        SaveSession(session);
        return session;
    }

    public async Task<bool> ValidateSessionAsync()
    {
        if (Session is null || Session.IsExpired)
            return false;

        BackloggeryUser? user = await _client.GetCurrentUserAsync();
        return user?.UserId == Session.UserId;
    }

    public async Task<BackloggerySyncData> FetchDataAsync(CancellationToken cancellationToken = default)
    {
        BackloggerySession session = Session ?? throw new BackloggerySessionExpiredException();

        Task<IReadOnlyList<Entitlement>> entitlementsTask =
            Task.Run(() => _entitlementsRepository.GetAllEntitlements(), cancellationToken);
        IReadOnlyList<BackloggeryGame> library = await _client.GetLibraryAsync(session.Username, cancellationToken);

        return new BackloggerySyncData(await entitlementsTask, library);
    }

    public IReadOnlyList<BackloggerySyncItem> BuildPlan(BackloggerySyncData data, BackloggeryCrossPlatformMode mode)
    {
        return BackloggeryMatcher.BuildPlan(data.Entitlements, _syncRepository.GetSyncedEntitlements(),
            data.Library, mode);
    }

    public void SetIgnored(IEnumerable<BackloggerySyncItem> items, bool ignored)
    {
        foreach (BackloggerySyncItem item in items.Where(item => !item.IsSynced && item.IsIgnored != ignored))
        {
            if (ignored)
            {
                _syncRepository.Save(item.AllEntitlementIds, BackloggerySyncStatus.Ignored, null, item.Platform);
                item.MarkIgnored();
            }
            else
            {
                _syncRepository.Remove(item.AllEntitlementIds);
                item.IsIgnored = false;
            }
        }
    }

    public async Task ApplyAsync(IReadOnlyList<BackloggerySyncItem> items, BackloggerySaveOptions saveOptions,
        IProgress<ProgressReport> progress,
        CancellationToken cancellationToken)
    {
        var toAdd = items.Where(item => item.IsSelected && item.IsSelectable)
            .Select(item => (Item: item, Title: item.Title.Trim(), Options: item.ResolveSaveOptions(saveOptions)))
            .ToList();

        if (!await ValidateSessionAsync())
            throw new BackloggerySessionExpiredException();

        int consecutiveFailures = 0;

        for (int i = 0; i < toAdd.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (BackloggerySyncItem item, string title, BackloggerySaveOptions options) = toAdd[i];

            progress.Report(new ProgressReport($"[{i + 1}/{toAdd.Count}] Adding {title} ({item.Platform})...",
                (double)i / toAdd.Count * 100));

            if (i > 0)
                await Task.Delay(DelayBetweenAdds, cancellationToken);

            try
            {
                long gameId = await _client.AddDigitalGameAsync(title, item.Platform, options, cancellationToken);

                // Every entitlement of the row is recorded, including the other platform of a paired row,
                // so that it is not shown again
                _syncRepository.Save(item.AllEntitlementIds, BackloggerySyncStatus.Added, gameId, item.Platform);
                item.MarkSynced(new BackloggeryGame(gameId, title,
                    BackloggeryPlatforms.GetPlatformId(item.Platform)!.Value, BackloggeryPlatforms.DigitalFormatId));
                consecutiveFailures = 0;
            }
            catch (Exception exception) when (exception is BackloggeryApiException or HttpRequestException)
            {
                item.Error = exception.Message;

                if (++consecutiveFailures >= FailuresBeforeSessionCheck && !await ValidateSessionAsync())
                    throw new BackloggerySessionExpiredException();
            }
        }

        progress.Report(new ProgressReport("Done", 100));
    }

    private void SaveSession(BackloggerySession session)
    {
        if (Session is not null && Session.UserId != session.UserId)
        {
            RestoreLogToken();
            throw new BackloggeryApiException(
                $"This is a different Backloggery account than '{Session.Username}', which PSNav is linked to.");
        }

        Session = session;
        _settingsRepository.Set(SessionSettingKey, session);
    }

    private void RestoreLogToken()
    {
        if (Session is not null)
            _client.SetLogToken(Session.LogToken);
    }
}
