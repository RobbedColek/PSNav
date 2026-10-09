using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PSNav.Models.Backloggery;

public sealed partial class BackloggerySyncItem : ViewModels.ViewModelBase
{
    public required string Name { get; init; }

    public string? OriginalTitle { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitleDisplay), nameof(EditSummary))]
    public partial string Title { get; set; } = string.Empty;
    public byte[]? Image { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditSummary))]
    public partial bool? StealthSaveOverride { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditSummary))]
    public partial BackloggeryPriority? PriorityOverride { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditSummary))]
    public partial BackloggeryGameStatus? StatusOverride { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditSummary))]
    public partial BackloggerySaveOptions PreviewDefaults { get; set; } = new(
        true, BackloggeryPriority.Normal, BackloggeryGameStatus.Unplayed);

    public required IReadOnlyDictionary<string, IReadOnlyList<string>> EntitlementIds { get; init; }

    public required string Platform { get; init; }

    public bool ShowPlatform { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MatchDisplay), nameof(StatusDisplay))]
    public partial BackloggeryGame? MatchedGame { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MatchDisplay), nameof(StatusDisplay), nameof(NeedsReview))]
    public partial BackloggeryMatchLevel MatchLevel { get; set; }

    [ObservableProperty] public partial bool IsSelected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplay), nameof(HasError))]
    public partial string? Error { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectable), nameof(StatusDisplay), nameof(EditSummary))]
    public partial bool IsSynced { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectable), nameof(StatusDisplay), nameof(TitleDisplay), nameof(EditSummary))]
    public partial bool IsIgnored { get; set; }

    public bool IsSelectable => !IsSynced && !IsIgnored;

    public string EditSummary
    {
        get
        {
            if (!IsSelectable)
                return string.Empty;

            List<string> values = [];

            if (Title != (OriginalTitle ?? Name))
                values.Add("Title changed");

            BackloggerySaveOptions options = ResolveSaveOptions(PreviewDefaults);
            values.Add(options.Priority == BackloggeryPriority.NowPlaying ? "Now Playing" : options.Priority.ToString());
            values.Add(options.Status.ToString());
            values.Add(options.UseStealthSave ? "Stealth Save" : "Regular Save");
            return string.Join(" · ", values);
        }
    }

    public BackloggerySaveOptions ResolveSaveOptions(BackloggerySaveOptions defaults) => new(
        StealthSaveOverride ?? defaults.UseStealthSave,
        PriorityOverride ?? defaults.Priority,
        StatusOverride ?? defaults.Status);

    public IEnumerable<string> AllEntitlementIds => EntitlementIds.Values.SelectMany(ids => ids).Distinct();

    public bool NeedsReview => MatchLevel == BackloggeryMatchLevel.Possible;

    public bool HasError => Error is not null;

    public string TitleDisplay => IsIgnored ? "—" : Title;

    public string StatusDisplay => Error is not null
        ? $"✕ {Error}"
        : IsIgnored
            ? "Ignored by user"
            : IsSynced && MatchedGame is not null
                ? $"Synchronized ({BackloggeryPlatforms.GetPlatform(MatchedGame.PlatformId)}, " +
                  $"{BackloggeryPlatforms.GetFormatTitle(MatchedGame.FormatId)})"
                : MatchDisplay;

    public string MatchDisplay => MatchedGame is null
        ? "—"
        : $"{(MatchLevel == BackloggeryMatchLevel.Exact ? "✓" : "⚠")} {MatchedGame.Title} " +
          $"({BackloggeryPlatforms.GetPlatform(MatchedGame.PlatformId)}, " +
          $"{BackloggeryPlatforms.GetFormatTitle(MatchedGame.FormatId)})";

    public void MarkIgnored()
    {
        IsSelected = false;
        IsIgnored = true;
        Error = null;
    }

    public void MarkSynced(BackloggeryGame game)
    {
        IsSelected = false;
        IsSynced = true;
        Error = null;
        Title = game.Title;
        MatchedGame = game;
        MatchLevel = BackloggeryMatchLevel.Exact;
    }
}
