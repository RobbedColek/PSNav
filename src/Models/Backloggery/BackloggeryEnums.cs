namespace PSNav.Models.Backloggery;

public enum BackloggeryMatchLevel
{
    None,
    Possible,
    Exact
}

public enum BackloggerySyncStatus
{
    Added,
    Ignored
}

public enum BackloggeryCrossPlatformMode
{
    Both,
    Ps5Only,
    Ps4Only
}

public enum BackloggeryGameStatus
{
    Unplayed = 10,
    Unfinished = 20,
    Beaten = 30,
    Completed = 40,
    Endless = 60,
    None = 80
}

public enum BackloggeryPriority
{
    NowPlaying = 80,
    Ongoing = 70,
    Paused = 60,
    High = 50,
    Normal = 40,
    Low = 30,
    Replay = 20,
    Shelved = 10
}
