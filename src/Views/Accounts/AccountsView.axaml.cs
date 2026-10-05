using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Extensions.DependencyInjection;
using PSNav.Models.Accounts;
using PSNav.ViewModels.Accounts;
using PSNav.Views.Shell;

namespace PSNav.Views.Accounts;

public partial class AccountsView : UserControl
{
    private readonly AccountsViewModel _viewModel;

    public AccountsView()
    {
        InitializeComponent();

        _viewModel = App.GetServices().GetRequiredService<AccountsViewModel>();
        DataContext = _viewModel;

        AttachedToVisualTree += (_, _) => _viewModel.LoadAccountsCommand.Execute(null);
    }

    private async void AddAccount_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not MainWindow owner)
            return;

        owner.IsBlurred = true;

        try
        {
            AddAccountWindow dialog = new();

            Account? account = await dialog.ShowDialog<Account?>(owner);

            if (account is null)
                return;

            _viewModel.SaveAccount(account);
        }
        finally
        {
            owner.IsBlurred = false;
        }
    }

    private async void AddOfflineAccount_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not MainWindow owner)
            return;

        owner.IsBlurred = true;

        try
        {
            AddOfflineAccountWindow dialog = new();

            string? onlineId = await dialog.ShowDialog<string?>(owner);

            if (string.IsNullOrWhiteSpace(onlineId))
                return;

            string accountId = _viewModel.GenerateOfflineAccountId();

            Account account = new()
            {
                AccountId = accountId,
                OnlineId = onlineId,
                AvatarPicture = LoadOfflineAccountAvatar(),
                CreatedAt = DateTimeOffset.Now,
                UpdatedAt = DateTimeOffset.Now
            };

            _viewModel.SaveAccount(account);
        }
        finally
        {
            owner.IsBlurred = false;
        }
    }

    private static byte[] LoadOfflineAccountAvatar()
    {
        using Stream stream = AssetLoader.Open(new Uri("avares://PSNav/Assets/OfflineAccountAvatar.webp"));
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private async void Synchronize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        List<string> expiredAccounts = _viewModel.Accounts
            .Where(account => account is { HasNpssoToken: true, IsTokenExpired: true })
            .Select(account => account.OnlineId ?? account.AccountId)
            .ToList();

        if (expiredAccounts.Count > 0)
        {
            if (TopLevel.GetTopLevel(this) is not MainWindow owner)
                return;

            owner.IsBlurred = true;

            try
            {
                Common.ConfirmDialog dialog = new("Expired NPSSO tokens",
                    "The following accounts have expired NPSSO tokens:\n" +
                    string.Join("\n", expiredAccounts.Select(name => $"  • {name}")) + "\n\n" +
                    "You can refresh a token using the button next to an account entry.\n" +
                    "Do you want to continue? Only accounts with valid NPSSO tokens will be synchronized.");
                bool result = await dialog.ShowDialog<bool>(owner);

                if (!result)
                    return;
            }
            finally
            {
                owner.IsBlurred = false;
            }
        }

        await _viewModel.SynchronizeCommand.ExecuteAsync(null);
    }

    private async void RefreshAccount_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: Account account })
            return;

        if (TopLevel.GetTopLevel(this) is not MainWindow owner)
            return;

        owner.IsBlurred = true;

        try
        {
            AddAccountWindow dialog = new();

            Account? newAccount = await dialog.ShowDialog<Account?>(owner);

            if (newAccount is null)
                return;

            account.NpssoToken = newAccount.NpssoToken;
            account.NpssoTokenExpiration = newAccount.NpssoTokenExpiration;
            account.AccessToken = null;
            account.UpdatedAt = DateTimeOffset.Now;

            _viewModel.UpdateAccount(account);
        }
        finally
        {
            owner.IsBlurred = false;
        }
    }


    private async void DeleteAccount_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: Account account })
            return;

        if (TopLevel.GetTopLevel(this) is not MainWindow owner)
            return;

        owner.IsBlurred = true;

        try
        {
            Common.ConfirmDialog dialog = new("Remove account",
                $"Are you sure you want to remove account '{account.OnlineId ?? account.AccountId}'? This will also remove all related entitlements.");
            bool result = await dialog.ShowDialog<bool>(owner);

            if (result)
                _viewModel.DeleteAccount(account);
        }
        finally
        {
            owner.IsBlurred = false;
        }
    }
}