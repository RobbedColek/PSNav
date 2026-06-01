using System;
using System.Globalization;
using System.Reflection;
using Avalonia.Data.Converters;
using PSNav.Models.Shell;

namespace PSNav.Converters;

public class IconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not NavigationItem item) return null;

        var icon = Activator.CreateInstance(item.IconType);

        if (icon == null) return null;

        PropertyInfo? widthProp = item.IconType.GetProperty("Width");
        widthProp?.SetValue(icon, 20.0);

        PropertyInfo? heightProp = item.IconType.GetProperty("Height");
        heightProp?.SetValue(icon, 20.0);

        PropertyInfo? kindProperty = item.IconType.GetProperty("Kind");
        kindProperty?.SetValue(icon, item.IconKind);

        return icon;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}