using System;

namespace PSNav.Models.Consoles;

public sealed class ConsoleModel
{
    public int? Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}