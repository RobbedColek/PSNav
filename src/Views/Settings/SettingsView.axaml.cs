using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PSNav.ViewModels.Settings;

namespace PSNav.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        DataContext = App.GetServices().GetRequiredService<SettingsViewModel>();
    }
}
