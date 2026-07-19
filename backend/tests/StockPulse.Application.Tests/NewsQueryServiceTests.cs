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
