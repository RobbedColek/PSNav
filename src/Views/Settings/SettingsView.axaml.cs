using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ColekUI.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using PSNav.ViewModels.Settings;

namespace PSNav.Views.Settings;

public partial class SettingsView : UserControl
{
    private readonly SettingsViewModel _viewModel;
    private readonly IDialogService _dialogs;

    public SettingsView()
    {
        InitializeComponent();
        _dialogs = App.GetServices().GetRequiredService<IDialogService>();
        _viewModel = App.GetServices().GetRequiredService<SettingsViewModel>();
        DataContext = _viewModel;
    }

    private async void UnlinkBackloggery_Click(object? sender, RoutedEventArgs e)
    {
        if (!_viewModel.Backloggery.CanUnlink)
            return;

        bool result = await _dialogs.ConfirmAsync("Unlink Backloggery account",
            $"Are you sure you want to unlink Backloggery account '{_viewModel.Backloggery.Username}'?\n\n" +
            "All locally stored Backloggery data will be removed, including your saved login, sync history, " +
            "ignored games and Backloggery preferences.\n\n" +
            "Nothing on Backloggery's servers will be modified. " +
            "Your PSN accounts, entitlements, and consoles will be kept.",
            yesText: "Unlink account", noText: "Cancel", owner: TopLevel.GetTopLevel(this) as Window);

        if (!result)
            return;

        try
        {
            _viewModel.Backloggery.Unlink();
        }
        catch (Exception exception)
        {
            await _dialogs.ShowErrorAsync("Could not unlink Backloggery account", exception.Message,
                owner: TopLevel.GetTopLevel(this) as Window);
        }
    }
}
