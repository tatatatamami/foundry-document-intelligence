using FoundryDocumentIntelligence.Domain.Documents;
using FoundryDocumentIntelligence.Domain.Workspaces;

namespace FoundryDocumentIntelligence.UnitTests;

public class CanonicalDocumentTests
{
    [Theory]
    [InlineData(InformationOrigin.SourceExplicit)]
    [InlineData(InformationOrigin.Extracted)]
    [InlineData(InformationOrigin.Inferred)]
    [InlineData(InformationOrigin.Generated)]
    public void Metadata_PreservesOriginConfidenceAndEvidence(InformationOrigin origin)
    {
        var sourceDocumentId = new DocumentId(Guid.NewGuid());
        var evidence = new Evidence(
            sourceDocumentId,
            2,
            null,
            "Agreement",
            0.93,
            origin);
        var metadataValue = new CanonicalMetadataValue(
            "Agreement",
            origin,
            0.93,
            [evidence]);
        var document = new CanonicalDocument(
            new DocumentId(Guid.NewGuid()),
            new WorkspaceId(Guid.NewGuid()),
            [],
            new Dictionary<string, CanonicalMetadataValue>
            {
                ["classification"] = metadataValue
            },
            []);

        var result = document.Metadata["classification"];

        Assert.Equal("Agreement", result.Value);
        Assert.Equal(origin, result.Origin);
        Assert.Equal(0.93, result.Confidence);
        Assert.Contains(evidence, result.Evidence);
    }

    [Fact]
    public void Identity_DoesNotRequireInformationOrigin()
    {
        var documentId = new DocumentId(Guid.NewGuid());
        var workspaceId = new WorkspaceId(Guid.NewGuid());
        var pageId = new PageId($"{documentId.Value:N}-page-0001");

        var document = new CanonicalDocument(
            documentId,
            workspaceId,
            [new CanonicalPage(pageId, 1, "content")],
            new Dictionary<string, CanonicalMetadataValue>
            {
                ["sourceFileName"] = new(
                    "sample.jpg",
                    InformationOrigin.SystemAssigned,
                    null,
                    [])
            },
            []);

        Assert.Equal(documentId, document.DocumentId);
        Assert.Equal(workspaceId, document.WorkspaceId);
        Assert.Equal(pageId, document.Pages[0].PageId);
        Assert.Equal(
            InformationOrigin.SystemAssigned,
            document.Metadata["sourceFileName"].Origin);
    }
}
