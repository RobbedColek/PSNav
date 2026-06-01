using System.Collections.Generic;
using PSNav.Models.Entitlements;

namespace PSNav.Repositories.Entitlements;

public interface IEntitlementsRepository
{
    IReadOnlyList<Entitlement> GetAllEntitlements();
    bool Exists(string entitlementId);
    void UpsertBatch(string accountId, IEnumerable<Entitlement> entitlements);
    void LinkAccountEntitlement(string accountId, Entitlement entitlement);
}