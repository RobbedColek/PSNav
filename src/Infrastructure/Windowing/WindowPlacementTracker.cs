using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace PSNav.Infrastructure.Windowing;

public sealed class WindowPlacementTracker
{
    private readonly Window _window;
    private readonly IWindowPlacementStore _store;
    private readonly string _windowKey;

    private PixelPoint _startPosition;
    private Size _startSize;

    private WindowPlacementTracker(Window window, IWindowPlacementStore store, string windowKey)
    {
        _window = window;
        _store = store;
        _windowKey = windowKey;
    }

    public static void Attach(Window window, IWindowPlacementStore store, string windowKey, double defaultSizeRatio = 0.9)
    {
        WindowPlacementTracker tracker = new(window, store, windowKey);
        tracker.ApplyInitialPlacement(defaultSizeRatio);
        tracker.StartTracking();
    }

    private static string DescribeScreens(IReadOnlyList<Screen> screens) =>
        string.Join(';', screens
            .OrderBy(s => s.Bounds.X)
            .ThenBy(s => s.Bounds.Y)
            .Select(s => string.Create(CultureInfo.InvariantCulture,
                $"{s.Bounds.X},{s.Bounds.Y},{s.Bounds.Width},{s.Bounds.Height}@{s.Scaling:R}{(s.IsPrimary ? "*" : "")}")));

    private void ApplyInitialPlacement(double defaultSizeRatio)
    {
        IReadOnlyList<Screen> screens = _window.Screens.All;
        Screen? primary = _window.Screens.Primary;
        if (screens.Count == 0 || primary is null) return;

        WindowPlacement? saved = null;
        try
        {
            saved = _store.Load(_windowKey);
        }
        catch (Exception)
        {

        }

        if (saved is not null && saved.ScreenLayout == DescribeScreens(screens))
        {
            ApplyBounds(new PixelPoint(saved.X, saved.Y), new Size(saved.Width, saved.Height));
            if (saved.IsMaximized) _window.WindowState = WindowState.Maximized;
            return;
        }

        PixelRect workingArea = primary.WorkingArea;

        double width = workingArea.Width / primary.Scaling * defaultSizeRatio;
        double height = workingArea.Height / primary.Scaling * defaultSizeRatio;

        ApplyBounds(
            new PixelPoint(
                workingArea.X + (int)((workingArea.Width - width * primary.Scaling) / 2),
                workingArea.Y + (int)((workingArea.Height - height * primary.Scaling) / 2)),
            new Size(width, height));
    }

    private void ApplyBounds(PixelPoint position, Size size)
    {
        _window.WindowStartupLocation = WindowStartupLocation.Manual;
        _window.Width = size.Width;
        _window.Height = size.Height;
        _window.Position = position;

        _startPosition = position;
        _startSize = size;
    }

    private void StartTracking()
    {
        _window.Closing += (_, e) =>
        {
            if (!e.Cancel) SavePlacement();
        };
    }

    private void SavePlacement()
    {
        bool isNormal = _window.WindowState == WindowState.Normal;
        PixelPoint position = isNormal ? _window.Position : _startPosition;
        Size size = isNormal ? _window.ClientSize : _startSize;

        try
        {
            _store.Save(_windowKey, new WindowPlacement(
                DescribeScreens(_window.Screens.All),
                position.X,
                position.Y,
                size.Width,
                size.Height,
                _window.WindowState == WindowState.Maximized));
        }
        catch (Exception)
        {

        }
    }
}
