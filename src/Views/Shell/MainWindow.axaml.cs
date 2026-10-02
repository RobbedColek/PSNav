using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using PSNav.Infrastructure.Windowing;
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

    public MainWindow()
    {
        InitializeComponent();

        IServiceProvider services = App.GetServices();
        DataContext = services.GetRequiredService<MainViewModel>();

        WindowPlacementTracker.Attach(this, services.GetRequiredService<IWindowPlacementStore>(), "MainWindow");

        AppShell? shell = this.FindControl<AppShell>("Shell");
        BlurEffect? blurEffect = shell?.Effect as BlurEffect;

        this.GetObservable(IsBlurredProperty)
            .Subscribe(isBlurred => { blurEffect?.Radius = isBlurred ? 10 : 0; });
    }
}