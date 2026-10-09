using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ColekUI.Controls;
using Microsoft.Data.Sqlite;

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

        var result = await StorageProvider.OpenFilePickerAsync(options);
        if (result.Count > 0)
        {
            FilePathTextBox.Text = result[0].Path.LocalPath;
        }
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
            ShowError("Please select a valid entitlements.db file.");
            return;
        }

        try
        {
            var profileIds = ExtractProfileIds(filePath);
            if (profileIds.Count == 0)
            {
                ShowError("No profiles found in the entitlements database.");
                return;
            }

            ConsoleName = name;
            EntitlementsFilePath = filePath;
            ProfileIds = profileIds;
            Close(true);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to parse entitlements.db: {ex.Message}");
        }
    }

    private List<string> ExtractProfileIds(string filePath)
    {
        var ids = new List<string>();
        using SqliteConnection connection = new($"Data Source={filePath}");
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'entitlement_%'";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            var tableName = reader.GetString(0);
            var id = tableName.Replace("entitlement_", "");
            if (!string.IsNullOrEmpty(id) && int.TryParse(id, out _))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.IsVisible = true;
    }
}