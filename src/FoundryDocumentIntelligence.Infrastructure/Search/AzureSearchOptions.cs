namespace FoundryDocumentIntelligence.Infrastructure.Search;

public sealed record AzureSearchOptions(
    Uri Endpoint,
    string ApiVersion,
    string IndexName,
    int VectorDimensions);
