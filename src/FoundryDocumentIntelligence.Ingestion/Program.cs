using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Azure.Identity;
using FoundryDocumentIntelligence.Application.Search;
using FoundryDocumentIntelligence.Application.Documents;
using FoundryDocumentIntelligence.Domain.Documents;
using FoundryDocumentIntelligence.Domain.Workspaces;
using FoundryDocumentIntelligence.Infrastructure.Artifacts;
using FoundryDocumentIntelligence.Infrastructure.Configuration;
using FoundryDocumentIntelligence.Infrastructure.ContentUnderstanding;
using FoundryDocumentIntelligence.Infrastructure.OpenAI;
using FoundryDocumentIntelligence.Infrastructure.Search;

var configPath = GetOptionalArgument(args, "--config") ?? Path.Combine("config", "demo-workspace.json");

var settings = await DemoWorkspaceSettingsLoader.LoadAsync(configPath);

var analyzerDefinitionPath = GetOptionalArgument(args, "--write-analyzer");
if (analyzerDefinitionPath is not null)
{
    var definition = new ContentUnderstandingAnalyzerDefinitionBuilder()
        .Build(settings.ContentUnderstanding.CompletionModel, settings.Taxonomy);
    var definitionDirectory = Path.GetDirectoryName(Path.GetFullPath(analyzerDefinitionPath));
    Directory.CreateDirectory(definitionDirectory!);
    await File.WriteAllTextAsync(
        analyzerDefinitionPath,
        definition.ToJsonString(new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        }));
    Console.WriteLine($"Analyzer definition: {Path.GetFullPath(analyzerDefinitionPath)}");
}

var sourcePath = GetOptionalArgument(args, "--source");
if (sourcePath is null)
{
    if (analyzerDefinitionPath is not null)
    {
        return;
    }

    throw new ArgumentException("Required argument '--source' was not provided.");
}

var artifactRoot = GetOptionalArgument(args, "--artifacts") ?? Path.Combine("data", "demo");
if (!File.Exists(sourcePath))
{
    throw new FileNotFoundException("The source document was not found.", sourcePath);
}

var workspaceId = new WorkspaceId(settings.WorkspaceId);
var relativeSourcePath = Path.GetRelativePath(Environment.CurrentDirectory, sourcePath);
var documentId = CreateStableDocumentId(settings.WorkspaceId, relativeSourcePath);
var request = new DocumentAnalysisRequest(
    workspaceId,
    documentId,
    Path.GetFullPath(sourcePath),
    relativeSourcePath,
    Path.GetFileName(sourcePath),
    settings.CollectionType);
var options = new ContentUnderstandingOptions(
    new Uri(settings.ContentUnderstanding.Endpoint),
    settings.ContentUnderstanding.ApiVersion,
    settings.ContentUnderstanding.AnalyzerId,
    artifactRoot,
    settings.Taxonomy);

using var service = new LocalDocumentAnalysisService(options);
var result = await service.AnalyzeAsync(request);
using var enrichmentHttpClient = new HttpClient();
using var embeddingHttpClient = new HttpClient();
using var searchHttpClient = new HttpClient();
var credential = new DefaultAzureCredential();
var enrichmentService = new AzureOpenAiDocumentEnrichmentService(
    enrichmentHttpClient,
    credential,
    new AzureOpenAiOptions(
        new Uri(settings.AiEnrichment.Endpoint),
        settings.AiEnrichment.DeploymentName));
var enrichedDocument = await enrichmentService.EnrichAsync(
    result.Document,
    settings.Taxonomy);
var artifactWriter = new LocalPipelineArtifactWriter();
var enrichedPath = await artifactWriter.WriteEnrichedDocumentAsync(
    artifactRoot,
    enrichedDocument);

var embeddingService = new AzureOpenAiEmbeddingService(
    embeddingHttpClient,
    credential,
    new AzureOpenAiEmbeddingOptions(
        new Uri(settings.Embedding.Endpoint),
        settings.Embedding.DeploymentName,
        settings.Embedding.Dimensions));
var chunkFactory = new SearchChunkFactory();
var chunks = new List<FoundryDocumentIntelligence.Domain.Search.SearchChunk>();
foreach (var chunk in chunkFactory.Create(enrichedDocument))
{
    var vector = await embeddingService.GenerateAsync(chunk.Content);
    chunks.Add(chunk with { ContentVector = vector });
}

var derivedPath = await artifactWriter.WriteChunksAsync(
    artifactRoot,
    enrichedDocument.DocumentId,
    chunks);
var searchService = new AzureSearchService(
    searchHttpClient,
    credential,
    new AzureSearchOptions(
        new Uri(settings.Search.Endpoint),
        settings.Search.ApiVersion,
        settings.Search.IndexName,
        settings.Embedding.Dimensions),
    embeddingService);
await searchService.EnsureIndexAsync();
await searchService.IndexAsync(chunks);

Console.WriteLine($"DocumentId: {result.Document.DocumentId.Value}");
Console.WriteLine($"Raw result: {Path.GetFullPath(result.RawArtifactPath)}");
Console.WriteLine($"Canonical document: {Path.GetFullPath(result.CanonicalArtifactPath)}");
Console.WriteLine($"Enriched document: {Path.GetFullPath(enrichedPath)}");
Console.WriteLine($"Search chunks: {Path.GetFullPath(derivedPath)}");
Console.WriteLine($"Indexed chunks: {chunks.Count}");

static string? GetOptionalArgument(string[] arguments, string name)
{
    var index = Array.IndexOf(arguments, name);
    return index >= 0 && index + 1 < arguments.Length
        ? arguments[index + 1]
        : null;
}

static DocumentId CreateStableDocumentId(Guid workspaceId, string sourcePath)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{workspaceId:N}:{sourcePath}"));
    return new DocumentId(new Guid(hash.AsSpan(0, 16)));
}
