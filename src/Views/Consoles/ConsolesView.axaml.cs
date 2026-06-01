using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using PSNav.Models.Accounts;
using PSNav.Models.Consoles;
using PSNav.ViewModels.Consoles;
using PSNav.Views.Common;
using PSNav.Views.Shell;

namespace PSNav.Views.Consoles;

public partial class ConsolesView : UserControl
{
    private readonly ConsolesViewModel _viewModel;

    public ConsolesView()
    {
        InitializeComponent();

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

        MainWindow? mainWindow = VisualRoot as MainWindow;
        mainWindow?.IsBlurred = true;

        string? filePath = null;
        try
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

            var result = await (VisualRoot as Window)!.StorageProvider.OpenFilePickerAsync(options);
            if (result.Count > 0)
            {
                filePath = result[0].Path.LocalPath;
            }
        }
        finally
        {
            mainWindow?.IsBlurred = false;
        }

        if (filePath != null)
        {
            await _viewModel.UpdateConsole(_viewModel.SelectedConsole.Id!.Value, filePath);
        }
    }

    private async void RegisterConsole_Click(object? sender, RoutedEventArgs e)
    {
        MainWindow? mainWindow = VisualRoot as MainWindow;
        mainWindow?.IsBlurred = true;

        string? name = null;
        System.Collections.Generic.List<string>? profileIds = null;
        string? filePath = null;
        bool shouldRegister = false;

        try
        {
            RegisterConsoleWindow dialog = new();
            bool result = await dialog.ShowDialog<bool>(mainWindow ?? (Window)VisualRoot!);

            if (result && dialog is { ConsoleName: not null, ProfileIds: not null, EntitlementsFilePath: not null })
            {
                name = dialog.ConsoleName;
                profileIds = dialog.ProfileIds;
                filePath = dialog.EntitlementsFilePath;
                shouldRegister = true;
            }
        }
        finally
        {
            mainWindow?.IsBlurred = false;
        }

        if (shouldRegister && name != null && profileIds != null && filePath != null)
        {
            await _viewModel.RegisterConsole(name, filePath);
        }
    }

    private async void DeleteConsole_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ConsoleModel console }) return;
        MainWindow? mainWindow = VisualRoot as MainWindow;
        mainWindow?.IsBlurred = true;

        try
        {
            ConfirmDialog dialog = new("Remove console",
                $"Are you sure you want to remove console '{console.Name}'? This will also remove all related profiles and entitlements.");
            var result = await dialog.ShowDialog<bool>(mainWindow ?? (Window)VisualRoot!);

            if (result)
            {
                _viewModel.DeleteConsole(console);
            }
        }
        finally
        {
            mainWindow?.IsBlurred = false;
        }
    }
}