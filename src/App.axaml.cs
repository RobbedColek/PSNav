using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ColekUI.Dialogs;
using ColekUI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using PSNav.Infrastructure.Backloggery;
using PSNav.Infrastructure.Database;
using PSNav.Infrastructure.PlayStationApi;
using PSNav.Repositories.Accounts;
using PSNav.Repositories.Backloggery;
using PSNav.Repositories.Consoles;
using PSNav.Repositories.Entitlements;
using PSNav.Repositories.Patches;
using PSNav.Repositories.Settings;
using PSNav.Services.Accounts;
using PSNav.Services.Backloggery;
using PSNav.Services.Patches;
using PSNav.ViewModels.Accounts;
using PSNav.ViewModels.Backloggery;
using PSNav.ViewModels.Consoles;
using PSNav.ViewModels.Entitlements;
using PSNav.ViewModels.Patches;
using PSNav.ViewModels.Settings;
using PSNav.ViewModels.Shell;
using PSNav.Views.Shell;

namespace PSNav;

public class App : Application
{
    private IServiceProvider? Services { get; set; }

    public static IServiceProvider GetServices() => ((App)Current!).Services!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ServiceCollection services = new();

        // Register Services
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IPlayStationApiClient, PlayStationApiClient>();
        services.AddSingleton<IBackloggeryClient, BackloggeryClient>();
        services.AddSingleton<IAccountsRepository, AccountsRepository>();
        services.AddSingleton<IEntitlementsRepository, EntitlementsRepository>();
        services.AddSingleton<IConsolesRepository, ConsolesRepository>();
        services.AddSingleton<IPatchesRepository, PatchesRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IBackloggerySyncRepository, BackloggerySyncRepository>();
        services.AddSingleton<IWindowPlacementStore, WindowPlacementStore>();
        services.AddSingleton<IAccountService, AccountService>();
        services.AddSingleton<IPatchService, PatchService>();
        services.AddSingleton<IBackloggerySyncService, BackloggerySyncService>();
        services.AddSingleton<IDialogService, DialogService>();

        // Register ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<AccountsViewModel>();
        services.AddSingleton<EntitlementsViewModel>();
        services.AddSingleton<ConsolesViewModel>();
        services.AddSingleton<BackloggeryViewModel>();
        services.AddSingleton<PatchesViewModel>();
        services.AddSingleton<SettingsViewModel>();

        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
