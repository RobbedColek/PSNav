using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Microsoft.Extensions.DependencyInjection;
using PSNav.ViewModels.Shell;

namespace PSNav.Views.Shell;

public partial class MainWindow : Window
{
    private static readonly DirectProperty<MainWindow, bool> IsBlurredProperty =
        AvaloniaProperty.RegisterDirect<MainWindow, bool>(
            nameof(IsBlurred),
            o => o.IsBlurred,
            (o, v) => o.IsBlurred = v);

    public bool IsBlurred
    {
        get;
        set => SetAndRaise(IsBlurredProperty, ref field, value);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Screen? screen = Screens.Primary;
        if (screen is null) return;

        PixelRect workingArea = screen.WorkingArea;

        Width  = workingArea.Width / screen.Scaling * 0.9;
        Height = workingArea.Height / screen.Scaling * 0.9;

        Position = new PixelPoint(
            workingArea.X + (int)((workingArea.Width  - Width  * screen.Scaling) / 2),
            workingArea.Y + (int)((workingArea.Height - Height * screen.Scaling) / 2)
        );
    }

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;

        DataContext = App.GetServices().GetRequiredService<MainViewModel>();

        AppShell? shell = this.FindControl<AppShell>("Shell");
        BlurEffect? blurEffect = shell?.Effect as BlurEffect;

        this.GetObservable(IsBlurredProperty)
            .Subscribe(isBlurred => { blurEffect?.Radius = isBlurred ? 10 : 0; });
    }
}