using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PSNav.Views.Common;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(string title, string message) : this()
    {
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
    }

    private void No_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void Yes_Click(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}