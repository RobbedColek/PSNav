using ColekUI.Windowing;

namespace PSNav.Repositories.Settings;

public sealed class WindowPlacementStore(ISettingsRepository settings) : IWindowPlacementStore
{
    private static string SettingKey(string windowKey) => $"WindowPlacement:{windowKey}";

    public WindowPlacement? Load(string windowKey) => settings.Get<WindowPlacement>(SettingKey(windowKey));

    public void Save(string windowKey, WindowPlacement placement) => settings.Set(SettingKey(windowKey), placement);
}
