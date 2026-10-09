using System;
using System.Collections.Generic;
using PSNav.Models.Backloggery;
using PSNav.ViewModels.Backloggery;

namespace PSNav.ViewModels.Settings;

public sealed class SettingsViewModel(BackloggeryViewModel backloggery) : ViewModelBase
{
    public BackloggeryViewModel Backloggery { get; } = backloggery;

    public IReadOnlyList<BackloggeryCrossPlatformMode> CrossPlatformModes { get; } =
        Enum.GetValues<BackloggeryCrossPlatformMode>();

    public IReadOnlyList<BackloggeryPriority> Priorities { get; } =
    [
        BackloggeryPriority.NowPlaying,
        BackloggeryPriority.Ongoing,
        BackloggeryPriority.Paused,
        BackloggeryPriority.High,
        BackloggeryPriority.Normal,
        BackloggeryPriority.Low,
        BackloggeryPriority.Replay,
        BackloggeryPriority.Shelved
    ];

    public IReadOnlyList<BackloggeryGameStatus> Statuses { get; } =
        Enum.GetValues<BackloggeryGameStatus>();
}
