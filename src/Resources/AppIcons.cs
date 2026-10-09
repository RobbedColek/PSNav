using Avalonia;
using Avalonia.Controls;

namespace PSNav.Resources;

public static class AppIcons
{
    public static object Get(string key) =>
        Application.Current?.TryFindResource(key, out object? resource) == true && resource is not null
            ? resource
            : key;
}
