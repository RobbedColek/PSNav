using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using PSNav.Services.Consoles;

namespace PSNav.ViewModels.Consoles;

public sealed partial class ConsolesViewModel : ViewModelBase
{
    private readonly IAccountsRepository _accountRepository;
    private readonly IConsolesRepository _consolesRepository;
    private readonly IConsoleFtpService _ftpService;
    private bool _operationActive;
    private bool _isRefreshing;
    private long _progressGeneration;

    public ConsolesViewModel(IAccountsRepository accountRepository, IConsolesRepository consolesRepository,
        IConsoleFtpService ftpService)
    {
        _accountRepository = accountRepository;
        _consolesRepository = consolesRepository;
        _ftpService = ftpService;
        WeakReferenceMessenger.Default.Register<AccountAddedMessage>(this, (_, _) => RefreshAfterAccountChange());
        WeakReferenceMessenger.Default.Register<AccountDeletedMessage>(this, (_, _) => RefreshAfterAccountChange());
        WeakReferenceMessenger.Default.Register<AccountUpdatedMessage>(this, (_, _) => RefreshAfterAccountChange());
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

    [ObservableProperty] private bool _isUpdateIndeterminate;

    public ObservableCollection<ConsoleProfile> SelectedConsoleProfiles { get; } = [];

    [RelayCommand]
    private void LoadConsoles()
    {
        if (Consoles.Count == 0)
            RefreshConsoles();

        if (!_operationActive)
            SelectedConsole = Consoles.FirstOrDefault();
    }

    private void RefreshConsoles()
    {
        int? selectedId = SelectedConsole?.Id;
        _isRefreshing = true;
        try
        {
            var consoles = _consolesRepository.GetAllConsoles();
            LoadAvailableAccounts();
            Consoles.Clear();

            foreach (ConsoleModel console in consoles)
                Consoles.Add(console);

            ConsoleModel? selection = selectedId.HasValue
                ? Consoles.FirstOrDefault(console => console.Id == selectedId)
                : null;
            bool selectionUnchanged = ReferenceEquals(SelectedConsole, selection);
            SelectedConsole = selection;

            if (selectionUnchanged)
                LoadProfiles();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void RefreshAfterAccountChange()
    {
        if (!_operationActive)
            RefreshConsoles();
    }

    private void LoadAvailableAccounts()
    {
        AvailableAccounts.Clear();
        foreach (Account account in _accountRepository.GetAll())
            AvailableAccounts.Add(account);
    }

    private void LoadProfiles()
    {
        foreach (ConsoleProfile profile in SelectedConsoleProfiles)
            profile.PropertyChanged -= OnProfilePropertyChanged;

        SelectedConsoleProfiles.Clear();

        if (SelectedConsole?.Id == null)
            return;

        foreach (ConsoleProfile profile in _consolesRepository.GetProfilesForConsole(SelectedConsole.Id.Value))
        {
            profile.PropertyChanged += OnProfilePropertyChanged;
            SelectedConsoleProfiles.Add(profile);
        }
    }

    public void DeleteConsole(ConsoleModel console)
    {
        if (_operationActive || !console.Id.HasValue)
            return;

        int consoleId = console.Id.Value;

        BeginOperation();

        try
        {
            _consolesRepository.DeleteConsole(consoleId);
            RefreshConsoles();
        }
        finally
        {
            EndOperation();
            WeakReferenceMessenger.Default.Send(new ConsolesChangedMessage());
        }
    }

    public void SaveConsoleSettings(ConsoleModel console, ConsoleSettings settings)
    {
        if (_operationActive || !console.Id.HasValue)
            return;

        int consoleId = console.Id.Value;

        BeginOperation();

        try
        {
            ConsoleSettings validated = ValidateSettings(settings, false);
            _consolesRepository.UpsertConsole(new ConsoleModel
            {
                Id = consoleId,
                Name = validated.Name,
                IpAddress = validated.IpAddress,
                FtpPort = validated.FtpPort,
                CreatedAt = console.CreatedAt,
                UpdatedAt = console.UpdatedAt
            });
            RefreshConsoles();
        }
        finally
        {
            EndOperation();
        }
    }

    public Task UpdateConsole(int consoleId, string entitlementsFilePath) =>
        ImportConsoleAsync(consoleId, null, entitlementsFilePath, false, false);

    public Task RegisterConsole(string name, string entitlementsFilePath) =>
        ImportConsoleAsync(null, new ConsoleSettings(name, null, 2121), entitlementsFilePath, false, true);

    public Task RegisterConsoleViaFtp(ConsoleSettings settings) =>
        ImportConsoleAsync(null, settings, null, true, false);

    public Task UpdateConsoleViaFtp(ConsoleModel console)
    {
        if (_operationActive || !console.Id.HasValue)
            return Task.CompletedTask;

        int consoleId = console.Id.Value;
        ConsoleSettings settings = new(console.Name, console.IpAddress, console.FtpPort);
        return ImportConsoleAsync(consoleId, settings, null, true, false);
    }

    private async Task ImportConsoleAsync(int? consoleId, ConsoleSettings? settings, string? filePath, bool viaFtp,
        bool databaseValidated)
    {
        if (_operationActive)
            return;

        BeginOperation();

        string? temporaryDirectory = null;
        ConsoleModel? newConsole = null;
        Exception? failure = null;
        ConsoleSettings? validated = null;
        string? ftpPostDownloadStage = null;

        try
        {
            StartStage("Validating console settings...", true);
            validated = settings is null ? null : ValidateSettings(settings, viaFtp);
            if (viaFtp)
            {
                temporaryDirectory = Path.Combine(Path.GetTempPath(), $"PSNav-console-ftp-{Guid.NewGuid():N}");
                Directory.CreateDirectory(temporaryDirectory);
                filePath = Path.Combine(temporaryDirectory, "entitlements.db");
                await _ftpService.DownloadDatabaseAsync(validated!.IpAddress!, validated.FtpPort, filePath,
                    StartStage("Connecting", true));
            }

            if (!databaseValidated)
            {
                StartStage("Validating entitlements database...", true);

                if (viaFtp)
                    ftpPostDownloadStage = "validation";

                EntitlementDatabaseValidator.Validate(filePath!);
            }

            if (!consoleId.HasValue)
            {
                if (viaFtp)
                    ftpPostDownloadStage = "console registration";

                StartStage("Creating console...", true);
                newConsole = new ConsoleModel
                {
                    Name = validated!.Name,
                    IpAddress = validated.IpAddress,
                    FtpPort = validated.FtpPort
                };
                _consolesRepository.UpsertConsole(newConsole);
                consoleId = newConsole.Id ?? throw new InvalidOperationException("The console could not be created.");
            }

            if (viaFtp)
                ftpPostDownloadStage = "entitlement import";

            await _consolesRepository.UpdateEntitlements(consoleId.Value, filePath!,
                StartStage("Importing entitlements...", false));
        }
        catch (Exception exception)
        {
            failure = exception;
            if (newConsole?.Id is int newConsoleId)
            {
                try
                {
                    _consolesRepository.DeleteConsole(newConsoleId);
                }
                catch (Exception cleanupException)
                {
                    Trace.TraceWarning($"Could not remove failed console {newConsoleId}: {cleanupException}");
                }
            }

            if (ftpPostDownloadStage is not null)
            {
                throw new InvalidOperationException(
                    $"FTP {ftpPostDownloadStage} failed for {validated!.IpAddress}:{validated.FtpPort} after download. {exception.Message}",
                    exception);
            }

            throw;
        }
        finally
        {
            _progressGeneration++;
            try
            {
                if (temporaryDirectory is not null)
                {
                    try
                    {
                        if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
                    }
                    catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException)
                    {
                        Trace.TraceWarning($"Could not remove FTP temporary directory {temporaryDirectory}: {cleanupException}");
                    }
                }

                try
                {
                    RefreshConsoles();

                    if (failure is null && newConsole?.Id is int registeredId)
                        SelectedConsole = Consoles.FirstOrDefault(console => console.Id == registeredId);
                }
                catch (Exception refreshException) when (failure is not null)
                {
                    Trace.TraceWarning($"Could not refresh consoles after failed import: {refreshException}");
                }
            }
            finally
            {
                EndOperation();
                WeakReferenceMessenger.Default.Send(new ConsolesChangedMessage());
            }
        }
    }

    private static ConsoleSettings ValidateSettings(ConsoleSettings settings, bool requireIpAddress)
    {
        if (!ConsoleSettingsValidator.TryValidate(settings, requireIpAddress, out ConsoleSettings? validated,
                out string error))
            throw new ArgumentException(error, nameof(settings));
        return validated!;
    }

    private void BeginOperation()
    {
        _operationActive = true;
        _progressGeneration++;
        UpdateProgress = 0;
        UpdateStatus = string.Empty;
        IsUpdateIndeterminate = false;
        IsUpdating = true;
    }

    private IProgress<ProgressReport> StartStage(string status, bool indeterminate)
    {
        long generation = ++_progressGeneration;
        UpdateStatus = status;
        UpdateProgress = 0;
        IsUpdateIndeterminate = indeterminate;
        return new Progress<ProgressReport>(report =>
        {
            if (!_operationActive || generation != _progressGeneration)
                return;
                
            UpdateStatus = report.Status;
            IsUpdateIndeterminate = !report.Progress.HasValue;
            UpdateProgress = report.Progress ?? 0;
        });
    }

    private void EndOperation()
    {
        _progressGeneration++;
        UpdateStatus = string.Empty;
        UpdateProgress = 0;
        IsUpdateIndeterminate = false;
        IsUpdating = false;
        _operationActive = false;
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
        if (!_operationActive && !_isRefreshing &&
            e.PropertyName == nameof(ConsoleProfile.AccountId) && sender is ConsoleProfile profile)
        {
            UpdateProfileAccount(profile);
        }
    }
}