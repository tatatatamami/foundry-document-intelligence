using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using FoundryDocumentIntelligence.Application.Search;

namespace FoundryDocumentIntelligence.Infrastructure.OpenAI;

public sealed class AzureOpenAiEmbeddingService(
    HttpClient httpClient,
    TokenCredential credential,
    AzureOpenAiEmbeddingOptions options) : IEmbeddingService
{
    private static readonly TokenRequestContext TokenContext =
        new(["https://ai.azure.com/.default"]);

    public async Task<IReadOnlyList<float>> GenerateAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Embedding input cannot be empty.", nameof(input));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(options.Endpoint, "openai/v1/embeddings"))
        {
            Content = JsonContent.Create(new
            {
                model = options.DeploymentName,
                input,
                dimensions = options.Dimensions
            })
        };
        var token = await credential.GetTokenAsync(TokenContext, cancellationToken);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Embedding generation failed with HTTP {(int)response.StatusCode}: " +
                System.Text.Encoding.UTF8.GetString(responseBytes),
                null,
                response.StatusCode);
        }

        using var json = JsonDocument.Parse(responseBytes);
        var vector = json.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(value => value.GetSingle())
            .ToArray();
        if (vector.Length != options.Dimensions)
        {
            throw new InvalidOperationException(
                $"Embedding dimension was {vector.Length}; expected {options.Dimensions}.");
        }

        return vector;
    }
}
