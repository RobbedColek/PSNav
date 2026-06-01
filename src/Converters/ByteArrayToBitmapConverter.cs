using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace PSNav.Converters;

public sealed class ByteArrayToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] bytes || bytes.Length == 0)
        {
            return null;
        }

        if (parameter is not string paramStr || !int.TryParse(paramStr, out int targetWidth))
            return new Bitmap(new MemoryStream(bytes));
        using MemoryStream stream = new(bytes);
        return Bitmap.DecodeToWidth(stream, targetWidth);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}