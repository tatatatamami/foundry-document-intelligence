using System.Text.Json;
using FoundryDocumentIntelligence.Domain.Workspaces;

namespace FoundryDocumentIntelligence.Infrastructure.Configuration;

public sealed record DemoWorkspaceSettings(
    Guid WorkspaceId,
    string CollectionType,
    ContentUnderstandingSettings ContentUnderstanding,
    AzureOpenAiSettings AiEnrichment,
    AzureOpenAiEmbeddingSettings Embedding,
    AzureSearchSettings Search,
    WorkspaceTaxonomy Taxonomy);

public sealed record ContentUnderstandingSettings(
    string Endpoint,
    string ApiVersion,
    string AnalyzerId,
    string CompletionModel);

public sealed record AzureOpenAiSettings(string Endpoint, string DeploymentName);

public sealed record AzureOpenAiEmbeddingSettings(
    string Endpoint,
    string DeploymentName,
    int Dimensions);

public sealed record AzureSearchSettings(
    string Endpoint,
    string ApiVersion,
    string IndexName);

public static class DemoWorkspaceSettingsLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<DemoWorkspaceSettings> LoadAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        JsonSerializer.Deserialize<DemoWorkspaceSettings>(
            await File.ReadAllTextAsync(path, cancellationToken),
            SerializerOptions)
        ?? throw new InvalidOperationException(
            $"Could not read workspace configuration '{path}'.");
}
