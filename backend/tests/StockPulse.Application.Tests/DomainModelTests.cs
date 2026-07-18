using StockPulse.Domain.Entities;

namespace StockPulse.Application.Tests;

public sealed class DomainModelTests
{
#pragma warning disable CA1707 // Keep the descriptive test name required by the task specification.
    [Fact]
    public void StockNews_CreatesAnEmptyTickerCollection()
    {
        var news = new StockNews();

        Assert.Empty(news.Tickers);
    }
#pragma warning restore CA1707
}
