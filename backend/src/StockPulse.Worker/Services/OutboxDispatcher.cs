using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockPulse.Application.DTOs;
using StockPulse.Infrastructure.Persistence;

namespace StockPulse.Worker.Services;

public sealed partial class OutboxDispatcher(
    StockPulseDbContext dbContext,
    INewsCreatedNotifier notifier,
    ILogger<OutboxDispatcher> logger)
{
    private const int MaximumErrorLength = 1000;

    public async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var pendingEvents = await dbContext.NewsOutboxEvents
            .Where(outboxEvent => outboxEvent.DeliveredAtUtc == null && outboxEvent.NextAttemptAtUtc <= now)
            .OrderBy(outboxEvent => outboxEvent.NextAttemptAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var outboxEvent in pendingEvents)
        {
            try
            {
                var message = outboxEvent.Payload.Deserialize<NewsCreatedEvent>()
                    ?? throw new JsonException("Outbox payload does not contain a news-created event.");
                await notifier.NotifyAsync(outboxEvent.EventId, message, cancellationToken);
                outboxEvent.DeliveredAtUtc = DateTimeOffset.UtcNow;
                outboxEvent.LastError = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                outboxEvent.AttemptCount++;
                outboxEvent.LastError = TruncateError(exception);
                outboxEvent.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(
                    Math.Min(300, 1 << Math.Min(outboxEvent.AttemptCount, 8)));
                LogDispatchFailed(logger, exception, outboxEvent.EventId);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string TruncateError(Exception exception) =>
        exception.ToString()[..Math.Min(exception.ToString().Length, MaximumErrorLength)];

    [LoggerMessage(Level = LogLevel.Warning, Message = "Realtime outbox dispatch failed for event {EventId}.")]
    private static partial void LogDispatchFailed(ILogger logger, Exception exception, Guid eventId);
}
