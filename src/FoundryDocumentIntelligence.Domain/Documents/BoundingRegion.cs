namespace FoundryDocumentIntelligence.Domain.Documents;

public sealed record BoundingRegion(IReadOnlyList<Point> Points);

public sealed record Point(double X, double Y);
