using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PSNav.Models.Backloggery;

namespace PSNav.ViewModels.Backloggery;

public sealed partial class BackloggeryGameEditorViewModel : ViewModelBase
{
    private readonly bool _defaultStealthSave;

    public BackloggeryGameEditorViewModel(BackloggerySyncItem item, BackloggerySaveOptions defaults)
    {
        Item = item;
        _defaultStealthSave = defaults.UseStealthSave;
        Title = item.Title;
        UseStealthSave = item.StealthSaveOverride ?? defaults.UseStealthSave;
        Priorities = [new(null, $"Use default ({PriorityLabel(defaults.Priority)})"),
            .. Enum.GetValues<BackloggeryPriority>().OrderDescending()
                .Select(value => new BackloggeryPriorityChoice(value, PriorityLabel(value)))];
        Statuses = [new(null, $"Use default ({defaults.Status})"),
            .. Enum.GetValues<BackloggeryGameStatus>()
                .Select(value => new BackloggeryStatusChoice(value, value.ToString()))];
        SelectedPriority = Priorities.Single(choice => choice.Value == item.PriorityOverride);
        SelectedStatus = Statuses.Single(choice => choice.Value == item.StatusOverride);
    }

    public BackloggerySyncItem Item { get; }
    public IReadOnlyList<BackloggeryPriorityChoice> Priorities { get; }
    public IReadOnlyList<BackloggeryStatusChoice> Statuses { get; }

    [ObservableProperty] public partial string Title { get; set; }
    [ObservableProperty] public partial bool UseStealthSave { get; set; }
    [ObservableProperty] public partial BackloggeryPriorityChoice SelectedPriority { get; set; }
    [ObservableProperty] public partial BackloggeryStatusChoice SelectedStatus { get; set; }

    public void Commit()
    {
        Item.Title = Title.Trim();
        Item.PriorityOverride = SelectedPriority.Value;
        Item.StatusOverride = SelectedStatus.Value;
        Item.StealthSaveOverride = UseStealthSave == _defaultStealthSave ? null : UseStealthSave;
    }

    private static string PriorityLabel(BackloggeryPriority value) =>
        value == BackloggeryPriority.NowPlaying ? "Now Playing" : value.ToString();
}

public sealed record BackloggeryPriorityChoice(BackloggeryPriority? Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record BackloggeryStatusChoice(BackloggeryGameStatus? Value, string Label)
{
    public override string ToString() => Label;
}
