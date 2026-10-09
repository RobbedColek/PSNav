using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PSNav.Models.Backloggery;
using PSNav.Models.Common;
using PSNav.Models.Messages;
using PSNav.Repositories.Settings;
using PSNav.Services.Backloggery;

namespace PSNav.ViewModels.Backloggery;

public sealed partial class BackloggeryViewModel : ViewModelBase
{
    private const string CrossPlatformModeSettingKey = "Backloggery:CrossPlatformMode";
    private const string UseStealthSaveSettingKey = "Backloggery:UseStealthSave";
    private const string DefaultPrioritySettingKey = "Backloggery:DefaultPriority";
    private const string DefaultStatusSettingKey = "Backloggery:DefaultStatus";

    private readonly IBackloggerySyncService _syncService;
    private readonly ISettingsRepository _settingsRepository;
    private readonly bool _settingsLoaded;
    private BackloggerySyncData? _data;
    private CancellationTokenSource? _applyCancellation;
    private bool _sessionChecked;

    public BackloggeryViewModel(IBackloggerySyncService syncService, ISettingsRepository settingsRepository)
    {
        _syncService = syncService;
        _settingsRepository = settingsRepository;
        BackloggeryCrossPlatformMode? savedMode =
            _settingsRepository.Get<BackloggeryCrossPlatformMode?>(CrossPlatformModeSettingKey);
        CrossPlatformMode = savedMode.HasValue && Enum.IsDefined(savedMode.Value)
            ? savedMode.Value
            : BackloggeryCrossPlatformMode.Both;
        UseStealthSave = _settingsRepository.Get<bool?>(UseStealthSaveSettingKey) ?? true;
        BackloggeryPriority? savedPriority =
            _settingsRepository.Get<BackloggeryPriority?>(DefaultPrioritySettingKey);
        DefaultPriority = savedPriority.HasValue && Enum.IsDefined(savedPriority.Value)
            ? savedPriority.Value
            : BackloggeryPriority.Normal;
        BackloggeryGameStatus? savedStatus =
            _settingsRepository.Get<BackloggeryGameStatus?>(DefaultStatusSettingKey);
        DefaultStatus = savedStatus.HasValue && Enum.IsDefined(savedStatus.Value)
            ? savedStatus.Value
            : BackloggeryGameStatus.Unplayed;
        _settingsLoaded = true;
        Username = _syncService.Session?.Username ?? string.Empty;

        WeakReferenceMessenger.Default.Register<ConsolesChangedMessage>(this, (_, _) => ClearPlan());
        WeakReferenceMessenger.Default.Register<AccountsSynchronizedMessage>(this, (_, _) => ClearPlan());
        WeakReferenceMessenger.Default.Register<AccountDeletedMessage>(this, (_, _) => ClearPlan());
    }

    public event Action<string, Exception>? ErrorOccurred;

    [ObservableProperty] public partial IReadOnlyList<BackloggerySyncItem> Items { get; private set; } = [];

    // Session

    public bool HasSession => _syncService.Session is not null;

