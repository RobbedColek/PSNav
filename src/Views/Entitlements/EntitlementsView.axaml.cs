using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ColekUI.Controls;
using ColekUI.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using PSNav.Models.Entitlements;
using PSNav.ViewModels.Entitlements;

namespace PSNav.Views.Entitlements;

public partial class EntitlementsView : UserControl
{
    private readonly EntitlementsViewModel _viewModel;
    private readonly IDialogService _dialogs;

    public EntitlementsView()
    {
        InitializeComponent();

        _dialogs = App.GetServices().GetRequiredService<IDialogService>();
        _viewModel = App.GetServices().GetRequiredService<EntitlementsViewModel>();
        DataContext = _viewModel;

        AttachedToVisualTree += (_, _) => _viewModel.LoadAllEntitlementsCommand.Execute(null);
    }

    private void EmptySearchField_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.SearchText = string.Empty;
    }

    private async void CopyToClipboard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not TextBlock textBlock || string.IsNullOrEmpty(textBlock.Text)) return;
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null) return;
        await topLevel.Clipboard.SetTextAsync(textBlock.Text);
        ShowCopiedMessage();
    }

    private void EntitlementImage_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Entitlement? entitlement = _viewModel.SelectedEntitlement;
        if (entitlement?.Image == null) return;

        _viewModel.FullImageBytes = entitlement.Image;
        _viewModel.IsImageOverlayVisible = true;
    }

    private void ImageOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _viewModel.IsImageOverlayVisible = false;
        }
    }

    private async void FullImage_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;

        if (properties.IsLeftButtonPressed)
        {
            _viewModel.IsImageOverlayVisible = false;
            e.Handled = true;
        }
        else if (properties.IsRightButtonPressed)
        {
            e.Handled = true;

            if (_viewModel.FullImageBytes == null) return;

            IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null) return;

            using MemoryStream ms = new(_viewModel.FullImageBytes);
            Bitmap bitmap = new(ms);

            await clipboard.SetBitmapAsync(bitmap);
            ShowCopiedMessage();
        }
    }

    private void ShowCopiedMessage()
    {
        ShowToast("Copied to clipboard");
    }

    private async void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastMessage.Opacity = 1;
        await Task.Delay(2000);
        ToastMessage.Opacity = 0;
    }

    private async void ExportToCsv_Click(object? sender, RoutedEventArgs e)
    {
        var rows = EntitlementsGrid.CollectionView?.Cast<Entitlement>().ToList() ?? [];

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        IStorageFile? file;
        using ((topLevel as IBlurrable)?.PushBlur())
        {
            FilePickerSaveOptions options = new()
            {
                Title = "Export entitlements",
                SuggestedFileName = $"PSNav-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
                DefaultExtension = "csv",
                ShowOverwritePrompt = true,
                FileTypeChoices =
                [
                    new FilePickerFileType("CSV file")
                    {
                        Patterns = ["*.csv"]
                    }
                ]
            };

            file = await topLevel.StorageProvider.SaveFilePickerAsync(options);
        }

        if (file == null)
            return;

        try
        {
            await using Stream stream = await file.OpenWriteAsync();

            if (stream.CanSeek)
                stream.SetLength(0);

            await _viewModel.ExportToCsvAsync(stream, rows);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to export entitlements", ex);
            return;
        }
        finally
        {
            file.Dispose();
        }

        ShowToast(rows.Count == 1 ? "Exported 1 entitlement" : $"Exported {rows.Count} entitlements");
    }

    private async Task ShowErrorAsync(string title, Exception exception)
    {
        await _dialogs.ShowErrorAsync(title, exception.Message, owner: TopLevel.GetTopLevel(this) as Window);
    }

    private void ShowAllForSelectedEntitlement_Click(object? sender, RoutedEventArgs e)
    {
        Entitlement? selectedEntitlement = _viewModel.SelectedEntitlement;

        if (selectedEntitlement == null)
            return;

        _viewModel.SearchText = selectedEntitlement.TitleId!;
        _viewModel.SelectedEntitlement = selectedEntitlement;
    }

    private async void Url_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not TextBlock textBlock || string.IsNullOrEmpty(textBlock.Text) ||
            textBlock.Text == "Missing") return;

        PointerPointProperties pointerProperties = e.GetCurrentPoint(textBlock).Properties;

        if (pointerProperties.IsLeftButtonPressed)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = textBlock.Text,
                UseShellExecute = true
            });
        }
        else if (pointerProperties.IsRightButtonPressed)
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(this);

            if (topLevel?.Clipboard is null)
                return;

            await topLevel.Clipboard.SetTextAsync(textBlock.Text);
            ShowCopiedMessage();
        }
    }
}