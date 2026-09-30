using FoundryDocumentIntelligence.Domain.Documents;

namespace FoundryDocumentIntelligence.Domain.Workspaces;

public sealed record WorkspaceConfiguration(
    WorkspaceId WorkspaceId,
    AnalyzerConfiguration Analyzer,
    IReadOnlyList<MetadataFieldDefinition> MetadataSchema,
    WorkspaceTaxonomy Taxonomy,
    IReadOnlyList<string> FilterFields,
    IReadOnlyList<string> FacetFields,
    string? EvaluationDataset);

public sealed record AnalyzerConfiguration(string AnalyzerId, string ApiVersion);

public sealed record WorkspaceTaxonomy(
    TaxonomyDefinition DocumentCategories,
    TaxonomyDefinition ContentIndexes,
    TaxonomyDefinition SemanticTags,
    TaxonomyDefinition DevelopmentPhases);

public sealed record TaxonomyDefinition(
    string Name,
    bool AllowsMultipleValues,
    IReadOnlyList<TaxonomyOption> Options);

public sealed record TaxonomyOption(
    string Value,
    string DisplayName,
    string Description,
    string ProviderFieldName);

public sealed record MetadataFieldDefinition(
    string Name,
    MetadataValueType Type,
    bool AllowsMultipleValues,
    InformationOrigin ExpectedOrigin);

public enum MetadataValueType
{
    String,
    Number,
    Boolean,
    DateTime
}
