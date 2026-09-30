using FoundryDocumentIntelligence.Domain.Workspaces;

namespace FoundryDocumentIntelligence.UnitTests;

public class WorkspaceTests
{
    [Fact]
    public void Constructor_RequiresNonEmptyId()
    {
        Assert.Throws<ArgumentException>(() => new Workspace(default, "Example"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_RequiresDisplayName(string? displayName)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new Workspace(new WorkspaceId(Guid.NewGuid()), displayName!));
    }

    [Fact]
    public void Constructor_PreservesGenericWorkspaceIdentity()
    {
        var id = new WorkspaceId(Guid.NewGuid());

        var workspace = new Workspace(id, "Example workspace");

        Assert.Equal(id, workspace.Id);
        Assert.Equal("Example workspace", workspace.DisplayName);
    }
}
