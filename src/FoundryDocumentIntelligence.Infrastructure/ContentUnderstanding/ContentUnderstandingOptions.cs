using FoundryDocumentIntelligence.Domain.Workspaces;

namespace FoundryDocumentIntelligence.Infrastructure.ContentUnderstanding;

public sealed record ContentUnderstandingOptions(
    Uri Endpoint,
    string ApiVersion,
    string AnalyzerId,
    string ArtifactRoot,
    WorkspaceTaxonomy Taxonomy);
