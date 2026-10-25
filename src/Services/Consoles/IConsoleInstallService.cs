using System.Threading;
using System.Threading.Tasks;
using PSNav.Models.Consoles;
using PSNav.Models.Entitlements;

namespace PSNav.Services.Consoles;

public interface IConsoleInstallService
{
    Task SendToConsoleAsync(Entitlement entitlement, ConsoleModel console,
        CancellationToken cancellationToken = default);
}
