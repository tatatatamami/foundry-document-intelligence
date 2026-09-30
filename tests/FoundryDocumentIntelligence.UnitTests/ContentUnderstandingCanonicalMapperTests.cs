using System.Text;
using FoundryDocumentIntelligence.Application.Documents;
using FoundryDocumentIntelligence.Domain.Documents;
using FoundryDocumentIntelligence.Domain.Workspaces;
using FoundryDocumentIntelligence.Infrastructure.ContentUnderstanding;

namespace FoundryDocumentIntelligence.UnitTests;

public class ContentUnderstandingCanonicalMapperTests
{
    [Fact]
    public void Map_SeparatesIdentitySystemMetadataAndAiEnrichment()
    {
        var documentId = new DocumentId(Guid.NewGuid());
        var workspaceId = new WorkspaceId(Guid.NewGuid());
        var raw = Encoding.UTF8.GetBytes(
            """
            {
              "status": "Succeeded",
              "result": {
                "contents": [{
                  "fields": {
                    "documentDate": {
                      "type": "date",
                      "valueDate": "1991-01-02",
                      "confidence": 0.91,
                      "source": "D(1, 1, 2, 3, 2, 3, 4, 1, 4)"
                    },
                    "documentCategory": {
                      "type": "string",
                      "valueString": "企画資料",
                      "confidence": 0.88,
                      "source": "D(1, 1, 2, 3, 2, 3, 4, 1, 4)"
                    },
                    "contentIndexes": {
                      "type": "object",
                      "confidence": 0.84,
                      "valueObject": {
                        "Program": {
                          "type": "boolean",
                          "valueBoolean": true,
                          "confidence": 0.83,
                          "source": "D(1, 1, 2, 3, 2, 3, 4, 1, 4)"
                        },
                        "Graphic": {
                          "type": "boolean",
                          "valueBoolean": false,
                          "confidence": 0.92
                        },
                        "Sound": {
                          "type": "boolean",
                          "valueBoolean": true
                        }
                      }
                    },
                    "semanticTags": {
                      "type": "object",
                      "valueObject": {
                        "ControlsActions": {
                          "type": "boolean",
                          "valueBoolean": true
                        },
                        "ScreenUi": {
                          "type": "boolean",
                          "valueBoolean": false,
                          "source": "D(1, 1, 2, 3, 2, 3, 4, 1, 4)"
                        }
                      }
                    },
                    "summary": {
                      "type": "string",
                      "valueString": "操作案を検討する資料。",
                      "confidence": 0.82
                    },
                    "developmentPhase": {
                      "type": "string",
                      "valueString": "企画",
                      "confidence": 0.75
                    }
                  },
                  "pages": [{
                    "pageNumber": 1,
                    "lines": [{
                      "content": "テーマ選定について",
                      "confidence": 0.99,
                      "source": "D(1, 1, 2, 3, 2, 3, 4, 1, 4)"
                    }]
                  }]
                }]
              }
            }
            """);
        var request = new DocumentAnalysisRequest(
            workspaceId,
            documentId,
            @"C:\input\sample.jpg",
            @"data\demo\source\sample.jpg",
            "sample.jpg",
            "demo-documents");

        var document = new ContentUnderstandingCanonicalMapper(CreateTaxonomy()).Map(raw, request);

        Assert.Equal(documentId, document.DocumentId);
        Assert.Equal(workspaceId, document.WorkspaceId);
        Assert.Equal(
            InformationOrigin.SystemAssigned,
            document.Metadata["sourceFileName"].Origin);
        Assert.Equal(
            InformationOrigin.Extracted,
            document.Metadata["documentDate"].Origin);
        Assert.Equal(
            InformationOrigin.Inferred,
            document.Metadata["documentCategory"].Origin);
        Assert.Equal(
            InformationOrigin.Generated,
            document.Metadata["summary"].Origin);
        Assert.Equal(
            ["Program", "Sound"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(
                document.Metadata["contentIndexes"].Value));
        Assert.Equal(
            ["操作・アクション"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(
                document.Metadata["semanticTags"].Value));
        Assert.Equal(0.84, document.Metadata["contentIndexes"].Confidence);
        Assert.Single(document.Metadata["contentIndexes"].Evidence);
        Assert.Empty(document.Metadata["semanticTags"].Evidence);
        Assert.Single(document.Metadata["documentDate"].Evidence);
        Assert.Null(document.Metadata["documentCategory"].Evidence[0].ExtractedValue);
        Assert.Null(document.Metadata["summary"].Evidence.FirstOrDefault()?.ExtractedValue);
        Assert.Equal(1, document.Pages[0].PageNumber);
        Assert.Equal(
            $"{documentId.Value:N}-page-0001",
            document.Pages[0].PageId.Value);
        Assert.All(
            document.Pages[0].Evidence,
            evidence => Assert.Equal(InformationOrigin.Extracted, evidence.Origin));
    }

    private static WorkspaceTaxonomy CreateTaxonomy()
    {
        static TaxonomyDefinition Definition(
            string name,
            params (string Value, string ProviderFieldName)[] options) =>
            new(
                name,
                name is "contentIndexes" or "semanticTags",
                options.Select(option => new TaxonomyOption(
                    option.Value,
                    option.Value,
                    $"{option.Value}の判定条件。",
                    option.ProviderFieldName)).ToArray());

        return new WorkspaceTaxonomy(
            Definition("documentCategory", ("企画資料", "Planning")),
            Definition(
                "contentIndexes",
                ("Program", "Program"),
                ("Graphic", "Graphic"),
                ("Sound", "Sound")),
            Definition(
                "semanticTags",
                ("操作・アクション", "ControlsActions"),
                ("画面・UI", "ScreenUi")),
            Definition("developmentPhase", ("企画", "Planning")));
    }

    [Fact]
    public void Map_DoesNotInventMultiValueTaxonomyWhenProviderOmitsValueObject()
    {
        var raw = Encoding.UTF8.GetBytes(
            """
            {
              "result": {
                "contents": [{
                  "fields": {
                    "contentIndexes": { "type": "object" },
                    "semanticTags": { "type": "object" }
                  },
                  "pages": []
                }]
              }
            }
            """);
        var request = new DocumentAnalysisRequest(
            new WorkspaceId(Guid.NewGuid()),
            new DocumentId(Guid.NewGuid()),
            "sample.jpg",
            "sample.jpg",
            "sample.jpg",
            "demo-documents");

        var document = new ContentUnderstandingCanonicalMapper().Map(raw, request);

        Assert.Null(document.Metadata["contentIndexes"].Value);
        Assert.Null(document.Metadata["semanticTags"].Value);
        Assert.Empty(document.Metadata["contentIndexes"].Evidence);
        Assert.Empty(document.Metadata["semanticTags"].Evidence);
    }

    [Fact]
    public void Map_DoesNotInventMissingDateOrEvidence()
    {
        var documentId = new DocumentId(Guid.NewGuid());
        var raw = Encoding.UTF8.GetBytes(
            """
            {
              "status": "Succeeded",
              "result": {
                "contents": [{
                  "fields": {},
                  "pages": [{ "pageNumber": 1, "lines": [] }]
                }]
              }
            }
            """);
        var request = new DocumentAnalysisRequest(
            new WorkspaceId(Guid.NewGuid()),
            documentId,
            "sample.jpg",
            "sample.jpg",
            "sample.jpg",
            "demo-documents");

        var document = new ContentUnderstandingCanonicalMapper().Map(raw, request);

        Assert.Null(document.Metadata["documentDate"].Value);
        Assert.Empty(document.Metadata["documentDate"].Evidence);
        Assert.Null(document.Metadata["documentDate"].Confidence);
    }
}
