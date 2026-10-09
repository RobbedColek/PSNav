using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ColekUI.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using PSNav.Models.Backloggery;
using PSNav.ViewModels.Backloggery;

namespace PSNav.Views.Backloggery;

public partial class BackloggeryView : UserControl
{
    public static readonly DirectProperty<BackloggeryView, Thickness> EditButtonMarginProperty =
        AvaloniaProperty.RegisterDirect<BackloggeryView, Thickness>(nameof(EditButtonMargin), view => view.EditButtonMargin);

    private Thickness _editButtonMargin;
    private ScrollBar? _verticalScrollBar;
    private readonly BackloggeryViewModel _viewModel;
    private readonly IDialogService _dialogs;

    public BackloggeryView()
    {
        InitializeComponent();

        _dialogs = App.GetServices().GetRequiredService<IDialogService>();
        _viewModel = App.GetServices().GetRequiredService<BackloggeryViewModel>();
        DataContext = _viewModel;

        _viewModel.ErrorOccurred += ShowError;
        AttachedToVisualTree += (_, _) => _viewModel.InitializeCommand.Execute(null);
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || _viewModel.Editor is null)
                return;

            _viewModel.CloseEditorCommand.Execute(null);
            e.Handled = true;
        };

        ItemsGrid.AddHandler(PointerPressedEvent, ItemsGrid_PointerPressed, RoutingStrategies.Tunnel);
        ItemsGrid.TemplateApplied += ItemsGrid_TemplateApplied;
    }

    public Thickness EditButtonMargin => _editButtonMargin;

    private void ItemsGrid_TemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (_verticalScrollBar is not null)
            _verticalScrollBar.PropertyChanged -= VerticalScrollBar_PropertyChanged;

        _verticalScrollBar = e.NameScope.Find<ScrollBar>("PART_VerticalScrollbar");

        if (_verticalScrollBar is not null)
            _verticalScrollBar.PropertyChanged += VerticalScrollBar_PropertyChanged;

        UpdateEditButtonClearance();
    }

    private void VerticalScrollBar_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty || e.Property == BoundsProperty || e.Property == MarginProperty)
            UpdateEditButtonClearance();
    }

    private void UpdateEditButtonClearance()
    {
        double clearance = _verticalScrollBar is { IsVisible: true } scrollBar
            ? Math.Max(scrollBar.Bounds.Width, scrollBar.DesiredSize.Width) + scrollBar.Margin.Left + scrollBar.Margin.Right
            : 0;

        Thickness margin = new(0, 0, clearance, 0);

        if (margin == _editButtonMargin)
            return;

        SetAndRaise(EditButtonMarginProperty, ref _editButtonMargin, margin);
    }

    private IReadOnlyList<BackloggerySyncItem> SelectedItems =>
        ItemsGrid.SelectedItems.OfType<BackloggerySyncItem>().ToList();

    private void ItemsGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(ItemsGrid).Properties.IsRightButtonPressed)
            return;

        if ((e.Source as Control)?.FindAncestorOfType<DataGridRow>(includeSelf: true) is not
            { DataContext: BackloggerySyncItem item })
            return;

        if (!ItemsGrid.SelectedItems.Contains(item))
            ItemsGrid.SelectedItem = item;
    }

    private void ItemsContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        var items = SelectedItems;

        IgnoreMenuItem.IsVisible = items.Any(item => !item.IsSynced && !item.IsIgnored);
        UnignoreMenuItem.IsVisible = items.Any(item => item.IsIgnored);

        e.Cancel = _viewModel.IsBusy || (!IgnoreMenuItem.IsVisible && !UnignoreMenuItem.IsVisible);
    }

    private void Ignore_Click(object? sender, RoutedEventArgs e) => _viewModel.SetIgnored(SelectedItems, true);

    private void Unignore_Click(object? sender, RoutedEventArgs e) => _viewModel.SetIgnored(SelectedItems, false);

    private void Edit_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: BackloggerySyncItem item })
            _viewModel.EditCommand.Execute(item);
    }

    private async void ShowError(string title, Exception exception)
    {
        await _dialogs.ShowErrorAsync(title, exception.Message, owner: TopLevel.GetTopLevel(this) as Window);
    }
}
