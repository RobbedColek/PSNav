using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PSNav.Views.Common;

public partial class ErrorDialog : Window
{
    public ErrorDialog()
    {
        InitializeComponent();
    }

    public ErrorDialog(string title, string message) : this()
    {
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
