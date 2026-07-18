using Microsoft.EntityFrameworkCore;
using StockPulse.Application.Abstractions;
using StockPulse.Domain.Entities;

namespace StockPulse.Infrastructure.Persistence.Repositories;

public sealed class WatchlistRepository(StockPulseDbContext dbContext) : IWatchlistRepository
{
    public async Task<IReadOnlyList<WatchlistItem>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.WatchlistItems
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Ticker)
            .ToListAsync(cancellationToken);

    public async Task<WatchlistItem> AddAsync(WatchlistItem item, CancellationToken cancellationToken)
    {
        dbContext.WatchlistItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<bool> RemoveAsync(string ticker, CancellationToken cancellationToken)
    {
        var item = await dbContext.WatchlistItems
            .SingleOrDefaultAsync(value => value.Ticker == ticker, cancellationToken);

        if (item is null)
        {
            return false;
        }

        dbContext.WatchlistItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
