using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using IconPacks.Avalonia.BootstrapIcons;
using IconPacks.Avalonia.Codicons;
using IconPacks.Avalonia.PhosphorIcons;
using PSNav.Models.Shell;
using PSNav.ViewModels.Accounts;
using PSNav.ViewModels.Consoles;
using PSNav.ViewModels.Entitlements;
using PSNav.ViewModels.Patches;

namespace PSNav.ViewModels.Shell;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] public partial ViewModelBase? CurrentPage { get; set; }
    [ObservableProperty] public partial NavigationItem? SelectedMenuItem { get; set; }
    public ObservableCollection<NavigationItem> MenuItems { get; } = [];
    public ObservableCollection<NavigationItem> BottomMenuItems { get; } = [];

    private readonly IServiceProvider _serviceProvider;

    public MainViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        MenuItems.Add(new NavigationItem(
            "Entitlements",
            PackIconCodiconsKind.Database,
            typeof(PackIconCodicons),
            typeof(EntitlementsViewModel)));

        MenuItems.Add(new NavigationItem(
            "Accounts",
            PackIconBootstrapIconsKind.Playstation,
            typeof(PackIconBootstrapIcons),
            typeof(AccountsViewModel)));

        MenuItems.Add(new NavigationItem(
            "Consoles",
            PackIconPhosphorIconsKind.GameController,
            typeof(PackIconPhosphorIcons),
            typeof(ConsolesViewModel)));

        MenuItems.Add(new NavigationItem(
            "Patches",
            PackIconPhosphorIconsKind.RocketLaunchLight,
            typeof(PackIconPhosphorIcons),
            typeof(PatchesViewModel)));

//        BottomMenuItems.Add(new NavigationItem(
//            "Settings", 
//            PackIconCodiconsKind.SettingsGear, 
//            typeof(PackIconCodicons), 
//            typeof(ViewModelBase)));

        SelectedMenuItem = MenuItems[0];
    }

    partial void OnSelectedMenuItemChanged(NavigationItem? value)
    {
        if (value != null)
        {
            CurrentPage = (ViewModelBase)_serviceProvider.GetService(value.ViewModelType)!;
        }
    }
}