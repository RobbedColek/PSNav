using System;
using ColekUI.Controls;
using ColekUI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using PSNav.ViewModels.Shell;

namespace PSNav.Views.Shell;

public partial class MainWindow : ColekWindow
{
    public MainWindow()
    {
        InitializeComponent();

        IServiceProvider services = App.GetServices();
        DataContext = services.GetRequiredService<MainViewModel>();
        PlacementStore = services.GetRequiredService<IWindowPlacementStore>();
        PlacementKey = "MainWindow";
    }
}
