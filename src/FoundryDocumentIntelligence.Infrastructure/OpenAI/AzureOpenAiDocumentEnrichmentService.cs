using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using FoundryDocumentIntelligence.Application.Documents;
using FoundryDocumentIntelligence.Domain.Documents;
using FoundryDocumentIntelligence.Domain.Workspaces;

namespace FoundryDocumentIntelligence.Infrastructure.OpenAI;

public sealed class AzureOpenAiDocumentEnrichmentService(
    HttpClient httpClient,
    TokenCredential credential,
    AzureOpenAiOptions options) : IDocumentEnrichmentService
{
    private static readonly TokenRequestContext TokenContext =
        new(["https://ai.azure.com/.default"]);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly DocumentEnrichmentRequestBuilder _requestBuilder = new();

    public async Task<CanonicalDocument> EnrichAsync(
        CanonicalDocument document,
        WorkspaceTaxonomy taxonomy,
        CancellationToken cancellationToken = default)
    {
        var requestBody = _requestBuilder.Build(
            document,
            taxonomy,
            options.DeploymentName);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(options.Endpoint, "openai/v1/chat/completions"))
        {
            Content = JsonContent.Create(requestBody)
        };
        await AuthorizeAsync(request, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"AI enrichment failed with HTTP {(int)response.StatusCode}: " +
                System.Text.Encoding.UTF8.GetString(responseBytes),
                null,
                response.StatusCode);
        }

        using var responseJson = JsonDocument.Parse(responseBytes);
        var message = responseJson.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message");
        if (message.TryGetProperty("refusal", out var refusal) &&
            refusal.ValueKind == JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"AI enrichment was refused: {refusal.GetString()}");
        }

        var content = message.GetProperty("content").GetString()
            ?? throw new InvalidOperationException(
                "AI enrichment response did not contain structured content.");
        var enrichment = JsonSerializer.Deserialize<EnrichmentResponse>(
            content,
            SerializerOptions)
            ?? throw new InvalidOperationException(
                "AI enrichment structured content could not be parsed.");
        Validate(enrichment, taxonomy);

        var metadata = new Dictionary<string, CanonicalMetadataValue>(
            document.Metadata,
            StringComparer.Ordinal)
        {
            ["documentCategory"] = Inferred(enrichment.DocumentCategory),
            ["contentIndexes"] = Inferred(enrichment.ContentIndexes),
            ["semanticTags"] = Inferred(enrichment.SemanticTags),
            ["summary"] = new CanonicalMetadataValue(
                enrichment.Summary,
                InformationOrigin.Generated,
                null,
                []),
            ["developmentPhase"] = Inferred(enrichment.DevelopmentPhase)
        };
        return document with { Metadata = metadata };
    }

    private async Task AuthorizeAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(TokenContext, cancellationToken);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
    }

    private static CanonicalMetadataValue Inferred(object value) =>
        new(value, InformationOrigin.Inferred, null, []);

    private static void Validate(
        EnrichmentResponse enrichment,
        WorkspaceTaxonomy taxonomy)
    {
        ValidateSingle(
            enrichment.DocumentCategory,
            taxonomy.DocumentCategories,
            "documentCategory");
        ValidateMany(
            enrichment.ContentIndexes,
            taxonomy.ContentIndexes,
            "contentIndexes");
        ValidateMany(
            enrichment.SemanticTags,
            taxonomy.SemanticTags,
            "semanticTags");
        ValidateSingle(
            enrichment.DevelopmentPhase,
            taxonomy.DevelopmentPhases,
            "developmentPhase");
        if (string.IsNullOrWhiteSpace(enrichment.Summary))
        {
            throw new InvalidOperationException("AI enrichment returned an empty summary.");
        }
    }

    private static void ValidateSingle(
        string value,
        TaxonomyDefinition taxonomy,
        string fieldName)
    {
        if (!taxonomy.Options.Any(option =>
                string.Equals(option.Value, value, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"AI enrichment returned unconfigured {fieldName} value '{value}'.");
        }
    }

    private static void ValidateMany(
        IReadOnlyList<string> values,
        TaxonomyDefinition taxonomy,
        string fieldName)
    {
        var allowed = taxonomy.Options
            .Select(option => option.Value)
            .ToHashSet(StringComparer.Ordinal);
        var invalid = values.FirstOrDefault(value => !allowed.Contains(value));
        if (invalid is not null)
        {
            throw new InvalidOperationException(
                $"AI enrichment returned unconfigured {fieldName} value '{invalid}'.");
        }
    }

    private sealed record EnrichmentResponse(
        string DocumentCategory,
        string[] ContentIndexes,
        string[] SemanticTags,
        string Summary,
        string DevelopmentPhase);
}
