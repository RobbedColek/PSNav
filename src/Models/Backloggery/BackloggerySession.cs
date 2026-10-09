using System;
using System.Text.Json.Serialization;

namespace PSNav.Models.Backloggery;

public sealed record BackloggerySession(
    string Username,
    long UserId,
    string LogToken,
    DateTimeOffset? ExpiresAt)
{
    [JsonIgnore] public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.Now;
}
