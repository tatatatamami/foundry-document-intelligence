namespace FoundryDocumentIntelligence.Domain.Workspaces;

public sealed record Workspace
{
    public Workspace(WorkspaceId id, string displayName)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("Workspace ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Id = id;
        DisplayName = displayName;
    }

    public WorkspaceId Id { get; }

    public string DisplayName { get; }
}
