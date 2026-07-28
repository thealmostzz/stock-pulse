using StockPulse.Application.Abstractions;
using StockPulse.Application.DTOs;
using StockPulse.Application.Services;

namespace StockPulse.Application.Tests;

public sealed class NewsQueryServiceTests
{
#pragma warning disable CA1707 // Keep the descriptive test name required by the task specification.
    [Fact]
    public async Task GetLatestAsync_RejectsLimitAbove200()
    {
        var service = NewsQueryService.CreateForTest();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetLatestAsync(201, CancellationToken.None));
    }

    [Fact]
    public async Task QueryAsync_NormalizesTickerBeforeQueryingRepository()
    {
        var repository = new CapturingNewsRepository();
        var service = new NewsQueryService(repository);

        await service.QueryAsync(new NewsQueryRequest(" nvda ", null, null, null), CancellationToken.None);

        Assert.Equal("NVDA", repository.LastRequest!.Ticker);
    }

    [Fact]
    public async Task QueryAsync_NormalizesSourceCodeBeforeQueryingRepository()
    {
        var repository = new CapturingNewsRepository();
        var service = new NewsQueryService(repository);

        await service.QueryAsync(new NewsQueryRequest(null, " Mock ", null, null), CancellationToken.None);

        Assert.Equal("mock", repository.LastRequest!.SourceCode);
    }

    [Fact]
    public async Task QueryAsync_DefaultsMissingSortByToPublishedAt()
    {
        var repository = new CapturingNewsRepository();
        var service = new NewsQueryService(repository);

        await service.QueryAsync(new NewsQueryRequest(null, null, null, null), CancellationToken.None);

        Assert.Equal("publishedAt", repository.LastRequest!.SortBy);
    }

    [Fact]
    public async Task QueryAsync_NormalizesPublishedAtSortByToCanonicalValue()
    {
        var repository = new CapturingNewsRepository();
        var service = new NewsQueryService(repository);

        await service.QueryAsync(new NewsQueryRequest(null, null, null, null, SortBy: "publishedAt"), CancellationToken.None);

        Assert.Equal("publishedAt", repository.LastRequest!.SortBy);
    }

    [Fact]
    public async Task QueryAsync_NormalizesSortByToImpact()
    {
        var repository = new CapturingNewsRepository();
        var service = new NewsQueryService(repository);

        await service.QueryAsync(new NewsQueryRequest(null, null, null, null, SortBy: " IMPACT "), CancellationToken.None);

        Assert.Equal("impact", repository.LastRequest!.SortBy);
    }

    [Fact]
    public async Task QueryAsync_RejectsUnsupportedSortBy()
    {
        var service = NewsQueryService.CreateForTest();

        await Assert.ThrowsAsync<ArgumentException>(() => service.QueryAsync(
            new NewsQueryRequest(null, null, null, null, SortBy: "title"),
            CancellationToken.None));
    }

    [Fact]
    public async Task QueryAsync_RejectsUnsupportedSentiment()
    {
        var service = NewsQueryService.CreateForTest();

        await Assert.ThrowsAsync<ArgumentException>(() => service.QueryAsync(
            new NewsQueryRequest(null, null, "mixed", null),
            CancellationToken.None));
    }

    [Fact]
    public async Task QueryAsync_RejectsNumericSentiment()
    {
        var service = NewsQueryService.CreateForTest();

        await Assert.ThrowsAsync<ArgumentException>(() => service.QueryAsync(
            new NewsQueryRequest(null, null, "1", null),
            CancellationToken.None));
    }

    [Fact]
    public async Task QueryAsync_RejectsPageThatWouldOverflowSkip()
    {
        var service = NewsQueryService.CreateForTest();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.QueryAsync(
            new NewsQueryRequest(null, null, null, null, int.MaxValue, 2),
            CancellationToken.None));
    }
#pragma warning restore CA1707

    private sealed class CapturingNewsRepository : INewsRepository
    {
        public NewsQueryRequest? LastRequest { get; private set; }

        public Task<IReadOnlyList<NewsResponseDto>> GetLatestAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NewsResponseDto>>([]);

        public Task<PagedResponseDto<NewsResponseDto>> QueryAsync(NewsQueryRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new PagedResponseDto<NewsResponseDto>([], request.Page, request.PageSize, 0, false));
        }
    }
}
