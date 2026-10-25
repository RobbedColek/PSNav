using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using PSNav.Models.Patches;
using PSNav.Services.Consoles;

namespace PSNav.Services.Patches;

public sealed class PatchService : IPatchService
{
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

    public async Task<bool> UploadPatch(Patch patch)
    {
        const string apiUrl = "https://prosperopatches.com/api/internal/submitbyurl";

        HttpRequestMessage request = new(HttpMethod.Get, patch.ReferencePackageUrl);
        HttpResponseMessage response = await HttpClient.SendAsync(request);
        string responseBody = await response.Content.ReadAsStringAsync();
        string? jsonUrl = ReferencePackage.GetManifestUrl(responseBody);

        if (jsonUrl == null)
        {
            return false;
        }

        request = new HttpRequestMessage(HttpMethod.Post, apiUrl);

        string jsonPayload = JsonSerializer.Serialize(new
        {
            url = jsonUrl
        });

        using HttpContent content = new StringContent(
            jsonPayload,
            System.Text.Encoding.UTF8,
            "application/json");
        request.Content = content;

        response = await HttpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        JsonDocument jsonDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        if (jsonDocument.RootElement.GetProperty("success").GetBoolean()) return true;
        string? message = jsonDocument.RootElement.GetProperty("message").GetString();
        return message is "Game is already in the database." or "That patch is already in the database!";
    }
}