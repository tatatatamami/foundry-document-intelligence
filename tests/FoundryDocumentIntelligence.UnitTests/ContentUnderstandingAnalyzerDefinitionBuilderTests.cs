using FoundryDocumentIntelligence.Domain.Workspaces;
using FoundryDocumentIntelligence.Infrastructure.ContentUnderstanding;

namespace FoundryDocumentIntelligence.UnitTests;

public sealed class ContentUnderstandingAnalyzerDefinitionBuilderTests
{
    [Fact]
    public void Build_CreatesBooleanObjectPropertiesFromWorkspaceTaxonomy()
    {
        var taxonomy = new WorkspaceTaxonomy(
            Definition("documentCategory", ("企画資料", "PlanningDocument", "企画内容を扱う。"), ("その他", "Other", "記録を扱う。")),
            Definition("contentIndexes", ("Program", "Program", "実装を扱う。"), ("Sound", "Sound", "音響を扱う。")),
            Definition("semanticTags", ("画面・UI", "ScreenUi", "画面表示を扱う。")),
            Definition("developmentPhase", ("企画", "Planning", "初期検討を扱う。"), ("未設定", "Unspecified", "段階不明の情報を扱う。")));

        var definition = new ContentUnderstandingAnalyzerDefinitionBuilder()
            .Build("gpt-4.1-mini", taxonomy);
        var fields = definition["fieldSchema"]!["fields"]!;

        Assert.Equal("object", fields["contentIndexes"]!["type"]!.GetValue<string>());
        Assert.Equal("generate", fields["contentIndexes"]!["method"]!.GetValue<string>());
        Assert.Equal(
            "Program: 実装を扱う。",
            fields["contentIndexes"]!["properties"]!["Program"]!["description"]!.GetValue<string>());
        Assert.Null(fields["contentIndexes"]!["properties"]!["Graphic"]);
        Assert.Equal(
            "画面・UI: 画面表示を扱う。",
            fields["semanticTags"]!["properties"]!["ScreenUi"]!["description"]!.GetValue<string>());
        Assert.Contains(
            "企画資料: 企画内容を扱う。",
            fields["documentCategory"]!["description"]!.GetValue<string>());
        Assert.Contains(
            "未設定: 段階不明の情報を扱う。",
            fields["developmentPhase"]!["description"]!.GetValue<string>());
    }

    private static TaxonomyDefinition Definition(
        string name,
        params (string Value, string ProviderFieldName, string Description)[] options) =>
        new(
            name,
            name is "contentIndexes" or "semanticTags",
            options.Select(option =>
                new TaxonomyOption(
                    option.Value,
                    option.Value,
                    option.Description,
                    option.ProviderFieldName)).ToArray());
}
