using System;
using System.Threading.Tasks;
using PSNav.Models.Accounts;
using PSNav.Models.Common;

namespace PSNav.Services.Accounts;

public interface IAccountService
{
    Task<Account> CreateAccountFromSsoCookieJsonAsync(string json);

    Task SynchronizeAccountEntitlementsAsync(Account account, Func<Task>? onBatchProcessed = null,
        IProgress<ProgressReport>? progress = null);
}