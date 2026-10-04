using System;
using System.IO;
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

    private async void RefreshAccount_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not MainWindow owner)
            return;

        Account? selectedAccount = _viewModel.SelectedAccount;
        if (selectedAccount is null)
            return;

        owner.IsBlurred = true;

        try
        {
            AddAccountWindow dialog = new();

            Account? account = await dialog.ShowDialog<Account?>(owner);

            if (account is null)
                return;

            selectedAccount.NpssoToken = account.NpssoToken;
            selectedAccount.NpssoTokenExpiration = account.NpssoTokenExpiration;
            selectedAccount.AccessToken = null;
            selectedAccount.UpdatedAt = DateTimeOffset.Now;

            _viewModel.UpdateAccount(selectedAccount);
        }
        finally
        {
            owner.IsBlurred = false;
        }
    }


    private async void DeleteAccount_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: Account account }) return;
        if (TopLevel.GetTopLevel(this) is not MainWindow owner)
            return;

        owner.IsBlurred = true;

        try
        {
            Common.ConfirmDialog dialog = new("Remove account",
                $"Are you sure you want to remove account '{account.OnlineId ?? account.AccountId}'? This will also remove all related entitlements.");
            bool result = await dialog.ShowDialog<bool>(owner);

            if (result)
            {
                _viewModel.DeleteAccount(account);
            }
        }
        finally
        {
            owner.IsBlurred = false;
        }
    }
}