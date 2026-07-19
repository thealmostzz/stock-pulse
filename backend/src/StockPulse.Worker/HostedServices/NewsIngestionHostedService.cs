using StockPulse.Worker.Pipelines;
using StockPulse.Worker.Providers;
using StockPulse.Worker.Services;

namespace StockPulse.Worker.HostedServices;

public sealed partial class NewsIngestionHostedService(
    IServiceScopeFactory scopeFactory,
    IEnumerable<IProviderNewsClient> providers,
    ILogger<NewsIngestionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        do
        {
            try
            {
                await IngestPollingCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogPollingCycleFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task IngestPollingCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<NewsIngestionPipeline>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();

        await dispatcher.DispatchPendingAsync(cancellationToken);

        foreach (var provider in providers)
        {
            var articles = await provider.FetchNewsAsync(cancellationToken);
            await pipeline.IngestAsync(articles, cancellationToken);
        }

    }

    [LoggerMessage(Level = LogLevel.Error, Message = "News ingestion polling cycle failed.")]
    private static partial void LogPollingCycleFailed(ILogger logger, Exception exception);
}
