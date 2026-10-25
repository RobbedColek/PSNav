using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using PSNav.Models.Entitlements;

namespace PSNav.Services.Consoles;

public sealed class PackageManifestService : IPackageManifestService
{
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<string> GetManifestUrlAsync(Entitlement entitlement,
        CancellationToken cancellationToken = default)
    {
        string? referencePackageUrl = entitlement.ReferencePackageUrl;
        ArgumentException.ThrowIfNullOrWhiteSpace(referencePackageUrl);

        if (ReferencePackage.IsLicenseOnly(entitlement.Type))
            throw new InvalidOperationException(
                "This add-on is a license that unlocks content already included in the game. There is no package to download.");

        if (!ReferencePackage.IsXmlUrl(referencePackageUrl))
            return referencePackageUrl;

        try
        {
            string xml = await HttpClient.GetStringAsync(referencePackageUrl, cancellationToken);
            return ReferencePackage.GetManifestUrl(xml, entitlement.EntitlementId) ??
                   throw new InvalidOperationException(
                       $"The XML does not contain a package for {entitlement.EntitlementId}.");
        }
        catch (Exception exception) when (IsFetchFailure(exception, cancellationToken) ||
                                          exception is XmlException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Could not get the package manifest from {referencePackageUrl}. {exception.Message}", exception);
        }
    }

    public async Task<IReadOnlyList<string>> GetPackageUrlsAsync(Entitlement entitlement,
        CancellationToken cancellationToken = default)
    {
        string manifestUrl = await GetManifestUrlAsync(entitlement, cancellationToken);

        try
        {
            await using var stream = await HttpClient.GetStreamAsync(manifestUrl, cancellationToken);
            using JsonDocument manifest = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var urls = manifest.RootElement.GetProperty("pieces").EnumerateArray()
                .OrderBy(piece => piece.TryGetProperty("fileOffset", out JsonElement offset) ? offset.GetInt64() : 0)
                .Select(piece => piece.GetProperty("url").GetString())
                .OfType<string>()
                .ToList();

            return urls.Count > 0
                ? urls
                : throw new InvalidOperationException("The manifest does not list any package pieces.");
        }
        catch (Exception exception) when (IsFetchFailure(exception, cancellationToken) ||
                                          exception is JsonException or KeyNotFoundException
                                              or InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Could not read the package manifest from {manifestUrl}. {exception.Message}", exception);
        }
    }

    private static bool IsFetchFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException ||
        exception is TaskCanceledException && !cancellationToken.IsCancellationRequested;
}
