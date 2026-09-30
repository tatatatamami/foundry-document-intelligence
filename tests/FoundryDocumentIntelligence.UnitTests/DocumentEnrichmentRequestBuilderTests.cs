using FoundryDocumentIntelligence.Domain.Documents;
using FoundryDocumentIntelligence.Domain.Workspaces;
using FoundryDocumentIntelligence.Infrastructure.OpenAI;

namespace FoundryDocumentIntelligence.UnitTests;

public sealed class DocumentEnrichmentRequestBuilderTests
{
    [Fact]
    public void BuildSchema_UsesOnlyConfiguredTaxonomyValuesAndStrictObject()
    {
        var taxonomy = CreateTaxonomy();

        var schema = new DocumentEnrichmentRequestBuilder().BuildSchema(taxonomy);

        Assert.False(schema["additionalProperties"]!.GetValue<bool>());
        var properties = schema["properties"]!;
        Assert.Equal(
            ["CategoryA", "CategoryB"],
            properties["documentCategory"]!["enum"]!
                .AsArray()
                .Select(value => value!.GetValue<string>()));
        Assert.Equal(
            ["IndexA", "IndexB"],
            properties["contentIndexes"]!["items"]!["enum"]!
                .AsArray()
                .Select(value => value!.GetValue<string>()));
        Assert.Equal(
            ["TagA", "TagB"],
            properties["semanticTags"]!["items"]!["enum"]!
                .AsArray()
                .Select(value => value!.GetValue<string>()));
    }

    [Fact]
    public void Build_IncludesPageContentAndTaxonomyDescriptions()
    {
        var document = new CanonicalDocument(
            new DocumentId(Guid.NewGuid()),
            new WorkspaceId(Guid.NewGuid()),
            [new CanonicalPage(new PageId("page-1"), 1, "OCR content")],
            new Dictionary<string, CanonicalMetadataValue>(),
            []);

        var request = new DocumentEnrichmentRequestBuilder()
            .Build(document, CreateTaxonomy(), "deployment");
        var content = request["messages"]![1]!["content"]!.GetValue<string>();

        Assert.Contains("OCR content", content);
        Assert.Contains("CategoryA: CategoryA criteria", content);
        Assert.Equal(
            "json_schema",
            request["response_format"]!["type"]!.GetValue<string>());
        Assert.True(
            request["response_format"]!["json_schema"]!["strict"]!.GetValue<bool>());
    }

    private static WorkspaceTaxonomy CreateTaxonomy() =>
        new(
            Definition("documentCategory", "CategoryA", "CategoryB"),
            Definition("contentIndexes", "IndexA", "IndexB"),
            Definition("semanticTags", "TagA", "TagB"),
            Definition("developmentPhase", "PhaseA", "PhaseB"));

    private static TaxonomyDefinition Definition(string name, params string[] values) =>
        new(
            name,
            name is "contentIndexes" or "semanticTags",
            values.Select(value => new TaxonomyOption(
                value,
                value,
                $"{value} criteria",
                value)).ToArray());
}
