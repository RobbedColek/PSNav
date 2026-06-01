using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PSNav.ViewModels;

namespace PSNav;

public class ViewLocator : IDataTemplate
{
    private readonly Dictionary<object, Control> _cache = new();

    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        if (_cache.TryGetValue(data, out Control? cachedView))
        {
            return cachedView;
        }

        var name = data.GetType().FullName!.Replace("ViewModel", "View");
        Type? type = Type.GetType(name);

        if (type == null) return new TextBlock { Text = "Not Found: " + name };
        Control view = (Control)Activator.CreateInstance(type)!;
        _cache[data] = view;
        return view;
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}