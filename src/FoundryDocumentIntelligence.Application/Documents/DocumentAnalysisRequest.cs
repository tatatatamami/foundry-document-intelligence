using FoundryDocumentIntelligence.Domain.Documents;
using FoundryDocumentIntelligence.Domain.Workspaces;

namespace FoundryDocumentIntelligence.Application.Documents;

public sealed record DocumentAnalysisRequest(
    WorkspaceId WorkspaceId,
    DocumentId DocumentId,
    string SourceFilePath,
    string SourcePath,
    string SourceFileName,
    string CollectionType);

public sealed record DocumentAnalysisResult(
    CanonicalDocument Document,
    string RawArtifactPath,
    string CanonicalArtifactPath);

public interface IDocumentAnalysisService
{
    Task<DocumentAnalysisResult> AnalyzeAsync(
        DocumentAnalysisRequest request,
        CancellationToken cancellationToken = default);
}
