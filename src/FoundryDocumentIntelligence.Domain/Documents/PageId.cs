namespace FoundryDocumentIntelligence.Domain.Documents;

public readonly record struct PageId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
}
