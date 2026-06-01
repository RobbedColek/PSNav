using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PSNav.Models.Accounts;
using PSNav.Models.Common;
using PSNav.Models.Messages;
using PSNav.Repositories.Accounts;
using PSNav.Services.Accounts;

namespace PSNav.ViewModels.Accounts;

public sealed partial class AccountsViewModel(IAccountsRepository accountRepository, IAccountService accountService)
    : ViewModelBase
{
    public ObservableCollection<Account> Accounts { get; } = [];

    [ObservableProperty] private Account? _selectedAccount;

    [ObservableProperty] private bool _isSynchronizing;

    [ObservableProperty] private string _syncStatus = string.Empty;

    [ObservableProperty] private double _syncProgress;

    [RelayCommand]
    private void LoadAccounts()
    {
        if (Accounts.Count > 0) return;
        RefreshAccounts();
    }

    private void RefreshAccounts()
    {
        Accounts.Clear();

        foreach (Account account in accountRepository.GetAll())
        {
            Accounts.Add(account);
        }
    }

    public void SaveAccount(Account account)
    {
        accountRepository.Upsert(account);
        RefreshAccounts();
        WeakReferenceMessenger.Default.Send(new AccountAddedMessage());
    }

    public string GenerateOfflineAccountId()
    {
        var accounts = accountRepository.GetAll();
        long maxId = -1;
        const string prefix = "DEADBEEF";

        foreach (Account account in accounts)
        {
            if (!account.AccountId.StartsWith(prefix) || account.AccountId.Length != 16) continue;
            if (!long.TryParse(account.AccountId.AsSpan(prefix.Length), System.Globalization.NumberStyles.HexNumber,
                    null, out long currentId)) continue;
            if (currentId <= maxId) continue;
            maxId = currentId;
        }

        return $"{prefix}{maxId + 1:X8}";
    }

    public void UpdateAccount(Account account)
    {
        accountRepository.Update(account);

        RefreshAccounts();
    }

    [RelayCommand]
    public void DeleteAccount(Account account)
    {
        accountRepository.Delete(account.AccountId);

        RefreshAccounts();
    }

    [RelayCommand]
    private async Task SynchronizeAsync()
    {
        var accounts = accountRepository.GetAll();

        if (accounts.Count == 0)
            return;

        IsSynchronizing = true;
        SyncProgress = 0;

        try
        {
            for (int i = 0; i < accounts.Count; i++)
            {
                Account account = accounts[i];
                if (!account.HasNpssoToken || account.IsTokenExpired)
                {
                    continue;
                }

                var accountPrefix = accounts.Count > 1 ? $"[{i + 1}/{accounts.Count}] " : string.Empty;

                var progress = new Progress<ProgressReport>(report =>
                {
                    SyncStatus = $"{accountPrefix}{account.OnlineId ?? account.AccountId}: {report.Status}";
                    SyncProgress = (double)i / accounts.Count * 100;
                });

                try
                {
                    await accountService.SynchronizeAccountEntitlementsAsync(account, null, progress);

                    account.LastSynchronization = DateTimeOffset.Now;
                    accountRepository.Update(account);
                }
                catch
                {
                    account.NpssoTokenExpiration = DateTimeOffset.Now;
                    accountRepository.Update(account);
                }
            }

            SyncStatus = "Synchronization completed.";
            SyncProgress = 100;
            await Task.Delay(1000);
        }
        finally
        {
            IsSynchronizing = false;
            SyncStatus = string.Empty;
            RefreshAccounts();
        }

        WeakReferenceMessenger.Default.Send(new AccountsSynchronizedMessage());
    }
}