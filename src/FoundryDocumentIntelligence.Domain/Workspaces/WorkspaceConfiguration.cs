namespace FoundryDocumentIntelligence.Domain.Workspaces;

public sealed record WorkspaceConfiguration(
    WorkspaceId WorkspaceId,
    string? AnalyzerConfigurationId,
    IReadOnlyList<MetadataFieldDefinition> MetadataSchema,
    IReadOnlyList<string> Classifications,
    IReadOnlyList<string> FilterFields,
    IReadOnlyList<string> FacetFields,
    string? EvaluationDataset);

public sealed record MetadataFieldDefinition(string Name, MetadataValueType Type);

public enum MetadataValueType
{
    String,
    Number,
    Boolean,
    DateTime
}
