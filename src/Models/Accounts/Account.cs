using System;

namespace PSNav.Models.Accounts;

public sealed class Account
{
    public required string AccountId { get; init; }
    public string? OnlineId { get; set; }
    public byte[]? ProfilePicture { get; init; }
    public byte[]? AvatarPicture { get; init; }
    public string? NpssoToken { get; set; }
    public DateTimeOffset? NpssoTokenExpiration { get; set; }
    public DateTimeOffset? LastSynchronization { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Not saved in the database
    public string? AccessToken { get; set; }
    public int EntitlementCount { get; set; }

    public byte[]? DisplayPicture => ProfilePicture is { Length: > 0 }
        ? ProfilePicture
        : AvatarPicture;

    public bool HasNpssoToken => !string.IsNullOrWhiteSpace(NpssoToken);
    public bool IsTokenExpired => NpssoTokenExpiration.HasValue && NpssoTokenExpiration.Value < DateTimeOffset.Now;
}