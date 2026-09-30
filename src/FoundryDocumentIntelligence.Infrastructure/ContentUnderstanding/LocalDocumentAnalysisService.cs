using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Identity;
using FoundryDocumentIntelligence.Application.Documents;

namespace FoundryDocumentIntelligence.Infrastructure.ContentUnderstanding;

public sealed class LocalDocumentAnalysisService : IDocumentAnalysisService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _httpClient = new();
    private readonly ContentUnderstandingOptions _options;
    private readonly ContentUnderstandingClient _client;
    private readonly ContentUnderstandingCanonicalMapper _mapper;

    public LocalDocumentAnalysisService(ContentUnderstandingOptions options)
    {
        _options = options;
        _client = new ContentUnderstandingClient(
            _httpClient,
            new DefaultAzureCredential(),
            options);
        _mapper = new ContentUnderstandingCanonicalMapper(options.Taxonomy);
    }

    public async Task<DocumentAnalysisResult> AnalyzeAsync(
        DocumentAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var rawResponse = await _client.AnalyzeAsync(request.SourceFilePath, cancellationToken);
        var rawDirectory = Path.Combine(_options.ArtifactRoot, "raw");
        var canonicalDirectory = Path.Combine(_options.ArtifactRoot, "canonical");
        Directory.CreateDirectory(rawDirectory);
        Directory.CreateDirectory(canonicalDirectory);

        var artifactName = request.DocumentId.Value.ToString("N");
        var rawPath = Path.Combine(rawDirectory, $"{artifactName}.json");
        await File.WriteAllBytesAsync(rawPath, rawResponse, cancellationToken);

        var document = _mapper.Map(rawResponse, request);
        var canonicalPath = Path.Combine(canonicalDirectory, $"{artifactName}.json");
        await using var canonicalStream = File.Create(canonicalPath);
        await JsonSerializer.SerializeAsync(
            canonicalStream,
            document,
            SerializerOptions,
            cancellationToken);

        return new DocumentAnalysisResult(document, rawPath, canonicalPath);
    }

    public void Dispose() => _httpClient.Dispose();
}
