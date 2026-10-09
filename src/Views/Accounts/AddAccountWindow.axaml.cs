using System;
using System.Net.Http;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ColekUI.Controls;
using Microsoft.Extensions.DependencyInjection;
using PSNav.Models.Accounts;
using PSNav.Services.Accounts;

namespace PSNav.Views.Accounts;

public partial class AddAccountWindow : DialogWindow
{
    private readonly IAccountService _accountService;
    private bool _isAdding;
    private bool _isClosingWithResult;

    public AddAccountWindow()
    {
        InitializeComponent();
        _accountService = App.GetServices().GetRequiredService<IAccountService>();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_isAdding && !_isClosingWithResult)
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
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

            _isClosingWithResult = true;
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

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.IsVisible = true;
    }

    private void SetIsAddingAccount(bool isAdding)
    {
        _isAdding = isAdding;
        AddAccountProgressBar.IsVisible = isAdding;
        CancelButton.IsEnabled = !isAdding;
        AddAccountButton.IsEnabled = !isAdding;
        SsoCookieJsonTextBox.IsEnabled = !isAdding;

        if (isAdding)
        {
            ErrorTextBlock.IsVisible = false;
        }
    }
}