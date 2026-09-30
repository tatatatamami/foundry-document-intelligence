namespace FoundryDocumentIntelligence.Domain.Documents;

public sealed record Evidence(
    DocumentId SourceDocumentId,
    int PageNumber,
    BoundingRegion? BoundingRegion,
    string? ExtractedValue,
    double? Confidence,
    InformationOrigin Origin);
