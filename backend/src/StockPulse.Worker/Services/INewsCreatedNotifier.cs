using StockPulse.Application.DTOs;

namespace StockPulse.Worker.Services;

public interface INewsCreatedNotifier
{
    Task NotifyAsync(NewsCreatedEvent message, CancellationToken cancellationToken);
}
