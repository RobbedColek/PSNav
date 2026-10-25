using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PSNav.Models.Consoles;
using PSNav.Models.Entitlements;

namespace PSNav.Services.Consoles;

public sealed class ConsoleInstallService(IPackageManifestService packageManifestService) : IConsoleInstallService
{
    private const int DpiPort = 12800;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = RequestTimeout
    };

    public async Task SendToConsoleAsync(Entitlement entitlement, ConsoleModel console,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(console.IpAddress))
            throw new InvalidOperationException($"Console \"{console.Name}\" has no IP address set.");

        string packageUrl = await packageManifestService.GetManifestUrlAsync(entitlement, cancellationToken);

        string endpoint = new UriBuilder(Uri.UriSchemeHttp, console.IpAddress, DpiPort, "upload").Uri.ToString();
        string response;

        try
        {
            StringContent urlContent = new(packageUrl);
            urlContent.Headers.ContentType = null;
            using MultipartFormDataContent content = new();
            content.Add(urlContent, "\"url\"");

            using HttpResponseMessage message = await HttpClient.PostAsync(endpoint, content, cancellationToken);
            response = (await message.Content.ReadAsStringAsync(cancellationToken)).Trim();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"{DpiRequirement(console)} The console did not respond within {RequestTimeout.TotalSeconds:0} seconds.");
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"{DpiRequirement(console)}" + System.Environment.NewLine + "{exception.Message}", exception);
        }

        if (!response.Contains("SUCCESS:", StringComparison.Ordinal))
        {
            string detail = string.IsNullOrEmpty(response) ? "The console returned an empty response." : response;
            throw new InvalidOperationException($"{DpiRequirement(console)} The console responded: {detail}");
        }
    }

    private static string DpiRequirement(ConsoleModel console) =>
        $"Could not send the package to \"{console.Name}\" ({console.IpAddress}). " + System.Environment.NewLine +
        "Make sure the console is on and reachable and that DPIv2 API is running.";
}
