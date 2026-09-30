namespace FoundryDocumentIntelligence.Domain.Workspaces;

public readonly record struct WorkspaceId(Guid Value)
{
    public bool IsEmpty => Value == Guid.Empty;
}
