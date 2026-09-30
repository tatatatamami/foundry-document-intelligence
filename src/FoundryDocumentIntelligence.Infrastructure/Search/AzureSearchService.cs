using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using FoundryDocumentIntelligence.Application.Search;
using FoundryDocumentIntelligence.Domain.Documents;
using FoundryDocumentIntelligence.Domain.Search;

namespace FoundryDocumentIntelligence.Infrastructure.Search;

public sealed class AzureSearchService(
    HttpClient httpClient,
    TokenCredential credential,
    AzureSearchOptions options,
    IEmbeddingService embeddingService) : ISearchIndexService, IHybridSearchService
{
    private static readonly TokenRequestContext TokenContext =
        new(["https://search.azure.com/.default"]);

    public async Task EnsureIndexAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(
            HttpMethod.Put,
            $"indexes/{Uri.EscapeDataString(options.IndexName)}",
            CreateIndexDefinition(),
            cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "Search index creation", cancellationToken);
    }

    public async Task IndexAsync(
        IReadOnlyList<SearchChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        var documents = chunks.Select(chunk =>
            new Dictionary<string, object?>
            {
                ["@search.action"] = "mergeOrUpload",
                ["chunkId"] = chunk.ChunkId,
                ["workspaceId"] = chunk.WorkspaceId.Value.ToString("D"),
                ["documentId"] = chunk.DocumentId.Value.ToString("D"),
                ["pageId"] = chunk.PageId.Value,
                ["pageNumber"] = chunk.PageNumber,
                ["sourceFileName"] = chunk.SourceFileName,
                ["collectionType"] = chunk.CollectionType,
                ["documentCategory"] = chunk.DocumentCategory,
                ["contentIndexes"] = chunk.ContentIndexes,
                ["semanticTags"] = chunk.SemanticTags,
                ["documentDate"] = chunk.DocumentDate,
                ["developmentPhase"] = chunk.DevelopmentPhase,
                ["summary"] = chunk.Summary,
                ["content"] = chunk.Content,
                ["contentVector"] = chunk.ContentVector
            });
        using var request = await CreateRequestAsync(
            HttpMethod.Post,
            $"indexes/{Uri.EscapeDataString(options.IndexName)}/docs/index",
            new { value = documents },
            cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "Search document indexing", cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
        DocumentSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.Text))
        {
            return [];
        }

        var vector = await embeddingService.GenerateAsync(query.Text, cancellationToken);
        var body = new
        {
            search = query.Text,
            filter = BuildFilter(query),
            top = query.Size,
            select = "chunkId,documentId,pageId,pageNumber,sourceFileName,documentCategory,contentIndexes,semanticTags,documentDate,developmentPhase,summary,content",
            highlight = "content",
            vectorQueries = new[]
            {
                new
                {
                    kind = "vector",
                    vector,
                    fields = "contentVector",
                    k = Math.Max(query.Size, 10)
                }
            }
        };
        using var request = await CreateRequestAsync(
            HttpMethod.Post,
            $"indexes/{Uri.EscapeDataString(options.IndexName)}/docs/search",
            body,
            cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var bytes = await EnsureSuccessAsync(response, "Hybrid search", cancellationToken);
        using var json = JsonDocument.Parse(bytes);
        return json.RootElement.GetProperty("value")
            .EnumerateArray()
            .Select(MapResult)
            .ToArray();
    }

    public static string BuildFilter(DocumentSearchQuery query)
    {
        var filters = new List<string>
        {
            $"workspaceId eq '{EscapeFilter(query.WorkspaceId.Value.ToString("D"))}'"
        };
        AddScalarFilter(filters, "documentCategory", query.DocumentCategory);
        AddCollectionFilter(filters, "contentIndexes", query.ContentIndex);
        AddCollectionFilter(filters, "semanticTags", query.SemanticTag);
        return string.Join(" and ", filters);
    }

    private object CreateIndexDefinition() => new
    {
        name = options.IndexName,
        fields = new object[]
        {
            Field("chunkId", "Edm.String", key: true, filterable: true),
            Field("workspaceId", "Edm.String", filterable: true, facetable: true),
            Field("documentId", "Edm.String", filterable: true),
            Field("pageId", "Edm.String", filterable: true),
            Field("pageNumber", "Edm.Int32", filterable: true),
            Field("sourceFileName", "Edm.String", searchable: true, filterable: true),
            Field("collectionType", "Edm.String", filterable: true, facetable: true),
            Field("documentCategory", "Edm.String", filterable: true, facetable: true),
            Field("contentIndexes", "Collection(Edm.String)", filterable: true, facetable: true),
            Field("semanticTags", "Collection(Edm.String)", filterable: true, facetable: true),
            Field("documentDate", "Edm.DateTimeOffset", filterable: true, facetable: true),
            Field("developmentPhase", "Edm.String", filterable: true, facetable: true),
            Field("summary", "Edm.String", searchable: true),
            Field("content", "Edm.String", searchable: true),
            new
            {
                name = "contentVector",
                type = "Collection(Edm.Single)",
                searchable = true,
                retrievable = false,
                dimensions = options.VectorDimensions,
                vectorSearchProfile = "default-vector-profile"
            }
        },
        vectorSearch = new
        {
            algorithms = new[] { new { name = "default-hnsw", kind = "hnsw" } },
            profiles = new[]
            {
                new
                {
                    name = "default-vector-profile",
                    algorithm = "default-hnsw"
                }
            }
        }
    };

    private static object Field(
        string name,
        string type,
        bool key = false,
        bool searchable = false,
        bool filterable = false,
        bool facetable = false) =>
        new
        {
            name,
            type,
            key,
            searchable,
            filterable,
            facetable,
            retrievable = true
        };

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string relativePath,
        object body,
        CancellationToken cancellationToken)
    {
        var separator = relativePath.Contains('?') ? '&' : '?';
        var request = new HttpRequestMessage(
            method,
            new Uri(
                options.Endpoint,
                $"{relativePath}{separator}api-version={Uri.EscapeDataString(options.ApiVersion)}"))
        {
            Content = JsonContent.Create(body)
        };
        var token = await credential.GetTokenAsync(TokenContext, cancellationToken);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        return request;
    }

    private static async Task<byte[]> EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"{operation} failed with HTTP {(int)response.StatusCode}: " +
                System.Text.Encoding.UTF8.GetString(bytes),
                null,
                response.StatusCode);
        }

        return bytes;
    }

    private static DocumentSearchResult MapResult(JsonElement result)
    {
        var content = result.GetProperty("content").GetString() ?? string.Empty;
        var snippet = ReadHighlight(result) ?? CreateSnippet(content);
        return new DocumentSearchResult(
            result.GetProperty("chunkId").GetString()!,
            new DocumentId(Guid.Parse(result.GetProperty("documentId").GetString()!)),
            new PageId(result.GetProperty("pageId").GetString()!),
            result.GetProperty("pageNumber").GetInt32(),
            result.GetProperty("sourceFileName").GetString()!,
            GetString(result, "documentCategory"),
            GetStrings(result, "contentIndexes"),
            GetStrings(result, "semanticTags"),
            ReadDate(result),
            GetString(result, "developmentPhase"),
            GetString(result, "summary"),
            snippet,
            result.GetProperty("@search.score").GetDouble());
    }

    private static string? ReadHighlight(JsonElement result)
    {
        if (!result.TryGetProperty("@search.highlights", out var highlights) ||
            !highlights.TryGetProperty("content", out var content) ||
            content.GetArrayLength() == 0)
        {
            return null;
        }

        return string.Join(
            " … ",
            content.EnumerateArray()
                .Select(value => value.GetString())
                .Where(value => value is not null)
                .Select(value => value!
                    .Replace("<em>", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("</em>", string.Empty, StringComparison.OrdinalIgnoreCase)));
    }

    private static string CreateSnippet(string content) =>
        content.Length <= 280 ? content : $"{content[..280]}…";

    private static string? GetString(JsonElement result, string name) =>
        result.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string> GetStrings(JsonElement result, string name) =>
        result.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Select(item => item.GetString())
                .Where(item => item is not null)
                .Select(item => item!)
                .ToArray()
            : [];

    private static DateTimeOffset? ReadDate(JsonElement result) =>
        result.TryGetProperty("documentDate", out var value) &&
        value.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(value.GetString(), out var date)
            ? date
            : null;

    private static void AddScalarFilter(
        List<string> filters,
        string field,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add($"{field} eq '{EscapeFilter(value)}'");
        }
    }

    private static void AddCollectionFilter(
        List<string> filters,
        string field,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add($"{field}/any(item: item eq '{EscapeFilter(value)}')");
        }
    }

    private static string EscapeFilter(string value) => value.Replace("'", "''");
}
