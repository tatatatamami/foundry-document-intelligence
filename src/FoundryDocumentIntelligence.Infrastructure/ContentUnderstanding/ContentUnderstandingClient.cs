using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;

namespace FoundryDocumentIntelligence.Infrastructure.ContentUnderstanding;

public sealed class ContentUnderstandingClient(
    HttpClient httpClient,
    TokenCredential credential,
    ContentUnderstandingOptions options)
{
    private static readonly string[] TerminalStatuses = ["Succeeded", "Failed", "Canceled"];
    private readonly TokenRequestContext _tokenContext =
        new(["https://cognitiveservices.azure.com/.default"]);

    public async Task<byte[]> AnalyzeAsync(
        string sourceFilePath,
        CancellationToken cancellationToken)
    {
        var requestUri = new Uri(
            options.Endpoint,
            $"contentunderstanding/analyzers/{Uri.EscapeDataString(options.AnalyzerId)}:analyze?api-version={Uri.EscapeDataString(options.ApiVersion)}");
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        await AuthorizeAsync(request, cancellationToken);
        var data = await File.ReadAllBytesAsync(sourceFilePath, cancellationToken);
        request.Content = JsonContent.Create(new
        {
            inputs = new[]
            {
                new
                {
                    data = Convert.ToBase64String(data),
                    mimeType = GetContentType(sourceFilePath),
                    name = Path.GetFileName(sourceFilePath)
                }
            }
        });

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var responseBody = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Content Understanding analyze request failed with HTTP {(int)response.StatusCode}: {System.Text.Encoding.UTF8.GetString(responseBody)}",
                null,
                response.StatusCode);
        }

        if (response.StatusCode != System.Net.HttpStatusCode.Accepted ||
            !response.Headers.TryGetValues("Operation-Location", out var operationLocations) ||
            !Uri.TryCreate(operationLocations.SingleOrDefault(), UriKind.Absolute, out var operationUri))
        {
            throw new InvalidOperationException(
                "Content Understanding did not return an analysis operation location.");
        }

        return await PollOperationAsync(operationUri, cancellationToken);
    }

    private async Task<byte[]> PollOperationAsync(
        Uri operationUri,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, operationUri);
            await AuthorizeAsync(request, cancellationToken);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Content Understanding operation failed with HTTP {(int)response.StatusCode}: {System.Text.Encoding.UTF8.GetString(body)}",
                    null,
                    response.StatusCode);
            }

            using var json = JsonDocument.Parse(body);
            var status = json.RootElement.GetProperty("status").GetString()
                ?? throw new InvalidOperationException("Analysis operation response did not contain a status.");
            if (TerminalStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            {
                if (!status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Content Understanding analysis ended with status '{status}': {System.Text.Encoding.UTF8.GetString(body)}");
                }

                return body;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    private async Task AuthorizeAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(_tokenContext, cancellationToken);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
    }

    private static string GetContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            _ => throw new NotSupportedException(
                $"Unsupported document extension '{Path.GetExtension(path)}'.")
        };
}
