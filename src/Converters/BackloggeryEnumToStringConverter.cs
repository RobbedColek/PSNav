using System;
using System.Globalization;
using Avalonia.Data.Converters;
using PSNav.Models.Backloggery;

namespace PSNav.Converters;

public sealed class BackloggeryEnumToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            BackloggeryCrossPlatformMode.Both => "Upload both versions",
            BackloggeryCrossPlatformMode.Ps5Only => "Prefer PS5",
            BackloggeryCrossPlatformMode.Ps4Only => "Prefer PS4",
            BackloggeryPriority.NowPlaying => "Now Playing",
            _ => value?.ToString()
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
