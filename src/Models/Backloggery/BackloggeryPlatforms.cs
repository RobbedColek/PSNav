namespace PSNav.Models.Backloggery;

public static class BackloggeryPlatforms
{
    public const string Ps4 = "PS4";
    public const string Ps5 = "PS5";

    public const int Ps4PlatformId = 134;
    public const int Ps5PlatformId = 135;

    public const int DigitalFormatId = 1;
    public const int PhysicalFormatId = 20;
    public const int PhysicalGameOnlyFormatId = 21;

    public static int? GetPlatformId(string platform) => platform switch
    {
        Ps4 => Ps4PlatformId,
        Ps5 => Ps5PlatformId,
        _ => null
    };

    public static string? GetPlatform(int platformId) => platformId switch
    {
        Ps4PlatformId => Ps4,
        Ps5PlatformId => Ps5,
        _ => null
    };

    public static string GetPlatformTitle(string platform) => platform switch
    {
        Ps4 => "PlayStation 4",
        Ps5 => "PlayStation 5",
        _ => platform
    };

    public static string GetFormatTitle(int formatId) => formatId switch
    {
        DigitalFormatId => "Digital",
        PhysicalFormatId => "Physical",
        PhysicalGameOnlyFormatId => "Physical (Game Only)",
        _ => "Other"
    };
}
