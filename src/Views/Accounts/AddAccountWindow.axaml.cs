using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using PSNav.Models.Accounts;
using PSNav.Services.Accounts;

namespace PSNav.Views.Accounts;

public partial class AddAccountWindow : Window
{
    private const string SsoCookieUrl = "https://ca.account.sony.com/api/v1/ssocookie";
    private readonly IAccountService _accountService;

    public AddAccountWindow()
    {
        InitializeComponent();
        _accountService = App.GetServices().GetRequiredService<IAccountService>();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private async void Add_Click(object? sender, RoutedEventArgs e)
    {
        string json = SsoCookieJsonTextBox.Text?.Trim() ?? string.Empty;

        SetIsAddingAccount(true);

        try
        {
            Account account = await _accountService.CreateAccountFromSsoCookieJsonAsync(json);

            Close(account);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
                                              or InvalidOperationException)
        {
            ShowError(exception.Message);
        }
        finally
        {
            SetIsAddingAccount(false);
        }
    }

    private void OpenSsoCookieUrl_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = SsoCookieUrl,
            UseShellExecute = true
        });
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.IsVisible = true;
    }

    private void SetIsAddingAccount(bool isAdding)
    {
        AddAccountProgressBar.IsVisible = isAdding;
        AddAccountButton.IsEnabled = !isAdding;
        SsoCookieJsonTextBox.IsEnabled = !isAdding;

        if (isAdding)
        {
            ErrorTextBlock.IsVisible = false;
        }
    }
}