using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PSNav.Models.Backloggery;

namespace PSNav.Infrastructure.Backloggery;

public interface IBackloggeryClient
{
    void SetLogToken(string logToken);

    Task<BackloggerySession> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    Task<BackloggeryUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackloggeryGame>> GetLibraryAsync(string username, CancellationToken cancellationToken = default);

    Task<long> AddDigitalGameAsync(string title, string platform, BackloggerySaveOptions saveOptions,
        CancellationToken cancellationToken = default);
}

public sealed record BackloggeryUser(long UserId, string Username);
