namespace PSNav.Models.Patches;

public sealed class Patch
{
    public required string EntitlementId { get; init; }
    public string? Name { get; set; }
    public byte[]? Image { get; init; }
    public string? ReferencePackageUrl { get; init; }
    public bool? IsSent { get; set; }
}