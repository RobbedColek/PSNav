using System;
using ColekUI.Navigation;
using Microsoft.Extensions.DependencyInjection;
using PSNav.Resources;
using PSNav.ViewModels.Accounts;
using PSNav.ViewModels.Consoles;
using PSNav.ViewModels.Entitlements;
using PSNav.ViewModels.Patches;

namespace PSNav.ViewModels.Shell;

public class MainViewModel : ViewModelBase
{
    public INavigator Navigation { get; }

    public MainViewModel(IServiceProvider serviceProvider)
    {
        Navigation = new Navigator(t => serviceProvider.GetRequiredService(t));

        Navigation.Items.Add(new NavigationItem(
            "Entitlements",
            AppIcons.Get("Codicon.Database"),
            typeof(EntitlementsViewModel)));

        Navigation.Items.Add(new NavigationItem(
            "Accounts",
            AppIcons.Get("Bootstrap.Playstation"),
            typeof(AccountsViewModel)));

        Navigation.Items.Add(new NavigationItem(
            "Consoles",
            AppIcons.Get("Phosphor.GameController"),
            typeof(ConsolesViewModel)));

        Navigation.Items.Add(new NavigationItem(
            "Patches",
            AppIcons.Get("Phosphor.RocketLaunchLight"),
            typeof(PatchesViewModel)));

//        Navigation.FooterItems.Add(new NavigationItem(
//            "Settings",
//            AppIcons.Get("Codicon.SettingsGear"),
//            typeof(ViewModelBase)));

        Navigation.SelectedItem = Navigation.Items[0];
    }
}
