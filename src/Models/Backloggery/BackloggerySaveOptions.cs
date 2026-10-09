namespace PSNav.Models.Backloggery;

public sealed record BackloggerySaveOptions(
    bool UseStealthSave,
    BackloggeryPriority Priority,
    BackloggeryGameStatus Status);
