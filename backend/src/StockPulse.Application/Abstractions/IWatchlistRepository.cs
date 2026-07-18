using StockPulse.Domain.Entities;

namespace StockPulse.Application.Abstractions;

public interface IWatchlistRepository
{
    Task<IReadOnlyList<WatchlistItem>> GetAllAsync(CancellationToken cancellationToken);
    Task<WatchlistItem> AddAsync(WatchlistItem item, CancellationToken cancellationToken);
    Task<bool> RemoveAsync(string ticker, CancellationToken cancellationToken);
}
