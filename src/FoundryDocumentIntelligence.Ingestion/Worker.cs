namespace FoundryDocumentIntelligence.Ingestion;

public sealed class Worker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
}
