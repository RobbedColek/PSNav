using System;
using System.IO;

namespace PSNav.Models.Consoles;

public static class ConsolePlatform
{
    public const string Ps4 = "PS4";
    public const string Ps5 = "PS5";

    public static string? FromDatabaseFileName(string? filePath) =>
        Path.GetFileName(filePath) switch
        {
            "entitlement4.db" => Ps5,
            "entitlement.db" => Ps4,
            _ => null
        };

    public static bool IsPs5(string? platform) => string.Equals(platform, Ps5, StringComparison.OrdinalIgnoreCase);
    public static bool IsPs4(string? platform) => string.Equals(platform, Ps4, StringComparison.OrdinalIgnoreCase);
}
