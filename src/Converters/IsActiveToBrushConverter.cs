using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PSNav.Converters;

public sealed class IsActiveToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? Brushes.White : Brush.Parse("#FF9A9A9A");
        }

        return Brushes.White;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}