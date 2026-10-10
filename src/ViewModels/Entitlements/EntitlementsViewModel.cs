using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PSNav.Infrastructure.Export;
using PSNav.Models.Entitlements;
using PSNav.Models.Messages;
using PSNav.Repositories.Entitlements;
using PSNav.Repositories.Settings;

namespace PSNav.ViewModels.Entitlements;

public partial class FilterOption : ViewModelBase
{
    public string DisplayName { get; init; } = string.Empty;
    public object? Value { get; init; }

    [ObservableProperty] private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        OnSelectionChanged?.Invoke();
        if (!value) OnHiddenAction?.Invoke();
    }

    public Action? OnSelectionChanged { get; init; }
    public Action? OnHiddenAction { get; init; }
}

public sealed partial class EntitlementsViewModel : ViewModelBase
{
    private const string HiddenColumnsSettingKey = "Entitlements:HiddenColumns";

    private readonly IEntitlementsRepository _entitlementsRepository;
    private readonly ISettingsRepository _settingsRepository;

    public EntitlementsViewModel(IEntitlementsRepository entitlementsRepository,
        ISettingsRepository settingsRepository)
    {
        _entitlementsRepository = entitlementsRepository;
        _settingsRepository = settingsRepository;
        InitializeColumns();
        WeakReferenceMessenger.Default.Register<ConsolesChangedMessage>(this, (_, _) => RefreshAllEntitlements());
        WeakReferenceMessenger.Default.Register<AccountsSynchronizedMessage>(this, (_, _) => RefreshAllEntitlements());
        WeakReferenceMessenger.Default.Register<AccountDeletedMessage>(this, (_, _) => RefreshAllEntitlements());
        WeakReferenceMessenger.Default.Register<AccountUpdatedMessage>(this, (_, _) => RefreshAllEntitlements());
    }

    public ObservableCollection<FilterOption> AvailableColumns { get; } = [];

    private bool _columnsInitialized;

    private void InitializeColumns()
    {
        if (_columnsInitialized) return;
        _columnsInitialized = true;
        var hidden = _settingsRepository.Get<List<string>>(HiddenColumnsSettingKey)?.ToHashSet() ?? [];

        void AddColumn(string name, ObservableCollection<FilterOption>? filterToClear = null)
        {
            AvailableColumns.Add(new FilterOption
            {
                DisplayName = name,
                IsSelected = !hidden.Contains(name),
                OnSelectionChanged = SaveColumnSelection,
                OnHiddenAction = filterToClear is null ? null : () => ClearFilter(filterToClear)
            });
        }

        AddColumn("Icon");
        AddColumn("Name");
        AddColumn("Title ID");
        AddColumn("Owner", AvailableOwners);
        AddColumn("Platform", AvailablePlatforms);
        AddColumn("Type", AvailableTypes);
        AddColumn("Retention", AvailableRetentions);
        AddColumn("Feature", AvailableFeatures);
        AddColumn("Membership", AvailableMemberships);
    }

    private void SaveColumnSelection() =>
        _settingsRepository.Set(HiddenColumnsSettingKey,
            AvailableColumns.Where(c => !c.IsSelected).Select(c => c.DisplayName).ToList());

    private static readonly Dictionary<string, Func<Entitlement, string?>> ExportValueSelectors = new()
    {
        ["Name"] = e => e.Name,
        ["Title ID"] = e => e.TitleId,
        ["Owner"] = e => e.OwnerOnlineId,
        ["Platform"] = e => e.Platform,
        ["Type"] = e => e.TypeDisplayString,
        ["Retention"] = e => e.RetentionPolicyDisplayString,
        ["Feature"] = e => e.FeatureTypeDisplayString,
        ["Membership"] = e => e.MembershipDisplayString
    };

