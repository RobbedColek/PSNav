using System;
using System.Linq;
using System.Xml.Linq;

namespace PSNav.Services.Consoles;

public static class ReferencePackage
{
    public static bool IsXmlUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            ? uri.AbsolutePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            : url.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    public static string? GetManifestUrl(string xml) =>
        XDocument.Parse(xml)
            .Descendants("package")
            .FirstOrDefault()
            ?.Attribute("manifest_url")
            ?.Value;

    // A version XML lists every package of a title (the game and its add-ons), each under a tag whose content_id
    // matches the entitlement ID of the content it installs
    public static string? GetManifestUrl(string xml, string contentId) =>
        XDocument.Parse(xml)
            .Descendants()
            .Where(tag => string.Equals(tag.Attribute("content_id")?.Value, contentId, StringComparison.OrdinalIgnoreCase))
            .Select(tag => tag.Element("package")?.Attribute("manifest_url")?.Value)
            .FirstOrDefault(url => !string.IsNullOrEmpty(url));

    public static bool IsLicenseOnly(string? entitlementType) => entitlementType is "PSAL" or "PS4AL";
}
