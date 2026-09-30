using FoundryDocumentIntelligence.Domain.Documents;
using FoundryDocumentIntelligence.Domain.Workspaces;

namespace FoundryDocumentIntelligence.Domain.Search;

public sealed record SearchChunk(
    string ChunkId,
    WorkspaceId WorkspaceId,
    DocumentId DocumentId,
    PageId PageId,
    int PageNumber,
    string SourceFileName,
    string CollectionType,
    string? DocumentCategory,
    IReadOnlyList<string> ContentIndexes,
    IReadOnlyList<string> SemanticTags,
    DateTimeOffset? DocumentDate,
    string? DevelopmentPhase,
    string? Summary,
    string Content,
    IReadOnlyList<float> ContentVector);

public sealed record DocumentSearchQuery(
    WorkspaceId WorkspaceId,
    string Text,
    string? DocumentCategory,
    string? ContentIndex,
    string? SemanticTag,
    int Size = 10);

public sealed record DocumentSearchResult(
    string ChunkId,
    DocumentId DocumentId,
    PageId PageId,
    int PageNumber,
    string SourceFileName,
    string? DocumentCategory,
    IReadOnlyList<string> ContentIndexes,
    IReadOnlyList<string> SemanticTags,
    DateTimeOffset? DocumentDate,
    string? DevelopmentPhase,
    string? Summary,
    string Snippet,
    double Score);
