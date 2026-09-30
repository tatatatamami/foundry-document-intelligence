using FoundryDocumentIntelligence.Domain.Documents;
using FoundryDocumentIntelligence.Domain.Workspaces;

namespace FoundryDocumentIntelligence.Application.Documents;

public interface IDocumentEnrichmentService
{
    Task<CanonicalDocument> EnrichAsync(
        CanonicalDocument document,
        WorkspaceTaxonomy taxonomy,
        CancellationToken cancellationToken = default);
}
