using System;
using System.Collections.Generic;
using System.Reflection;
using PSNav.Models.Backloggery;
using PSNav.ViewModels.Backloggery;

namespace PSNav.ViewModels.Settings;

public sealed class SettingsViewModel(BackloggeryViewModel backloggery) : ViewModelBase
{
    public BackloggeryViewModel Backloggery { get; } = backloggery;

    public string Version { get; } = GetVersion();

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

    private static string GetVersion()
    {
        string? version = typeof(SettingsViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (version is null)
            return "Unknown";

        // Nightly builds are versioned 0.0.0-nightly, the numeric part is only a placeholder
        version = version.Replace("0.0.0-nightly", "nightly", StringComparison.Ordinal);

        int separator = version.IndexOf('+');
        if (separator < 0)
            return version;

        string revision = version[(separator + 1)..];
        return $"{version[..separator]} ({revision[..Math.Min(7, revision.Length)]})";
    }
}
