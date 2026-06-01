namespace PSNav.Models.Entitlements;

public sealed class Entitlement
{
    public required string EntitlementId { get; init; }
    public string? TitleId { get; set; }
    public string? ProductId { get; set; }
    public string? Name { get; set; }
    public byte[]? Image { get; set; }
    public string? ImageUrl { get; init; }
    public string? Platform { get; set; }
    public string? ReferencePackageUrl { get; set; }
    public long? PackageFileSize { get; init; }
    public int? EntitlementType { get; init; }
    public int? FeatureType { get; init; }
    public string? PackageType { get; init; }
    public string? Type { get; init; }
    public string? Membership { get; init; }
    public bool? IsActive { get; set; }
    public string? OwnerOnlineId { get; set; }

    // Display strings
    public string? TypeDisplayString { get; set; }
    public string? RetentionPolicyDisplayString { get; set; }
    public string? FeatureTypeDisplayString { get; set; }
    public string? MembershipDisplayString { get; set; }

    public bool HasReferencePackageUrl => !string.IsNullOrWhiteSpace(ReferencePackageUrl);
}