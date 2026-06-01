using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PSNav.Views.Accounts;

public partial class AddOfflineAccountWindow : Window
{
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
            return;
        }

        Close(onlineId);
    }
}