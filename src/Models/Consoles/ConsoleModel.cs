using System;

namespace PSNav.Models.Consoles;

public sealed class ConsoleModel
{
    public int? Id { get; set; }
    public required string Name { get; set; }
    public string? IpAddress { get; set; }
    public int FtpPort { get; set; } = 2121;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}