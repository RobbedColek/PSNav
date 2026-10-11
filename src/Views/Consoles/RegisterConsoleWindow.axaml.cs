using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ColekUI.Controls;
using PSNav.Services.Consoles;

namespace PSNav.Views.Consoles;

public partial class RegisterConsoleWindow : DialogWindow
{
    public string? ConsoleName { get; private set; }
    public string? EntitlementsFilePath { get; private set; }
    public List<string>? ProfileIds { get; private set; }

    public RegisterConsoleWindow()
    {
        InitializeComponent();
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        FilePickerOpenOptions options = new()
        {
            Title = "Select entitlement.db / entitlement4.db",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Entitlements Database (entitlement.db / entitlement4.db)")
                {
                    Patterns = ["*.db"]
                }
            ]
        };

        var result = await StorageProvider.OpenFilePickerAsync(options);
        if (result.Count > 0)
            FilePathTextBox.Text = result[0].Path.LocalPath;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Register_Click(object? sender, RoutedEventArgs e)
    {
        ErrorTextBlock.IsVisible = false;

        var name = ConsoleNameTextBox.Text?.Trim();
        var filePath = FilePathTextBox.Text;

        if (string.IsNullOrEmpty(name))
        {
            ShowError("Please enter a console name.");
            return;
        }

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            ShowError("Please select a valid entitlement.db / entitlement4.db file.");
            return;
        }

        try
        {
            var profileIds = EntitlementDatabaseValidator.Validate(filePath);

            ConsoleName = name;
            EntitlementsFilePath = filePath;
            ProfileIds = new List<string>(profileIds);
            Close(true);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to validate entitlement.db / entitlement4.db: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.IsVisible = true;
    }
}