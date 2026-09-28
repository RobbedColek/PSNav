using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PSNav.Models.Accounts;
using PSNav.Models.Common;
using PSNav.Models.Consoles;
using PSNav.Models.Messages;
using PSNav.Repositories.Accounts;
using PSNav.Repositories.Consoles;

namespace PSNav.ViewModels.Consoles;

public sealed partial class ConsolesViewModel : ViewModelBase
{
    private readonly IAccountsRepository _accountRepository;
    private readonly IConsolesRepository _consolesRepository;

    public ConsolesViewModel(IAccountsRepository accountRepository, IConsolesRepository consolesRepository)
    {
        _accountRepository = accountRepository;
        _consolesRepository = consolesRepository;
        WeakReferenceMessenger.Default.Register<AccountAddedMessage>(this, (_, _) => RefreshConsoles());
    }

    public ObservableCollection<ConsoleModel> Consoles { get; } = [];
    public ObservableCollection<Account> AvailableAccounts { get; } = [];

    [ObservableProperty] private ConsoleModel? _selectedConsole;

    partial void OnSelectedConsoleChanged(ConsoleModel? value)
    {
        LoadProfiles();
    }

    [ObservableProperty] private bool _isUpdating;

    [ObservableProperty] private string _updateStatus = string.Empty;

    [ObservableProperty] private double _updateProgress;

    public ObservableCollection<ConsoleProfile> SelectedConsoleProfiles { get; } = [];

    [RelayCommand]
    private void LoadConsoles()
    {
        if (Consoles.Count > 0) return;
        RefreshConsoles();
    }

    private void RefreshConsoles()
    {
        LoadAvailableAccounts();
        Consoles.Clear();
        foreach (ConsoleModel console in _consolesRepository.GetAllConsoles())
        {
            Consoles.Add(console);
        }
    }

    private void LoadAvailableAccounts()
    {
        AvailableAccounts.Clear();
        foreach (Account account in _accountRepository.GetAll())
        {
            AvailableAccounts.Add(account);
        }
    }

    private void LoadProfiles()
    {
        foreach (ConsoleProfile profile in SelectedConsoleProfiles)
        {
            profile.PropertyChanged -= OnProfilePropertyChanged;
        }

        SelectedConsoleProfiles.Clear();
        if (SelectedConsole?.Id == null) return;
        foreach (ConsoleProfile profile in _consolesRepository.GetProfilesForConsole(SelectedConsole.Id.Value))
        {
            profile.PropertyChanged += OnProfilePropertyChanged;
            SelectedConsoleProfiles.Add(profile);
        }
    }

    public void DeleteConsole(ConsoleModel console)
    {
        if (!console.Id.HasValue) return;
        _consolesRepository.DeleteConsole(console.Id.Value);
        RefreshConsoles();
    }

    public async Task UpdateConsole(int consoleId, string entitlementsFilePath)
    {
        IsUpdating = true;
        UpdateProgress = 0;

        try
        {
            var progress = new System.Progress<ProgressReport>(report =>
            {
                UpdateStatus = report.Status;
                if (report.Progress.HasValue)
                {
                    UpdateProgress = report.Progress.Value;
                }
            });

            await _consolesRepository.UpdateEntitlements(consoleId, entitlementsFilePath, progress);
        }
        finally
        {
            IsUpdating = false;
            UpdateStatus = string.Empty;
            RefreshConsoles();
            WeakReferenceMessenger.Default.Send(new ConsolesChangedMessage());
        }
    }

    public async Task RegisterConsole(string name, string entitlementsFilePath)
    {
        IsUpdating = true;
        UpdateProgress = 0;

        ConsoleModel console = new() { Name = name };

        try
        {
            UpdateStatus = "Creating console...";
            _consolesRepository.UpsertConsole(console);

            if (console.Id.HasValue)
            {
                var progress = new System.Progress<ProgressReport>(report =>
                {
                    UpdateStatus = report.Status;
                    if (report.Progress.HasValue)
                    {
                        UpdateProgress = report.Progress.Value;
                    }
                });

                await _consolesRepository.UpdateEntitlements(console.Id.Value, entitlementsFilePath, progress);
            }
        }
        catch
        {
            if (console.Id.HasValue)
            {
                _consolesRepository.DeleteConsole(console.Id.Value);
            }

            throw;
        }
        finally
        {
            IsUpdating = false;
            UpdateStatus = string.Empty;
            RefreshConsoles();
            WeakReferenceMessenger.Default.Send(new ConsolesChangedMessage());
        }
    }

    private void UpdateProfileAccount(ConsoleProfile profile)
    {
        _consolesRepository.UpsertProfile(profile);

        if (profile.AccountId != null)
        {
            Account? account = _accountRepository.GetById(profile.AccountId);
            profile.OnlineId = account?.OnlineId;
            WeakReferenceMessenger.Default.Send(new ConsolesChangedMessage());
        }
        else
        {
            profile.OnlineId = null;
        }
    }

    private void OnProfilePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConsoleProfile.AccountId) && sender is ConsoleProfile profile)
        {
            UpdateProfileAccount(profile);
        }
    }
}