    public bool ShowLogin => !HasSession || (_sessionChecked && !IsSessionValid);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SessionText), nameof(ShowLogin))]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand), nameof(ApplyCommand))]
    public partial bool IsSessionValid { get; set; }

    public string SessionText
    {
        get
        {
            BackloggerySession? session = _syncService.Session;

            if (session is null)
                return string.Empty;

            if (!IsSessionValid)
                return $"The session for {session.Username} has expired. Log in again to continue.";

            if (session.ExpiresAt is null)
                return $"Logged in as {session.Username}";

            int days = Math.Max(0, (int)(session.ExpiresAt.Value - DateTimeOffset.Now).TotalDays);
            return $"Logged in as {session.Username} · session expires in {days} day{(days == 1 ? "" : "s")}";
        }
    }

    [ObservableProperty] public partial string Username { get; set; }
    [ObservableProperty] public partial string Password { get; set; } = string.Empty;
    [ObservableProperty] public partial string LogToken { get; set; } = string.Empty;
    [ObservableProperty] public partial string? LoginError { get; set; }

    // Progress

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand), nameof(LoginWithTokenCommand), nameof(AnalyzeCommand),
        nameof(ApplyCommand), nameof(SelectAllCommand), nameof(UnselectAllCommand), nameof(SelectUnmatchedCommand),
        nameof(UnselectUnmatchedCommand), nameof(EditCommand), nameof(SaveEditCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty] public partial bool IsApplying { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = string.Empty;
    [ObservableProperty] public partial double Progress { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveEditCommand))]
    public partial BackloggeryGameEditorViewModel? Editor { get; private set; }

    private bool CanEdit(BackloggerySyncItem? item) =>
        !IsBusy && item is { IsSelectable: true } && Items.Contains(item);

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Edit(BackloggerySyncItem? item)
    {
        if (!CanEdit(item))
            return;
        Editor = new(item!, new(UseStealthSave, DefaultPriority, DefaultStatus));
    }

    private bool CanSaveEdit() => Editor is { } editor && CanEdit(editor.Item) &&
        !string.IsNullOrWhiteSpace(editor.Title);

    [RelayCommand(CanExecute = nameof(CanSaveEdit))]
    private void SaveEdit()
    {
        if (!CanSaveEdit())
            return;
        Editor!.Commit();
        CloseEditor();
    }

    [RelayCommand]
    private void CloseEditor() => Editor = null;

    partial void OnEditorChanged(BackloggeryGameEditorViewModel? oldValue, BackloggeryGameEditorViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.PropertyChanged -= Editor_PropertyChanged;
        if (newValue is not null)
            newValue.PropertyChanged += Editor_PropertyChanged;
    }

    private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        SaveEditCommand.NotifyCanExecuteChanged();

    // Options

    [ObservableProperty] public partial BackloggeryCrossPlatformMode CrossPlatformMode { get; set; }
    [ObservableProperty] public partial bool UseStealthSave { get; set; } = true;
    [ObservableProperty] public partial BackloggeryPriority DefaultPriority { get; set; } = BackloggeryPriority.Normal;
    [ObservableProperty] public partial BackloggeryGameStatus DefaultStatus { get; set; } = BackloggeryGameStatus.Unplayed;

    public bool HasPlan => _data is not null;

    public string SummaryText
    {
        get
        {
            int selectable = Items.Count(item => item.IsSelectable);
            int selected = Items.Count(item => item.IsSelected);
            int synced = Items.Count(item => item.IsSynced);
            int ignored = Items.Count(item => item.IsIgnored);
            int present = Items.Count(item => item.IsSelectable && item.MatchLevel == BackloggeryMatchLevel.Exact);
            int review = Items.Count(item => item.IsSelectable && item.NeedsReview);
            int failed = Items.Count(item => item.Error is not null);

            string summary =
                $"{selected} of {selectable} selected · {present} already on Backloggery · {review} need review · " +
                $"{ignored} ignored · {synced} synchronized";
            return failed > 0 ? $"{summary} · {failed} failed" : summary;
        }
    }

    partial void OnCrossPlatformModeChanged(BackloggeryCrossPlatformMode value)
    {
        if (!_settingsLoaded)
            return;

        _settingsRepository.Set(CrossPlatformModeSettingKey, value);
        RebuildPlan();
    }

    partial void OnUseStealthSaveChanged(bool value) => SavePreference(UseStealthSaveSettingKey, value);

    partial void OnDefaultPriorityChanged(BackloggeryPriority value) => SavePreference(DefaultPrioritySettingKey, value);

    partial void OnDefaultStatusChanged(BackloggeryGameStatus value) => SavePreference(DefaultStatusSettingKey, value);

    private void SavePreference<T>(string key, T value)
    {
        if (_settingsLoaded)
        {
            CloseEditor();
            _settingsRepository.Set(key, value);
            UpdatePreviewDefaults(Items);
        }
    }

    private void UpdatePreviewDefaults(IEnumerable<BackloggerySyncItem> items)
    {
        BackloggerySaveOptions defaults = new(UseStealthSave, DefaultPriority, DefaultStatus);
        foreach (BackloggerySyncItem item in items)
            item.PreviewDefaults = defaults;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (_sessionChecked || !HasSession)
            return;

        _sessionChecked = true;
        IsBusy = true;
        Status = "Checking Backloggery session...";

        try
        {
            IsSessionValid = await _syncService.ValidateSessionAsync();
        }
        catch (Exception exception)
        {
            IsSessionValid = _syncService.Session?.IsExpired == false;
            ErrorOccurred?.Invoke("Could not check the Backloggery session", exception);
        }
        finally
        {
            OnPropertyChanged(nameof(ShowLogin));
            IsBusy = false;
            Status = string.Empty;
        }
    }

    private bool CanLogin() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password))
        {
            LoginError = "Enter your username and password.";
            return;
        }

        await LoginCoreAsync(() => _syncService.LoginAsync(Username.Trim(), Password));
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginWithTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(LogToken))
        {
            LoginError = "Paste the value of the log_token cookie.";
            return;
        }

        await LoginCoreAsync(() => _syncService.LoginWithTokenAsync(LogToken.Trim()));
    }

    private async Task LoginCoreAsync(Func<Task<BackloggerySession>> login)
    {
        IsBusy = true;
        LoginError = null;
        Status = "Logging in to Backloggery...";

        try
        {
            BackloggerySession session = await login();
            Username = session.Username;
            Password = string.Empty;
            LogToken = string.Empty;
            _sessionChecked = true;
            IsSessionValid = true;
            OnPropertyChanged(nameof(HasSession));
            OnPropertyChanged(nameof(SessionText));
            OnPropertyChanged(nameof(ShowLogin));
        }
        catch (Exception exception)
        {
            LoginError = exception.Message;
        }
        finally
        {
            IsBusy = false;
            Status = string.Empty;
        }
    }

    private bool CanAnalyze() => !IsBusy && IsSessionValid;

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        CloseEditor();
        IsBusy = true;
        Status = "Fetching Backloggery library and analyzing entitlements...";

        try
        {
            _data = await _syncService.FetchDataAsync();
            RebuildPlan();
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke("Could not analyze entitlements", exception);
        }
        finally
        {
            IsBusy = false;
            Status = string.Empty;
        }
    }

    private bool CanApply() => !IsBusy && IsSessionValid && Items.Any(item => item.IsSelected);

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        CloseEditor();
        BackloggerySaveOptions saveOptions = new(UseStealthSave, DefaultPriority, DefaultStatus);
        IsBusy = true;
        IsApplying = true;
        Progress = 0;
        _applyCancellation = new CancellationTokenSource();

        Progress<ProgressReport> progress = new(report =>
        {
            Status = report.Status;
            if (report.Progress.HasValue)
            {
                Progress = report.Progress.Value;
            }
        });

        try
        {
            await _syncService.ApplyAsync(Items.ToList(), saveOptions, progress, _applyCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            //
        }
        catch (BackloggerySessionExpiredException exception)
        {
            IsSessionValid = false;
            ErrorOccurred?.Invoke("Backloggery session expired", exception);
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke("Synchronization failed", exception);
        }
        finally
        {
            _applyCancellation.Dispose();
            _applyCancellation = null;

            OnPropertyChanged(nameof(SummaryText));
            IsApplying = false;
            IsBusy = false;
            Status = string.Empty;
        }
    }

    [RelayCommand]
    private void Cancel() => _applyCancellation?.Cancel();

    private bool CanChangeSelection() => !IsBusy && Items.Count > 0;

    [RelayCommand(CanExecute = nameof(CanChangeSelection))]
    private void SelectAll() => SetSelection(true, unmatchedOnly: false);

    [RelayCommand(CanExecute = nameof(CanChangeSelection))]
    private void UnselectAll() => SetSelection(false, unmatchedOnly: false);

    [RelayCommand(CanExecute = nameof(CanChangeSelection))]
    private void SelectUnmatched() => SetSelection(true, unmatchedOnly: true);

    [RelayCommand(CanExecute = nameof(CanChangeSelection))]
    private void UnselectUnmatched() => SetSelection(false, unmatchedOnly: true);

    public void SetIgnored(IEnumerable<BackloggerySyncItem> items, bool ignored)
    {
        if (IsBusy)
            return;

        try
        {
            _syncService.SetIgnored(items.ToList(), ignored);
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke(ignored ? "Could not ignore games" : "Could not un-ignore games", exception);
        }
    }

    private void SetSelection(bool isSelected, bool unmatchedOnly)
    {
        foreach (BackloggerySyncItem item in Items.Where(item =>
                     item.IsSelectable && (!unmatchedOnly || item.MatchedGame is null)))
        {
            item.IsSelected = isSelected;
        }
    }

    private void RebuildPlan()
    {
        if (_data is null || IsApplying)
            return;

        ReplaceItems(_syncService.BuildPlan(_data, CrossPlatformMode));
    }

    private void ClearPlan()
    {
        if (IsApplying)
            return;

        _data = null;
        ReplaceItems([]);
    }

    private void ReplaceItems(IReadOnlyList<BackloggerySyncItem> items)
    {
        CloseEditor();
        UpdatePreviewDefaults(items);
        foreach (BackloggerySyncItem item in Items)
        {
            item.PropertyChanged -= Item_PropertyChanged;
        }

        foreach (BackloggerySyncItem item in items)
        {
            item.PropertyChanged += Item_PropertyChanged;
        }

        Items = items;
        OnPlanChanged();
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BackloggerySyncItem.IsSelected) or nameof(BackloggerySyncItem.Error)
            or nameof(BackloggerySyncItem.IsSynced) or nameof(BackloggerySyncItem.IsIgnored))
        {
            OnPropertyChanged(nameof(SummaryText));
            ApplyCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            SaveEditCommand.NotifyCanExecuteChanged();
            if (Editor is { Item.IsSelectable: false })
                CloseEditor();
        }
    }

    private void OnPlanChanged()
    {
        EditCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasPlan));
        ApplyCommand.NotifyCanExecuteChanged();
        SelectAllCommand.NotifyCanExecuteChanged();
        UnselectAllCommand.NotifyCanExecuteChanged();
        SelectUnmatchedCommand.NotifyCanExecuteChanged();
        UnselectUnmatchedCommand.NotifyCanExecuteChanged();
    }
}
