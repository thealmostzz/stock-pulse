using System.Text.RegularExpressions;
using StockPulse.Application.Abstractions;
using StockPulse.Application.DTOs;
using StockPulse.Domain.Entities;

namespace StockPulse.Application.Services;

public sealed partial class WatchlistService(IWatchlistRepository repository)
{
    public static WatchlistService CreateForTest() => new(new InMemoryWatchlistRepository());

    public async Task<IReadOnlyList<WatchlistItemDto>> GetAllAsync(CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken))
        .Select(MapToDto)
        .ToArray();

    public async Task<WatchlistItemDto> AddAsync(CreateWatchlistRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticker = NormalizeTicker(request.Ticker);
        var item = await repository.AddAsync(
            new WatchlistItem
            {
                Ticker = ticker,
                DisplayName = request.DisplayName?.Trim(),
                Market = request.Market?.Trim()
            },
            cancellationToken);

        return MapToDto(item);
    }

    public Task<bool> RemoveAsync(string ticker, CancellationToken cancellationToken) =>
        repository.RemoveAsync(NormalizeTicker(ticker), cancellationToken);

    private static WatchlistItemDto MapToDto(WatchlistItem item) =>
        new(item.Id, item.Ticker, item.DisplayName, item.Market, item.SortOrder, item.IsActive);

    private static string NormalizeTicker(string ticker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);

        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        if (!TickerPattern().IsMatch(normalizedTicker))
        {
            throw new ArgumentException("Ticker format is invalid.", nameof(ticker));
        }

        return normalizedTicker;
    }

    [GeneratedRegex("^[A-Z][A-Z0-9.-]{0,19}$")]
    private static partial Regex TickerPattern();

    private sealed class InMemoryWatchlistRepository : IWatchlistRepository
    {
        private readonly List<WatchlistItem> items = [];

        public Task<IReadOnlyList<WatchlistItem>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WatchlistItem>>(items);

        public Task<WatchlistItem> AddAsync(WatchlistItem item, CancellationToken cancellationToken)
        {
            item.Id = items.Count + 1;
            items.Add(item);
            return Task.FromResult(item);
        }

        public Task<bool> RemoveAsync(string ticker, CancellationToken cancellationToken) =>
            Task.FromResult(items.RemoveAll(item => item.Ticker == ticker) > 0);
    }
}
