using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PSNav.Models.Entitlements;

namespace PSNav.Services.Consoles;

public interface IPackageManifestService
{
    Task<string> GetManifestUrlAsync(Entitlement entitlement, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetPackageUrlsAsync(Entitlement entitlement,
        CancellationToken cancellationToken = default);
}
