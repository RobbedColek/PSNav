using Avalonia.Interactivity;
using ColekUI.Controls;

namespace PSNav.Views.Accounts;

public partial class AddOfflineAccountWindow : DialogWindow
{
    private const int MaxProfileIdLength = 16;

    public AddOfflineAccountWindow()
    {
        InitializeComponent();
    }

    public AddOfflineAccountWindow(string onlineId) : this()
    {
        Title = "Rename Offline Account";
        Header = "Rename offline account";
        Description = "Enter a new Profile ID for this offline account.";
        OnlineIdTextBox.Text = onlineId;
        AddButton.Content = "Rename";
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