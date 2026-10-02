namespace PSNav.Infrastructure.Windowing;

public sealed record WindowPlacement(
    string ScreenLayout,
    int X,
    int Y,
    double Width,
    double Height,
    bool IsMaximized);

public interface IWindowPlacementStore
{
    WindowPlacement? Load(string windowKey);

    void Save(string windowKey, WindowPlacement placement);
}
