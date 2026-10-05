using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PSNav.Views.Accounts;

public partial class AddOfflineAccountWindow : Window
{
    private const int MaxProfileIdLength = 16;

    public AddOfflineAccountWindow()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        string onlineId = OnlineIdTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(onlineId))
        {
            ShowError("Profile ID cannot be empty.");
            return;
        }

        if (onlineId.Length > MaxProfileIdLength)
        {
            ShowError($"Profile ID cannot be longer than {MaxProfileIdLength} characters.");
            return;
        }

        Close(onlineId);
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.IsVisible = true;
    }
}