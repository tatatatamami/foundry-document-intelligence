using FoundryDocumentIntelligence.Web.Components;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Core;
using Azure.Identity;
using FoundryDocumentIntelligence.Application.Search;
using FoundryDocumentIntelligence.Infrastructure.Configuration;
using FoundryDocumentIntelligence.Infrastructure.OpenAI;
using FoundryDocumentIntelligence.Infrastructure.Search;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();

var workspaceConfigPath = Path.GetFullPath(
    builder.Configuration["WorkspaceConfigPath"]
    ?? Path.Combine("..", "..", "config", "demo-workspace.json"));
var workspaceSettings = await DemoWorkspaceSettingsLoader.LoadAsync(workspaceConfigPath);
builder.Services.AddSingleton(workspaceSettings);
builder.Services.AddSingleton<TokenCredential>(_ =>
    new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ExcludeManagedIdentityCredential = builder.Environment.IsDevelopment()
    }));
builder.Services.AddSingleton<IEmbeddingService>(services =>
    new AzureOpenAiEmbeddingService(
        services.GetRequiredService<IHttpClientFactory>().CreateClient("embedding"),
        services.GetRequiredService<TokenCredential>(),
        new AzureOpenAiEmbeddingOptions(
            new Uri(workspaceSettings.Embedding.Endpoint),
            workspaceSettings.Embedding.DeploymentName,
            workspaceSettings.Embedding.Dimensions)));
builder.Services.AddSingleton<AzureSearchService>(services =>
    new AzureSearchService(
        services.GetRequiredService<IHttpClientFactory>().CreateClient("search"),
        services.GetRequiredService<TokenCredential>(),
        new AzureSearchOptions(
            new Uri(workspaceSettings.Search.Endpoint),
            workspaceSettings.Search.ApiVersion,
            workspaceSettings.Search.IndexName,
            workspaceSettings.Embedding.Dimensions),
        services.GetRequiredService<IEmbeddingService>()));
builder.Services.AddSingleton<IHybridSearchService>(
    services => services.GetRequiredService<AzureSearchService>());

var applicationInsightsConnectionString =
    builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
    builder.Services.AddOpenTelemetry()
        .UseAzureMonitor(options => options.ConnectionString = applicationInsightsConnectionString);
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
