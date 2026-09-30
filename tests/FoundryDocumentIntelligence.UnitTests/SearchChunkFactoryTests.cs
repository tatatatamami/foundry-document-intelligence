using FoundryDocumentIntelligence.Application.Search;
using FoundryDocumentIntelligence.Domain.Documents;
using FoundryDocumentIntelligence.Domain.Workspaces;

namespace FoundryDocumentIntelligence.UnitTests;

public sealed class SearchChunkFactoryTests
{
    [Fact]
    public void Create_ProjectsDocumentMetadataOntoEachNonEmptyPage()
    {
        var documentId = new DocumentId(Guid.NewGuid());
        var document = new CanonicalDocument(
            documentId,
            new WorkspaceId(Guid.NewGuid()),
            [
                new CanonicalPage(new PageId("page-1"), 1, "First page"),
                new CanonicalPage(new PageId("page-2"), 2, "Second page"),
                new CanonicalPage(new PageId("page-3"), 3, null)
            ],
            new Dictionary<string, CanonicalMetadataValue>
            {
                ["sourceFileName"] = Metadata("sample.pdf"),
                ["collectionType"] = Metadata("demo"),
                ["documentCategory"] = Metadata("企画資料"),
                ["contentIndexes"] = Metadata(new[] { "Program", "Graphic" }),
                ["semanticTags"] = Metadata(new[] { "操作・アクション" }),
                ["documentDate"] = Metadata("2026-09-30"),
                ["developmentPhase"] = Metadata("企画"),
                ["summary"] = Metadata("Summary")
            },
            []);

        var chunks = new SearchChunkFactory().Create(document);

        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, chunk =>
        {
            Assert.Equal(documentId, chunk.DocumentId);
            Assert.Equal("企画資料", chunk.DocumentCategory);
            Assert.Equal(["Program", "Graphic"], chunk.ContentIndexes);
            Assert.Equal(["操作・アクション"], chunk.SemanticTags);
            Assert.Equal("Summary", chunk.Summary);
            Assert.Empty(chunk.ContentVector);
        });
        Assert.Equal("page-1", chunks[0].PageId.Value);
        Assert.Equal("First page", chunks[0].Content);
    }

    private static CanonicalMetadataValue Metadata(object value) =>
        new(value, InformationOrigin.Inferred, null, []);
}
