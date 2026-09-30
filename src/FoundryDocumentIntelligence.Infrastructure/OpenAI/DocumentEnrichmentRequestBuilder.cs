using System.Text;
using System.Text.Json.Nodes;
using FoundryDocumentIntelligence.Domain.Documents;
using FoundryDocumentIntelligence.Domain.Workspaces;

namespace FoundryDocumentIntelligence.Infrastructure.OpenAI;

public sealed class DocumentEnrichmentRequestBuilder
{
    public JsonObject Build(
        CanonicalDocument document,
        WorkspaceTaxonomy taxonomy,
        string deploymentName)
    {
        var schema = BuildSchema(taxonomy);
        return new JsonObject
        {
            ["model"] = deploymentName,
            ["temperature"] = 0,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] =
                        "You classify and summarize documents using only the supplied OCR content. " +
                        "Use the configured taxonomy criteria. Do not add external facts. " +
                        "Select every supported multi-value label and no unsupported labels. " +
                        "Return a concise Japanese summary in one or two sentences. " +
                        "When the development phase is not sufficiently supported, select 未設定."
                },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = BuildDocumentInput(document, taxonomy)
                }
            },
            ["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = "document_enrichment",
                    ["strict"] = true,
                    ["schema"] = schema
                }
            }
        };
    }

    public JsonObject BuildSchema(WorkspaceTaxonomy taxonomy) =>
        new()
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["documentCategory"] = EnumString(taxonomy.DocumentCategories),
                ["contentIndexes"] = EnumArray(taxonomy.ContentIndexes),
                ["semanticTags"] = EnumArray(taxonomy.SemanticTags),
                ["summary"] = new JsonObject { ["type"] = "string" },
                ["developmentPhase"] = EnumString(taxonomy.DevelopmentPhases)
            },
            ["required"] = new JsonArray(
                "documentCategory",
                "contentIndexes",
                "semanticTags",
                "summary",
                "developmentPhase")
        };

    private static JsonObject EnumString(TaxonomyDefinition definition) =>
        new()
        {
            ["type"] = "string",
            ["enum"] = ToValues(definition)
        };

    private static JsonObject EnumArray(TaxonomyDefinition definition) =>
        new()
        {
            ["type"] = "array",
            ["items"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = ToValues(definition)
            }
        };

    private static JsonArray ToValues(TaxonomyDefinition definition)
    {
        var result = new JsonArray();
        foreach (var option in definition.Options)
        {
            result.Add(option.Value);
        }

        return result;
    }

    private static string BuildDocumentInput(
        CanonicalDocument document,
        WorkspaceTaxonomy taxonomy)
    {
        var builder = new StringBuilder();
        builder.AppendLine("TAXONOMY CRITERIA");
        AppendTaxonomy(builder, taxonomy.DocumentCategories);
        AppendTaxonomy(builder, taxonomy.ContentIndexes);
        AppendTaxonomy(builder, taxonomy.SemanticTags);
        AppendTaxonomy(builder, taxonomy.DevelopmentPhases);
        builder.AppendLine();
        builder.AppendLine("DOCUMENT OCR CONTENT");
        foreach (var page in document.Pages)
        {
            builder.AppendLine($"--- Page {page.PageNumber} ---");
            builder.AppendLine(page.Content);
        }

        return builder.ToString();
    }

    private static void AppendTaxonomy(
        StringBuilder builder,
        TaxonomyDefinition definition)
    {
        builder.AppendLine($"{definition.Name}:");
        foreach (var option in definition.Options)
        {
            builder.AppendLine($"- {option.Value}: {option.Description}");
        }
    }
}
