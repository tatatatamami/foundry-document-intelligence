using FoundryDocumentIntelligence.Domain.Workspaces;

namespace FoundryDocumentIntelligence.Domain.Documents;

public sealed record CanonicalDocument(
    DocumentId DocumentId,
    WorkspaceId WorkspaceId,
    IReadOnlyList<CanonicalPage> Pages,
    IReadOnlyDictionary<string, CanonicalMetadataValue> Metadata,
    IReadOnlyList<CanonicalEntity> Entities);

public sealed record CanonicalMetadataValue(
    object? Value,
    InformationOrigin Origin,
    double? Confidence,
    IReadOnlyList<Evidence> Evidence);

public sealed record CanonicalPage(PageId PageId, int PageNumber, string? Content)
{
    public IReadOnlyList<CanonicalTable> Tables { get; init; } = [];

    public IReadOnlyList<CanonicalFigure> Figures { get; init; } = [];

    public IReadOnlyList<CanonicalRegion> Regions { get; init; } = [];

    public IReadOnlyList<Evidence> Evidence { get; init; } = [];
}

public sealed record CanonicalTable(
    string? Caption,
    string Content,
    IReadOnlyList<Evidence> Evidence);

public sealed record CanonicalFigure(
    string? Caption,
    string? Description,
    IReadOnlyList<Evidence> Evidence);

public sealed record CanonicalRegion(
    string? Content,
    BoundingRegion? BoundingRegion,
    IReadOnlyList<Evidence> Evidence);

public sealed record CanonicalEntity(
    string Type,
    string Value,
    InformationOrigin Origin,
    IReadOnlyList<Evidence> Evidence);
