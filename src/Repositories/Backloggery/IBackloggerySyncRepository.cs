using System.Collections.Generic;
using PSNav.Models.Backloggery;

namespace PSNav.Repositories.Backloggery;

public interface IBackloggerySyncRepository
{
    IReadOnlyDictionary<string, BackloggerySyncRecord> GetSyncedEntitlements();

    void Save(IEnumerable<string> entitlementIds, BackloggerySyncStatus status, long? backloggeryGameId,
        string? platform);

    void Remove(IEnumerable<string> entitlementIds);

    void ClearAccountData();
}
