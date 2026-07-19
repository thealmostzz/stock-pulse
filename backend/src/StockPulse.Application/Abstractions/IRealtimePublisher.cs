using StockPulse.Application.DTOs;

namespace StockPulse.Application.Abstractions;

public interface IRealtimePublisher
{
    Task PublishNewsCreatedAsync(NewsCreatedEvent message, CancellationToken cancellationToken);
}
