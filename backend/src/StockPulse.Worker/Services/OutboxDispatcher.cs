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
    private const int MaximumBatchSize = 100;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    public async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var lockToken = Guid.NewGuid();
        var lockedUntilUtc = now.Add(LeaseDuration);
        await dbContext.NewsOutboxEvents
            .Where(outboxEvent => outboxEvent.DeliveredAtUtc == null &&
                outboxEvent.NextAttemptAtUtc <= now &&
                (outboxEvent.LockedUntilUtc == null || outboxEvent.LockedUntilUtc <= now))
            .OrderBy(outboxEvent => outboxEvent.NextAttemptAtUtc)
            .Take(MaximumBatchSize)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(outboxEvent => outboxEvent.LockToken, lockToken)
                .SetProperty(outboxEvent => outboxEvent.LockedUntilUtc, lockedUntilUtc), cancellationToken);
        dbContext.ChangeTracker.Clear();
        var pendingEvents = await dbContext.NewsOutboxEvents
            .Where(outboxEvent => outboxEvent.LockToken == lockToken)
            .OrderBy(outboxEvent => outboxEvent.NextAttemptAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var outboxEvent in pendingEvents)
        {
            try
            {
                var message = outboxEvent.Payload.Deserialize<NewsCreatedEvent>()
                    ?? throw new JsonException("Outbox payload does not contain a news-created event.");
                await notifier.NotifyAsync(outboxEvent.EventId, message, cancellationToken);
                var delivered = await MarkDeliveredAsync(outboxEvent.EventId, lockToken, cancellationToken);
                if (!delivered)
                {
                    LogLeaseLost(logger, outboxEvent.EventId);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var retried = await ScheduleRetryAsync(outboxEvent.EventId, lockToken, outboxEvent.AttemptCount, exception, cancellationToken);
                if (!retried)
                {
                    LogLeaseLost(logger, outboxEvent.EventId);
                }
                LogDispatchFailed(logger, exception, outboxEvent.EventId);
            }
        }
    }

    private async Task<bool> MarkDeliveredAsync(Guid eventId, Guid lockToken, CancellationToken cancellationToken) =>
        await dbContext.NewsOutboxEvents
            .Where(outboxEvent => outboxEvent.EventId == eventId && outboxEvent.LockToken == lockToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(outboxEvent => outboxEvent.DeliveredAtUtc, DateTimeOffset.UtcNow)
                .SetProperty(outboxEvent => outboxEvent.LastError, (string?)null)
                .SetProperty(outboxEvent => outboxEvent.LockToken, (Guid?)null)
                .SetProperty(outboxEvent => outboxEvent.LockedUntilUtc, (DateTimeOffset?)null), cancellationToken) == 1;

    private async Task<bool> ScheduleRetryAsync(
        Guid eventId,
        Guid lockToken,
        int attemptCount,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var nextAttemptCount = attemptCount + 1;
        var nextAttemptAtUtc = now.AddSeconds(Math.Min(300, 1 << Math.Min(nextAttemptCount, 8)));
        return await dbContext.NewsOutboxEvents
            .Where(outboxEvent => outboxEvent.EventId == eventId && outboxEvent.LockToken == lockToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(outboxEvent => outboxEvent.AttemptCount, outboxEvent => outboxEvent.AttemptCount + 1)
                .SetProperty(outboxEvent => outboxEvent.LastError, TruncateError(exception))
                .SetProperty(outboxEvent => outboxEvent.NextAttemptAtUtc, nextAttemptAtUtc)
                .SetProperty(outboxEvent => outboxEvent.LockToken, (Guid?)null)
                .SetProperty(outboxEvent => outboxEvent.LockedUntilUtc, (DateTimeOffset?)null), cancellationToken) == 1;
    }

    private static string TruncateError(Exception exception) =>
        exception.ToString()[..Math.Min(exception.ToString().Length, MaximumErrorLength)];

    [LoggerMessage(Level = LogLevel.Warning, Message = "Realtime outbox dispatch failed for event {EventId}.")]
    private static partial void LogDispatchFailed(ILogger logger, Exception exception, Guid eventId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Realtime outbox lease was lost before finalizing event {EventId}.")]
    private static partial void LogLeaseLost(ILogger logger, Guid eventId);
}
