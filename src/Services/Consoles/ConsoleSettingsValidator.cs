using System.Globalization;
using System.Net;
using PSNav.Models.Consoles;

namespace PSNav.Services.Consoles;

public static class ConsoleSettingsValidator
{
    public static bool TryValidate(string? name, string? ipAddress, string? port, string? platform,
        bool requireIpAddress, out ConsoleSettings? settings, out string error)
    {
        int ftpPort = int.TryParse(port, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPort)
            ? parsedPort
            : 0;

        return TryValidate(name, ipAddress, ftpPort, platform, requireIpAddress, out settings, out error);
    }

    public static bool TryValidate(ConsoleSettings settings, bool requireIpAddress, out ConsoleSettings? validated,
        out string error)
    {
        return TryValidate(settings.Name, settings.IpAddress, settings.FtpPort, settings.Platform, requireIpAddress,
            out validated, out error);
    }

    private static bool TryValidate(string? name, string? ipAddress, int ftpPort, string? platform,
        bool requireIpAddress, out ConsoleSettings? settings, out string error)
    {
        settings = null;
        error = string.Empty;

        var trimmedName = name?.Trim();
        var trimmedIpAddress = ipAddress?.Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            error = "Please enter a console name.";
            return false;
        }

        if (string.IsNullOrEmpty(trimmedIpAddress))
        {
            if (requireIpAddress)
            {
                error = "Please enter the console's IP address.";
                return false;
            }

            trimmedIpAddress = null;
        }
        else if (!IPAddress.TryParse(trimmedIpAddress, out _))
        {
            error = "Please enter a valid IP address, not a hostname or URL.";
            return false;
        }

        if (ftpPort is < 1 or > 65535)
        {
            error = "Please enter an FTP port between 1 and 65535.";
            return false;
        }

        if (platform is not (null or ConsolePlatform.Ps4 or ConsolePlatform.Ps5))
        {
            error = "Please select a valid console platform.";
            return false;
        }

        settings = new ConsoleSettings(trimmedName, trimmedIpAddress, ftpPort, platform);
        return true;
    }
}
