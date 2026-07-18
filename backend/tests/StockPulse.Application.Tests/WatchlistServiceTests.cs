using StockPulse.Application.DTOs;
using StockPulse.Application.Services;

namespace StockPulse.Application.Tests;

public sealed class WatchlistServiceTests
{
#pragma warning disable CA1707 // Keep the descriptive test name required by the task specification.
    [Fact]
    public async Task AddAsync_NormalizesTickerToUpperCase()
    {
        var service = WatchlistService.CreateForTest();

        var item = await service.AddAsync(new CreateWatchlistRequest(" nvda ", null, null), CancellationToken.None);

        Assert.Equal("NVDA", item.Ticker);
    }
#pragma warning restore CA1707
}
