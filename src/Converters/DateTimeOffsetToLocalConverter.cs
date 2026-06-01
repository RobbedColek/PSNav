using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PSNav.Converters;

public class DateTimeOffsetToLocalConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dto)
        {
            return dto.ToLocalTime().DateTime;
        }

        return value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}