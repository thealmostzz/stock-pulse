using StockPulse.Application.DTOs;

namespace StockPulse.Worker.Services;

public interface INewsCreatedNotifier
{
    Task NotifyAsync(Guid eventId, NewsCreatedEvent message, CancellationToken cancellationToken);
}
