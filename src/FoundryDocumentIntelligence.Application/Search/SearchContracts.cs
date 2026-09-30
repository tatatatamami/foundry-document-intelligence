using FoundryDocumentIntelligence.Domain.Search;

namespace FoundryDocumentIntelligence.Application.Search;

public interface IEmbeddingService
{
    Task<IReadOnlyList<float>> GenerateAsync(
        string input,
        CancellationToken cancellationToken = default);
}

public interface ISearchIndexService
{
    Task EnsureIndexAsync(CancellationToken cancellationToken = default);

    Task IndexAsync(
        IReadOnlyList<SearchChunk> chunks,
        CancellationToken cancellationToken = default);
}

public interface IHybridSearchService
{
    Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
        DocumentSearchQuery query,
        CancellationToken cancellationToken = default);
}