    public async Task ExportToCsvAsync(Stream stream, IEnumerable<Entitlement> rows)
    {
        var columns = AvailableColumns
            .Where(c => c.IsSelected && ExportValueSelectors.ContainsKey(c.DisplayName))
            .Select(c => (Header: c.DisplayName, Selector: ExportValueSelectors[c.DisplayName]))
            .ToList();

        await CsvWriter.WriteAsync(stream,
            columns.Select(c => c.Header).ToList(),
            rows.Select(e => (IReadOnlyList<string?>)columns.Select(c => c.Selector(e)).ToList()));
    }

    private void ClearFilter(ObservableCollection<FilterOption> options)
    {
        foreach (FilterOption option in options) option.IsSelected = false;
        ApplyFilters();
    }

    private IReadOnlyList<Entitlement>? _allEntitlements;
    public ObservableCollection<Entitlement> FilteredEntitlements { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    public ObservableCollection<FilterOption> AvailableOwners { get; } = [];
    public ObservableCollection<FilterOption> AvailablePlatforms { get; } = [];
    public ObservableCollection<FilterOption> AvailableTypes { get; } = [];
    public ObservableCollection<FilterOption> AvailableRetentions { get; } = [];
    public ObservableCollection<FilterOption> AvailableFeatures { get; } = [];
    public ObservableCollection<FilterOption> AvailableMemberships { get; } = [];
    private ObservableCollection<FilterOption> AvailableIsActive { get; } = [];

    public bool IsOwnerFiltered => AvailableOwners.Any(x => x.IsSelected);
    public bool IsPlatformFiltered => AvailablePlatforms.Any(x => x.IsSelected);
    public bool IsTypeFiltered => AvailableTypes.Any(x => x.IsSelected);
    public bool IsRetentionsFiltered => AvailableRetentions.Any(x => x.IsSelected);
    public bool IsFeaturesFiltered => AvailableFeatures.Any(x => x.IsSelected);
    public bool IsMembershipsFiltered => AvailableMemberships.Any(x => x.IsSelected);
    public bool IsActiveFiltered => AvailableIsActive.Any(x => x.IsSelected);

    [ObservableProperty] private Entitlement? _selectedEntitlement;

    [ObservableProperty] private bool _isImageOverlayVisible;

    [ObservableProperty] private byte[]? _fullImageBytes;

    [RelayCommand]
    private void LoadAllEntitlements()
    {
        InitializeColumns();
        if (FilteredEntitlements.Count > 0) return;
        RefreshAllEntitlements();
    }

    private void RefreshAllEntitlements()
    {
        _allEntitlements = _entitlementsRepository.GetAllEntitlements();
        UpdateFilterOptions();
        ApplyFilters();
    }

    private void UpdateFilterOptions()
    {
        if (_allEntitlements is null) return;

        var owners = _allEntitlements.Select(e => e.OwnerOnlineId).Distinct().OrderBy(x => x).ToList();
        var platforms = _allEntitlements.Select(e => e.Platform).Distinct().OrderBy(x => x == "N/A").ThenBy(x => x)
            .ToList();
        var types = _allEntitlements.Select(e => e.TypeDisplayString).Distinct().OrderBy(x => x == "N/A").ThenBy(x => x)
            .ToList();
        var retentions = _allEntitlements.Select(e => e.RetentionPolicyDisplayString).Distinct()
            .OrderBy(x => x == "N/A").ThenBy(x => x).ToList();
        var features = _allEntitlements.Select(e => e.FeatureTypeDisplayString).Distinct().OrderBy(x => x == "N/A")
            .ThenBy(x => x).ToList();
        var memberships = _allEntitlements.Select(e => e.MembershipDisplayString).Distinct().OrderBy(x => x == "N/A")
            .ThenBy(x => x).ToList();

        UpdateFilterCollection(AvailableOwners, owners);
        UpdateFilterCollection(AvailablePlatforms, platforms);
        UpdateFilterCollection(AvailableTypes, types);
        UpdateFilterCollection(AvailableRetentions, retentions);
        UpdateFilterCollection(AvailableFeatures, features);
        UpdateFilterCollection(AvailableMemberships, memberships);

        UpdateIsActiveFilter();
    }

    private void UpdateFilterCollection(ObservableCollection<FilterOption> collection, List<string?> newItems)
    {
        var currentSelected = collection.Where(x => x.IsSelected).Select(x => x.Value as string).ToHashSet();

        var toAdd = newItems.Where(x => x != null).Cast<string>().ToList();

        var currentItems = collection.Select(x => x.DisplayName).ToList();
        if (currentItems.SequenceEqual(toAdd)) return;

        collection.Clear();
        foreach (var item in toAdd)
        {
            collection.Add(new FilterOption
            {
                DisplayName = item,
                Value = item,
                IsSelected = currentSelected.Contains(item),
                OnSelectionChanged = ApplyFilters
            });
        }
    }

    private void UpdateIsActiveFilter()
    {
        if (AvailableIsActive.Count > 0) return;

        AvailableIsActive.Add(new FilterOption
            { DisplayName = "Active", Value = true, OnSelectionChanged = ApplyFilters });
        AvailableIsActive.Add(new FilterOption
            { DisplayName = "Inactive", Value = false, OnSelectionChanged = ApplyFilters });
    }

    private void ApplyFilters()
    {
        if (_allEntitlements is null) return;

        OnPropertyChanged(nameof(IsOwnerFiltered));
        OnPropertyChanged(nameof(IsPlatformFiltered));
        OnPropertyChanged(nameof(IsTypeFiltered));
        OnPropertyChanged(nameof(IsRetentionsFiltered));
        OnPropertyChanged(nameof(IsFeaturesFiltered));
        OnPropertyChanged(nameof(IsMembershipsFiltered));
        OnPropertyChanged(nameof(IsActiveFiltered));

        var filtered = _allEntitlements.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(e =>
                (e.Name != null && e.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (e.TitleId != null && e.TitleId.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (e.EntitlementId.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (e.ProductId != null && e.ProductId.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            );
        }

        var selectedOwners = AvailableOwners.Where(x => x.IsSelected).Select(x => x.Value as string).ToList();
        if (selectedOwners.Count > 0)
        {
            filtered = filtered.Where(e => selectedOwners.Contains(e.OwnerOnlineId));
        }

        var selectedPlatforms = AvailablePlatforms.Where(x => x.IsSelected).Select(x => x.Value as string).ToList();
        if (selectedPlatforms.Count > 0)
        {
            filtered = filtered.Where(e => selectedPlatforms.Contains(e.Platform));
        }

        var selectedTypes = AvailableTypes.Where(x => x.IsSelected).Select(x => x.Value as string).ToList();
        if (selectedTypes.Count > 0)
        {
            filtered = filtered.Where(e => selectedTypes.Contains(e.TypeDisplayString));
        }

        var selectedRetentions = AvailableRetentions.Where(x => x.IsSelected).Select(x => x.Value as string).ToList();
        if (selectedRetentions.Count > 0)
        {
            filtered = filtered.Where(e => selectedRetentions.Contains(e.RetentionPolicyDisplayString));
        }

        var selectedFeatures = AvailableFeatures.Where(x => x.IsSelected).Select(x => x.Value as string).ToList();
        if (selectedFeatures.Count > 0)
        {
            filtered = filtered.Where(e => selectedFeatures.Contains(e.FeatureTypeDisplayString));
        }

        var selectedMemberships = AvailableMemberships.Where(x => x.IsSelected).Select(x => x.Value as string).ToList();
        if (selectedMemberships.Count > 0)
        {
            filtered = filtered.Where(e => selectedMemberships.Contains(e.MembershipDisplayString));
        }

        var selectedIsActive = AvailableIsActive.Where(x => x.IsSelected).Select(x => (bool)x.Value!).ToList();
        if (selectedIsActive.Count is > 0 and < 2)
        {
            var isActiveValue = selectedIsActive[0];
            filtered = filtered.Where(e => e.IsActive == isActiveValue);
        }

        var sorted = filtered.OrderBy(e => e.Name ?? string.Empty).ToList();

        FilteredEntitlements.Clear();
        foreach (Entitlement ent in sorted)
        {
            FilteredEntitlements.Add(ent);
        }
    }
}