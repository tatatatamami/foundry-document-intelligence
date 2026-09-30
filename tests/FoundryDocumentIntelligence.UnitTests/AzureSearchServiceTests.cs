using FoundryDocumentIntelligence.Domain.Search;
using FoundryDocumentIntelligence.Domain.Workspaces;
using FoundryDocumentIntelligence.Infrastructure.Search;

namespace FoundryDocumentIntelligence.UnitTests;

public sealed class AzureSearchServiceTests
{
    [Fact]
    public void BuildFilter_AlwaysIsolatesWorkspaceAndEscapesConfiguredFilters()
    {
        var workspaceId = new WorkspaceId(Guid.NewGuid());
        var query = new DocumentSearchQuery(
            workspaceId,
            "query",
            "企画'資料",
            "Program",
            "操作・アクション");

        var filter = AzureSearchService.BuildFilter(query);

        Assert.Contains($"workspaceId eq '{workspaceId.Value:D}'", filter);
        Assert.Contains("documentCategory eq '企画''資料'", filter);
        Assert.Contains("contentIndexes/any(item: item eq 'Program')", filter);
        Assert.Contains("semanticTags/any(item: item eq '操作・アクション')", filter);
    }
}
