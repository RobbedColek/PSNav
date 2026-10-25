namespace PSNav.Models.Consoles;

public sealed record ConsoleSettings(string Name, string? IpAddress, int FtpPort, string? Platform = null);
