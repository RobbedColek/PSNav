namespace PSNav.Models.Backloggery;

public sealed record BackloggerySyncRecord(
    string EntitlementId,
    BackloggerySyncStatus Status,
    long? BackloggeryGameId,
    string? Platform);
