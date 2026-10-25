using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ColekUI.Controls;
using ColekUI.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using PSNav.Models.Accounts;
using PSNav.Models.Consoles;
using PSNav.ViewModels.Consoles;

namespace PSNav.Views.Consoles;

public partial class ConsolesView : UserControl
{
    private readonly ConsolesViewModel _viewModel;
    private readonly IDialogService _dialogs;

    public ConsolesView()
    {
        InitializeComponent();

        _dialogs = App.GetServices().GetRequiredService<IDialogService>();
        _viewModel = App.GetServices().GetRequiredService<ConsolesViewModel>();
        DataContext = _viewModel;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewModel.LoadConsolesCommand.Execute(null);
    }

    private void AccountComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { DataContext: ConsoleProfile profile, SelectedItem: Account selectedAccount })
            return;

        if (profile.AccountId != selectedAccount.AccountId)
            profile.AccountId = selectedAccount.AccountId;
    }

    private async void ImportEntitlements_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsUpdating || GetActionConsole(sender)?.Id is not int consoleId)
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        try
        {
            string? filePath = null;
            using ((topLevel as IBlurrable)?.PushBlur())
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select entitlement.db or entitlement4.db",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Entitlements database (entitlement.db or entitlement4.db)")
                        {
                            Patterns = ["*.db"]
                        }
                    ]
                };

                var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                if (result.Count > 0)
                    filePath = result[0].Path.LocalPath;
            }

            if (filePath != null && !_viewModel.IsUpdating && FindConsole(consoleId) != null)
                await _viewModel.UpdateConsole(consoleId, filePath);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to import entitlements from file", ex);
        }
    }

    private ConsoleModel? GetActionConsole(object? sender) => sender switch
    {
        Button { DataContext: ConsoleModel console } => console,
        MenuItem { CommandParameter: ConsoleModel console } => console,
        _ => null
    };

    private ConsoleModel? FindConsole(int consoleId) =>
        _viewModel.Consoles.FirstOrDefault(console => console.Id == consoleId);

    private async void RegisterConsoleViaFtp_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsUpdating)
            return;

        try
        {
            ConsoleSettingsWindow dialog = new();
            ConsoleSettings? settings = await _dialogs.ShowAsync<ConsoleSettings?>(dialog,
                TopLevel.GetTopLevel(this) as Window);

            if (settings != null && !_viewModel.IsUpdating)
                await _viewModel.RegisterConsoleViaFtp(settings);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to register console via FTP", ex);
        }
    }

    private async void EditConsole_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsUpdating || GetActionConsole(sender) is not { Id: int consoleId } console)
            return;

        try
        {
            ConsoleSettingsWindow dialog = new("Edit console",
                "Update the console's name and optional FTP connection settings. This does not connect to the console.",
                "Save", new ConsoleSettings(console.Name, console.IpAddress, console.FtpPort, console.Platform), false);
            ConsoleSettings? settings = await _dialogs.ShowAsync<ConsoleSettings?>(dialog,
                TopLevel.GetTopLevel(this) as Window);

            if (settings != null && !_viewModel.IsUpdating && FindConsole(consoleId) is { } currentConsole)
                _viewModel.SaveConsoleSettings(currentConsole, settings);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to save console settings", ex);
        }
    }

    private async void ImportEntitlementsViaFtp_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsUpdating || GetActionConsole(sender) is not { Id: int consoleId } console)
            return;

        try
        {
            if (string.IsNullOrWhiteSpace(console.IpAddress))
            {
                ConsoleSettingsWindow dialog = new("Set up console FTP",
                    "Enter the console's IP address and FTP port.",
                    "Save and synchronize", new ConsoleSettings(console.Name, console.IpAddress, console.FtpPort, console.Platform), true);
                ConsoleSettings? settings = await _dialogs.ShowAsync<ConsoleSettings?>(dialog,
                    TopLevel.GetTopLevel(this) as Window);

                if (settings == null || _viewModel.IsUpdating || FindConsole(consoleId) is not { } currentConsole)
                    return;

                try
                {
                    _viewModel.SaveConsoleSettings(currentConsole, settings);
                }
                catch (Exception ex)
                {
                    await ShowErrorAsync("Failed to save console FTP settings", ex);
                    return;
                }
            }

            if (!_viewModel.IsUpdating && FindConsole(consoleId) is { } savedConsole)
                await _viewModel.UpdateConsoleViaFtp(savedConsole);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to synchronize console via FTP", ex);
        }
    }

    private async void RegisterConsole_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsUpdating)
            return;

        try
        {
            RegisterConsoleWindow dialog = new();
            bool result = await _dialogs.ShowAsync<bool>(dialog, TopLevel.GetTopLevel(this) as Window);

            if (result && !_viewModel.IsUpdating &&
                dialog is { ConsoleName: not null, ProfileIds: not null, EntitlementsFilePath: not null })
            {
                await _viewModel.RegisterConsole(dialog.ConsoleName, dialog.EntitlementsFilePath);
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to register console from file", ex);
        }
    }

    private async Task ShowErrorAsync(string title, Exception exception)
    {
        await _dialogs.ShowErrorAsync(title, exception.Message, owner: TopLevel.GetTopLevel(this) as Window);
    }

    private async void DeleteConsole_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsUpdating || GetActionConsole(sender) is not { Id: int consoleId } console)
            return;

        try
        {
            var result = await _dialogs.ConfirmAsync("Remove console",
                $"Are you sure you want to remove console '{console.Name}'? This will also remove all related profiles and entitlements.",
                owner: TopLevel.GetTopLevel(this) as Window);

            if (result && !_viewModel.IsUpdating && FindConsole(consoleId) is { } currentConsole)
                _viewModel.DeleteConsole(currentConsole);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to remove console", ex);
        }
    }
}