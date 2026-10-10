using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.VisualTree;
using ColekUI.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using PSNav.Models.Accounts;
using PSNav.ViewModels.Accounts;

namespace PSNav.Views.Accounts;

public partial class AccountsView : UserControl
{
    private readonly AccountsViewModel _viewModel;
    private readonly IDialogService _dialogs;

    public AccountsView()
    {
        InitializeComponent();

        _dialogs = App.GetServices().GetRequiredService<IDialogService>();
        _viewModel = App.GetServices().GetRequiredService<AccountsViewModel>();
        DataContext = _viewModel;

        AttachedToVisualTree += (_, _) => _viewModel.LoadAccountsCommand.Execute(null);
        AccountsGrid.AddHandler(PointerPressedEvent, AccountsGrid_PointerPressed, RoutingStrategies.Tunnel);
    }

    private void AccountsGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(AccountsGrid).Properties.IsRightButtonPressed)
            return;

        AccountsGrid.SelectedItem =
            ((e.Source as Control)?.FindAncestorOfType<DataGridRow>(includeSelf: true))?.DataContext as Account;
    }

    private void AccountsContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        Account? account = _viewModel.SelectedAccount;
        RenameMenuItem.IsVisible = account is { HasNpssoToken: false };
        RefreshMenuItem.IsVisible = account is { HasNpssoToken: true };
        e.Cancel = account is null;
    }

    private Account? GetActionAccount(object? sender) => sender switch
    {
        Button { DataContext: Account account } => account,
        MenuItem => _viewModel.SelectedAccount,
        _ => null
    };

    private async void RenameAccount_Click(object? sender, RoutedEventArgs e)
    {
        if (GetActionAccount(sender) is not { HasNpssoToken: false } account)
            return;

        AddOfflineAccountWindow dialog = new(account.OnlineId ?? string.Empty);
        string? onlineId = await _dialogs.ShowAsync<string?>(dialog, TopLevel.GetTopLevel(this) as Window);

        if (string.IsNullOrWhiteSpace(onlineId) || onlineId == account.OnlineId)
            return;

        account.OnlineId = onlineId;
        account.UpdatedAt = DateTimeOffset.Now;
        _viewModel.UpdateAccount(account);
    }

    private async void AddAccount_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddAccountWindow dialog = new();

        Account? account = await _dialogs.ShowAsync<Account?>(dialog, TopLevel.GetTopLevel(this) as Window);

        if (account is null)
            return;

        _viewModel.SaveAccount(account);
    }

    private async void AddOfflineAccount_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddOfflineAccountWindow dialog = new();

        string? onlineId = await _dialogs.ShowAsync<string?>(dialog, TopLevel.GetTopLevel(this) as Window);

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
            bool result = await _dialogs.ConfirmAsync("Expired NPSSO tokens",
                "The following accounts have expired NPSSO tokens:\n" +
                string.Join("\n", expiredAccounts.Select(name => $"  • {name}")) + "\n\n" +
                "You can refresh a token using the button next to an account entry.\n" +
                "Do you want to continue? Only accounts with valid NPSSO tokens will be synchronized.",
                owner: TopLevel.GetTopLevel(this) as Window);

            if (!result)
                return;
        }

        await _viewModel.SynchronizeCommand.ExecuteAsync(null);
    }

    private async void RefreshAccount_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (GetActionAccount(sender) is not { HasNpssoToken: true } account)
            return;

        AddAccountWindow dialog = new();

        Account? newAccount = await _dialogs.ShowAsync<Account?>(dialog, TopLevel.GetTopLevel(this) as Window);

        if (newAccount is null)
            return;

        account.NpssoToken = newAccount.NpssoToken;
        account.NpssoTokenExpiration = newAccount.NpssoTokenExpiration;
        account.AccessToken = null;
        account.UpdatedAt = DateTimeOffset.Now;

        _viewModel.UpdateAccount(account);
    }


    private async void DeleteAccount_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (GetActionAccount(sender) is not { } account)
            return;

        bool result = await _dialogs.ConfirmAsync("Remove account",
            $"Are you sure you want to remove account '{account.OnlineId ?? account.AccountId}'? This will also remove all related entitlements.",
            owner: TopLevel.GetTopLevel(this) as Window);

        if (result)
            _viewModel.DeleteAccount(account);
    }
}