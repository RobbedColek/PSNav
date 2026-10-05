using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSNav.Infrastructure.Export;

public static class CsvWriter
{
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static async Task WriteAsync(Stream stream, IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string?>> rows)
    {
        await using StreamWriter writer = new(stream, Utf8WithBom, leaveOpen: true);
        writer.NewLine = "\r\n";

        await writer.WriteLineAsync(FormatRow(headers));
        foreach (var row in rows)
        {
            await writer.WriteLineAsync(FormatRow(row));
        }
    }

    private static string FormatRow(IEnumerable<string?> fields)
    {
        return string.Join(',', fields.Select(Escape));
    }

    private static string Escape(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        if (field.IndexOfAny([',', '"', '\r', '\n']) < 0)
            return field;

        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
