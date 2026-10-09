using System;
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
        if (sender is not ComboBox { DataContext: ConsoleProfile profile } comboBox) return;
        Account? selectedAccount = comboBox.SelectedItem as Account;
        if (profile.AccountId != selectedAccount?.AccountId)
        {
            profile.AccountId = selectedAccount?.AccountId;
        }
    }

    private async void ImportEntitlements_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedConsole == null) return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        string? filePath = null;
        using ((topLevel as IBlurrable)?.PushBlur())
        {
            FilePickerOpenOptions options = new()
            {
                Title = "Select entitlements.db",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("SQLite Database")
                    {
                        Patterns = ["*.db"]
                    }
                ]
            };

            var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (result.Count > 0)
            {
                filePath = result[0].Path.LocalPath;
            }
        }

        if (filePath != null)
        {
            try
            {
                await _viewModel.UpdateConsole(_viewModel.SelectedConsole.Id!.Value, filePath);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Failed to import entitlements", ex);
            }
        }
    }

    private async void RegisterConsole_Click(object? sender, RoutedEventArgs e)
    {
        string? name = null;
        System.Collections.Generic.List<string>? profileIds = null;
        string? filePath = null;
        bool shouldRegister = false;

        RegisterConsoleWindow dialog = new();
        bool result = await _dialogs.ShowAsync<bool>(dialog, TopLevel.GetTopLevel(this) as Window);

        if (result && dialog is { ConsoleName: not null, ProfileIds: not null, EntitlementsFilePath: not null })
        {
            name = dialog.ConsoleName;
            profileIds = dialog.ProfileIds;
            filePath = dialog.EntitlementsFilePath;
            shouldRegister = true;
        }

        if (shouldRegister && name != null && profileIds != null && filePath != null)
        {
            try
            {
                await _viewModel.RegisterConsole(name, filePath);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Failed to register console", ex);
            }
        }
    }

    private async Task ShowErrorAsync(string title, Exception exception)
    {
        await _dialogs.ShowErrorAsync(title, exception.Message, owner: TopLevel.GetTopLevel(this) as Window);
    }

    private async void DeleteConsole_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ConsoleModel console }) return;
        var result = await _dialogs.ConfirmAsync("Remove console",
            $"Are you sure you want to remove console '{console.Name}'? This will also remove all related profiles and entitlements.",
            owner: TopLevel.GetTopLevel(this) as Window);

        if (result)
        {
            _viewModel.DeleteConsole(console);
        }
    }
}