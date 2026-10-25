using System;
using System.Globalization;
using Avalonia.Interactivity;
using ColekUI.Controls;
using PSNav.Models.Consoles;
using PSNav.Services.Consoles;

namespace PSNav.Views.Consoles;

public partial class ConsoleSettingsWindow : DialogWindow
{
    private static readonly string?[] Platforms = [null, ConsolePlatform.Ps4, ConsolePlatform.Ps5];

    private readonly bool _requireIpAddress;

    public ConsoleSettingsWindow() : this("Register Console",
        "Enter a name and the console's FTP connection settings.", "Register", new ConsoleSettings(string.Empty, null, 2121),
        true)
    {
    }

    public ConsoleSettingsWindow(string title, string description, string submitText, ConsoleSettings initialSettings,
        bool requireIpAddress)
    {
        InitializeComponent();
        Title = title;
        Header = title;
        Description = description;
        SubmitButton.Content = submitText;
        ConsoleNameTextBox.Text = initialSettings.Name;
        IpAddressTextBox.Text = initialSettings.IpAddress;
        FtpPortTextBox.Text = initialSettings.FtpPort.ToString(CultureInfo.InvariantCulture);
        PlatformComboBox.SelectedIndex = Math.Max(0, Array.IndexOf(Platforms, initialSettings.Platform));
        _requireIpAddress = requireIpAddress;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void Submit_Click(object? sender, RoutedEventArgs e)
    {
        ErrorTextBlock.IsVisible = false;
        string? platform = Platforms[Math.Max(0, PlatformComboBox.SelectedIndex)];
        if (!ConsoleSettingsValidator.TryValidate(ConsoleNameTextBox.Text, IpAddressTextBox.Text, FtpPortTextBox.Text,
                platform, _requireIpAddress, out ConsoleSettings? settings, out string error))
        {
            ErrorTextBlock.Text = error;
            ErrorTextBlock.IsVisible = true;
            return;
        }

        Close(settings);
    }
}
