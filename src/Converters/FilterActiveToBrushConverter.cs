using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PSNav.Converters;

public sealed class FilterActiveToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
        {
            return Brushes.Red;
        }

        return Brush.Parse("#FF9A9A9A");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}