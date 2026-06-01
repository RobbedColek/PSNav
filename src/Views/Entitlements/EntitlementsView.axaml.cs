using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using PSNav.Models.Entitlements;
using PSNav.ViewModels.Entitlements;

namespace PSNav.Views.Entitlements;

public partial class EntitlementsView : UserControl
{
    private readonly EntitlementsViewModel _viewModel;

    public EntitlementsView()
    {
        InitializeComponent();

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

    private async void ShowCopiedMessage()
    {
        CopiedMessage.Opacity = 1;
        await Task.Delay(2000);
        CopiedMessage.Opacity = 0;
    }

    private void ShowAllForSelectedEntitlement_Click(object? sender, RoutedEventArgs e)
    {
        Entitlement? selectedEntitlement = _viewModel.SelectedEntitlement;
        if (selectedEntitlement == null) return;
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
            if (topLevel?.Clipboard is null) return;
            await topLevel.Clipboard.SetTextAsync(textBlock.Text);
            ShowCopiedMessage();
        }
    }
}