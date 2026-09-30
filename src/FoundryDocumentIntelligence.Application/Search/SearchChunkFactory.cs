using System.Globalization;
using FoundryDocumentIntelligence.Domain.Documents;
using FoundryDocumentIntelligence.Domain.Search;

namespace FoundryDocumentIntelligence.Application.Search;

public sealed class SearchChunkFactory
{
    public IReadOnlyList<SearchChunk> Create(CanonicalDocument document)
    {
        var sourceFileName = GetString(document, "sourceFileName")
            ?? throw new InvalidOperationException("Canonical document has no sourceFileName metadata.");
        var collectionType = GetString(document, "collectionType")
            ?? throw new InvalidOperationException("Canonical document has no collectionType metadata.");
        var documentCategory = GetString(document, "documentCategory");
        var contentIndexes = GetStrings(document, "contentIndexes");
        var semanticTags = GetStrings(document, "semanticTags");
        var documentDate = ParseDate(GetString(document, "documentDate"));
        var developmentPhase = GetString(document, "developmentPhase");
        var summary = GetString(document, "summary");

        return document.Pages
            .Where(page => !string.IsNullOrWhiteSpace(page.Content))
            .Select(page => new SearchChunk(
                $"{document.DocumentId.Value:N}-page-{page.PageNumber:D4}",
                document.WorkspaceId,
                document.DocumentId,
                page.PageId,
                page.PageNumber,
                sourceFileName,
                collectionType,
                documentCategory,
                contentIndexes,
                semanticTags,
                documentDate,
                developmentPhase,
                summary,
                page.Content!,
                []))
            .ToArray();
    }

    private static string? GetString(CanonicalDocument document, string name) =>
        document.Metadata.TryGetValue(name, out var metadata)
            ? metadata.Value as string
            : null;

    private static IReadOnlyList<string> GetStrings(
        CanonicalDocument document,
        string name)
    {
        if (!document.Metadata.TryGetValue(name, out var metadata) ||
            metadata.Value is null)
        {
            return [];
        }

        return metadata.Value switch
        {
            IReadOnlyList<string> values => values,
            IEnumerable<object?> values => values
                .OfType<string>()
                .ToArray(),
            _ => throw new InvalidOperationException(
                $"Canonical metadata '{name}' is not a string collection.")
        };
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var date)
            ? date
            : throw new InvalidOperationException(
                $"Canonical documentDate '{value}' is not a valid date.");
    }
}
