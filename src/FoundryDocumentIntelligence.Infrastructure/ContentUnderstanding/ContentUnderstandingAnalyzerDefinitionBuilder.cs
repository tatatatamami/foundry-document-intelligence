using System.Text.Json.Nodes;
using FoundryDocumentIntelligence.Domain.Workspaces;

namespace FoundryDocumentIntelligence.Infrastructure.ContentUnderstanding;

public sealed class ContentUnderstandingAnalyzerDefinitionBuilder
{
    public JsonObject Build(string completionModel, WorkspaceTaxonomy taxonomy) =>
        new()
        {
            ["description"] =
                "日本語の企画資料、制作指示、仕様書、記録資料から文書レベルのメタデータとAI Enrichmentを生成する。値は解析対象の文書内容だけに基づく。",
            ["baseAnalyzerId"] = "prebuilt-document",
            ["models"] = new JsonObject
            {
                ["completion"] = completionModel
            },
            ["config"] = new JsonObject
            {
                ["returnDetails"] = true,
                ["enableOcr"] = true,
                ["enableLayout"] = true,
                ["enableFormula"] = false,
                ["enableBarcode"] = false,
                ["estimateFieldSourceAndConfidence"] = true,
                ["tableFormat"] = "html"
            },
            ["fieldSchema"] = new JsonObject
            {
                ["name"] = "DemoDocumentMetadataV2",
                ["fields"] = new JsonObject
                {
                    ["documentDate"] = new JsonObject
                    {
                        ["type"] = "date",
                        ["method"] = "extract",
                        ["estimateSourceAndConfidence"] = true,
                        ["description"] =
                            "資料の日付として明示的に印刷または記載された、信頼できる日付を抽出する。文脈から日付や年を推測せず、明示的な日付がない場合は値を返さない。"
                    },
                    ["documentCategory"] = BuildClassificationField(
                        taxonomy.DocumentCategories,
                        "文書全体の主目的を、次の肯定的な判定条件に基づいて1つ分類する。"),
                    ["contentIndexes"] = BuildBooleanObjectField(
                        taxonomy.ContentIndexes,
                        "文書で実質的に扱われている制作領域を判定する。各Propertyは、対応する判定条件を満たす内容が含まれる場合にtrueとし、複数をtrueにできる。"),
                    ["semanticTags"] = BuildBooleanObjectField(
                        taxonomy.SemanticTags,
                        "文書で実質的に扱われている意味的概念を判定する。各Propertyは、対応する判定条件を満たす内容が含まれる場合にtrueとし、複数をtrueにできる。"),
                    ["summary"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["method"] = "generate",
                        ["estimateSourceAndConfidence"] = true,
                        ["description"] =
                            "資料によって裏付けられる主要内容だけを、簡潔な日本語1～2文で要約する。外部知識、仮定、資料にない事実を追加しない。"
                    },
                    ["developmentPhase"] = BuildClassificationField(
                        taxonomy.DevelopmentPhases,
                        "文書内容が示す開発段階を、次の肯定的な判定条件に基づいて1つ分類する。特定段階の根拠が十分でない場合は未設定を選ぶ。")
                }
            }
        };

    private static JsonObject BuildClassificationField(
        TaxonomyDefinition taxonomy,
        string description)
    {
        var enumValues = new JsonArray();
        foreach (var option in taxonomy.Options)
        {
            enumValues.Add(option.Value);
        }

        return new JsonObject
        {
            ["type"] = "string",
            ["method"] = "classify",
            ["enum"] = enumValues,
            ["estimateSourceAndConfidence"] = true,
            ["description"] = $"{description} {FormatCriteria(taxonomy.Options)}"
        };
    }

    private static JsonObject BuildBooleanObjectField(
        TaxonomyDefinition taxonomy,
        string description)
    {
        var properties = new JsonObject();
        foreach (var option in taxonomy.Options)
        {
            properties[option.ProviderFieldName] = new JsonObject
            {
                ["type"] = "boolean",
                ["description"] = $"{option.DisplayName}: {option.Description}"
            };
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["method"] = "generate",
            ["properties"] = properties,
            ["estimateSourceAndConfidence"] = true,
            ["description"] = description
        };
    }

    private static string FormatCriteria(IReadOnlyList<TaxonomyOption> options) =>
        string.Join(" ", options.Select(option => $"{option.Value}: {option.Description}"));
}
