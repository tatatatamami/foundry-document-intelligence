using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FoundryDocumentIntelligence.Application.Documents;
using FoundryDocumentIntelligence.Domain.Documents;
using FoundryDocumentIntelligence.Domain.Workspaces;

namespace FoundryDocumentIntelligence.Infrastructure.ContentUnderstanding;

public sealed partial class ContentUnderstandingCanonicalMapper
{
    private static readonly IReadOnlyDictionary<string, InformationOrigin> FieldOrigins =
        new Dictionary<string, InformationOrigin>(StringComparer.Ordinal)
        {
            ["documentDate"] = InformationOrigin.Extracted,
            ["documentCategory"] = InformationOrigin.Inferred,
            ["contentIndexes"] = InformationOrigin.Inferred,
            ["semanticTags"] = InformationOrigin.Inferred,
            ["summary"] = InformationOrigin.Generated,
            ["developmentPhase"] = InformationOrigin.Inferred
        };
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>
        _taxonomyValues;

    public ContentUnderstandingCanonicalMapper(WorkspaceTaxonomy? taxonomy = null)
    {
        _taxonomyValues = taxonomy is null
            ? new Dictionary<string, IReadOnlyDictionary<string, string>>()
            : new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["contentIndexes"] = ToProviderValueMap(taxonomy.ContentIndexes),
                ["semanticTags"] = ToProviderValueMap(taxonomy.SemanticTags)
            };
    }

    public CanonicalDocument Map(
        ReadOnlyMemory<byte> rawResponse,
        DocumentAnalysisRequest request)
    {
        using var json = JsonDocument.Parse(rawResponse);
        var result = json.RootElement.GetProperty("result");
        var content = result.GetProperty("contents")[0];
        var metadata = CreateSystemMetadata(request);

        var fields = content.TryGetProperty("fields", out var fieldsElement)
            ? fieldsElement
            : default;
        foreach (var (name, origin) in FieldOrigins)
        {
            metadata[name] = fields.ValueKind == JsonValueKind.Object &&
                             fields.TryGetProperty(name, out var field)
                ? MapMetadataValue(name, field, origin, request.DocumentId)
                : new CanonicalMetadataValue(null, origin, null, []);
        }

        var pages = MapPages(content, request.DocumentId);
        return new CanonicalDocument(
            request.DocumentId,
            request.WorkspaceId,
            pages,
            metadata,
            []);
    }

    private static Dictionary<string, CanonicalMetadataValue> CreateSystemMetadata(
        DocumentAnalysisRequest request) =>
        new(StringComparer.Ordinal)
        {
            ["sourceFileName"] = SystemMetadata(request.SourceFileName),
            ["sourcePath"] = SystemMetadata(request.SourcePath),
            ["collectionType"] = SystemMetadata(request.CollectionType)
        };

    private static CanonicalMetadataValue SystemMetadata(string value) =>
        new(value, InformationOrigin.SystemAssigned, null, []);

    private CanonicalMetadataValue MapMetadataValue(
        string fieldName,
        JsonElement field,
        InformationOrigin origin,
        DocumentId documentId)
    {
        var value = IsMultiValueTaxonomy(fieldName)
            ? ReadSelectedTaxonomyValues(fieldName, field)
            : ReadFieldValue(field);
        double? confidence = field.TryGetProperty("confidence", out var confidenceElement) &&
                             confidenceElement.TryGetDouble(out var parsedConfidence)
            ? parsedConfidence
            : null;
        var evidence = IsMultiValueTaxonomy(fieldName)
            ? ReadSelectedTaxonomyEvidence(field, documentId, origin)
            : ReadEvidence(field, documentId, origin, value?.ToString());
        if (origin is InformationOrigin.Inferred or InformationOrigin.Generated)
        {
            evidence = IsMultiValueTaxonomy(fieldName)
                ? ReadSelectedTaxonomyEvidence(field, documentId, origin)
                : ReadEvidence(field, documentId, origin, null);
        }

        return new CanonicalMetadataValue(value, origin, confidence, evidence);
    }

    private static bool IsMultiValueTaxonomy(string fieldName) =>
        fieldName is "contentIndexes" or "semanticTags";

    private IReadOnlyList<string>? ReadSelectedTaxonomyValues(
        string fieldName,
        JsonElement field)
    {
        if (!field.TryGetProperty("valueObject", out var valueObject))
        {
            return null;
        }

        _taxonomyValues.TryGetValue(fieldName, out var configuredValues);
        return valueObject.EnumerateObject()
            .Where(property => ReadFieldValue(property.Value) is true)
            .Select(property =>
                configuredValues?.GetValueOrDefault(property.Name) ?? property.Name)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> ToProviderValueMap(
        TaxonomyDefinition definition) =>
        definition.Options.ToDictionary(
            option => option.ProviderFieldName,
            option => option.Value,
            StringComparer.Ordinal);

    private static IReadOnlyList<Evidence> ReadSelectedTaxonomyEvidence(
        JsonElement field,
        DocumentId documentId,
        InformationOrigin origin)
    {
        if (!field.TryGetProperty("valueObject", out var valueObject))
        {
            return ReadEvidence(field, documentId, origin, null);
        }

        var evidence = valueObject.EnumerateObject()
            .Where(property => ReadFieldValue(property.Value) is true)
            .SelectMany(property => ReadEvidence(property.Value, documentId, origin, null))
            .ToArray();
        return evidence.Length > 0
            ? evidence
            : ReadEvidence(field, documentId, origin, null);
    }

    private static object? ReadFieldValue(JsonElement field)
    {
        if (field.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (field.TryGetProperty("valueString", out var stringValue))
        {
            return stringValue.GetString();
        }

        if (field.TryGetProperty("valueDate", out var dateValue))
        {
            return dateValue.GetString();
        }

        if (field.TryGetProperty("valueNumber", out var numberValue))
        {
            return numberValue.GetDouble();
        }

        if (field.TryGetProperty("valueBoolean", out var booleanValue))
        {
            return booleanValue.GetBoolean();
        }

        if (field.TryGetProperty("valueArray", out var arrayValue))
        {
            return arrayValue.EnumerateArray().Select(ReadFieldValue).ToArray();
        }

        if (field.TryGetProperty("valueObject", out var objectValue))
        {
            return objectValue.EnumerateObject()
                .ToDictionary(item => item.Name, item => ReadFieldValue(item.Value));
        }

        return null;
    }

    private static IReadOnlyList<CanonicalPage> MapPages(
        JsonElement content,
        DocumentId documentId)
    {
        if (!content.TryGetProperty("pages", out var pagesElement))
        {
            return [];
        }

        var tablesByPage = MapTables(content, documentId);
        var figuresByPage = MapFigures(content, documentId);
        var pages = new List<CanonicalPage>();
        foreach (var page in pagesElement.EnumerateArray())
        {
            var pageNumber = page.GetProperty("pageNumber").GetInt32();
            var lines = page.TryGetProperty("lines", out var linesElement)
                ? linesElement.EnumerateArray().ToArray()
                : [];
            var pageContent = string.Join(
                Environment.NewLine,
                lines.Select(line => line.TryGetProperty("content", out var value)
                    ? value.GetString()
                    : null).Where(value => !string.IsNullOrWhiteSpace(value)));
            var lineEvidence = lines
                .SelectMany(line => ReadEvidence(
                    line,
                    documentId,
                    InformationOrigin.Extracted,
                    line.TryGetProperty("content", out var value) ? value.GetString() : null))
                .ToArray();
            var wordEvidence = page.TryGetProperty("words", out var wordsElement)
                ? wordsElement.EnumerateArray()
                    .SelectMany(word => ReadEvidence(
                        word,
                        documentId,
                        InformationOrigin.Extracted,
                        word.TryGetProperty("content", out var value) ? value.GetString() : null))
                    .ToArray()
                : [];

            pages.Add(new CanonicalPage(
                new PageId($"{documentId.Value:N}-page-{pageNumber:D4}"),
                pageNumber,
                string.IsNullOrWhiteSpace(pageContent) ? null : pageContent)
            {
                Tables = tablesByPage.GetValueOrDefault(pageNumber, []),
                Figures = figuresByPage.GetValueOrDefault(pageNumber, []),
                Regions = lines.Select(line => new CanonicalRegion(
                    line.TryGetProperty("content", out var value) ? value.GetString() : null,
                    ReadFirstBoundingRegion(line),
                    ReadEvidence(
                        line,
                        documentId,
                        InformationOrigin.Extracted,
                        line.TryGetProperty("content", out var text) ? text.GetString() : null)))
                    .ToArray(),
                Evidence = [.. lineEvidence, .. wordEvidence]
            });
        }

        return pages;
    }

    private static Dictionary<int, IReadOnlyList<CanonicalTable>> MapTables(
        JsonElement content,
        DocumentId documentId)
    {
        var result = new Dictionary<int, List<CanonicalTable>>();
        if (!content.TryGetProperty("tables", out var tables))
        {
            return result.ToDictionary(item => item.Key, item => (IReadOnlyList<CanonicalTable>)item.Value);
        }

        foreach (var table in tables.EnumerateArray())
        {
            var evidence = ReadEvidence(table, documentId, InformationOrigin.Extracted, null);
            var pageNumber = evidence.FirstOrDefault()?.PageNumber ?? 1;
            var tableContent = table.TryGetProperty("content", out var contentValue)
                ? contentValue.GetString()
                : table.GetRawText();
            var caption = table.TryGetProperty("caption", out var captionValue) &&
                          captionValue.TryGetProperty("content", out var captionContent)
                ? captionContent.GetString()
                : null;
            result.TryAdd(pageNumber, []);
            result[pageNumber].Add(new CanonicalTable(caption, tableContent ?? string.Empty, evidence));
        }

        return result.ToDictionary(item => item.Key, item => (IReadOnlyList<CanonicalTable>)item.Value);
    }

    private static Dictionary<int, IReadOnlyList<CanonicalFigure>> MapFigures(
        JsonElement content,
        DocumentId documentId)
    {
        var result = new Dictionary<int, List<CanonicalFigure>>();
        if (!content.TryGetProperty("figures", out var figures))
        {
            return result.ToDictionary(item => item.Key, item => (IReadOnlyList<CanonicalFigure>)item.Value);
        }

        foreach (var figure in figures.EnumerateArray())
        {
            var evidence = ReadEvidence(figure, documentId, InformationOrigin.Extracted, null);
            var pageNumber = evidence.FirstOrDefault()?.PageNumber ?? 1;
            var caption = figure.TryGetProperty("caption", out var captionValue) &&
                          captionValue.TryGetProperty("content", out var captionContent)
                ? captionContent.GetString()
                : null;
            var description = figure.TryGetProperty("description", out var descriptionValue)
                ? descriptionValue.GetString()
                : null;
            result.TryAdd(pageNumber, []);
            result[pageNumber].Add(new CanonicalFigure(caption, description, evidence));
        }

        return result.ToDictionary(item => item.Key, item => (IReadOnlyList<CanonicalFigure>)item.Value);
    }

    private static IReadOnlyList<Evidence> ReadEvidence(
        JsonElement element,
        DocumentId documentId,
        InformationOrigin origin,
        string? extractedValue)
    {
        double? confidence = element.TryGetProperty("confidence", out var confidenceElement) &&
                             confidenceElement.TryGetDouble(out var parsedConfidence)
            ? parsedConfidence
            : null;
        var sources = new List<string>();
        if (element.TryGetProperty("source", out var source) &&
            source.ValueKind == JsonValueKind.String)
        {
            sources.Add(source.GetString()!);
        }

        if (element.TryGetProperty("sources", out var sourceArray) &&
            sourceArray.ValueKind == JsonValueKind.Array)
        {
            sources.AddRange(sourceArray.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!));
        }

        return sources
            .SelectMany(ParseSource)
            .Select(region => new Evidence(
                documentId,
                region.PageNumber,
                region.BoundingRegion,
                extractedValue,
                confidence,
                origin))
            .ToArray();
    }

    private static BoundingRegion? ReadFirstBoundingRegion(JsonElement element)
    {
        if (!element.TryGetProperty("source", out var source) ||
            source.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return ParseSource(source.GetString()!).FirstOrDefault().BoundingRegion;
    }

    private static IEnumerable<(int PageNumber, BoundingRegion? BoundingRegion)> ParseSource(
        string source)
    {
        foreach (Match match in DocumentSourceRegex().Matches(source))
        {
            var values = match.Groups["values"].Value
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (values.Length == 0 ||
                !int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var page))
            {
                continue;
            }

            var coordinates = values.Skip(1)
                .Select(value => double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var coordinate)
                    ? coordinate
                    : (double?)null)
                .ToArray();
            var region = coordinates.Length >= 8 && coordinates.All(value => value.HasValue)
                ? new BoundingRegion(Enumerable.Range(0, coordinates.Length / 2)
                    .Select(index => new Point(
                        coordinates[index * 2]!.Value,
                        coordinates[index * 2 + 1]!.Value))
                    .ToArray())
                : null;
            yield return (page, region);
        }
    }

    [GeneratedRegex(@"D\((?<values>[^)]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex DocumentSourceRegex();
}
