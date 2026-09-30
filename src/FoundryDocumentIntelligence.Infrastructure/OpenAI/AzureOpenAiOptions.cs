namespace FoundryDocumentIntelligence.Infrastructure.OpenAI;

public sealed record AzureOpenAiOptions(Uri Endpoint, string DeploymentName);

public sealed record AzureOpenAiEmbeddingOptions(
    Uri Endpoint,
    string DeploymentName,
    int Dimensions);
