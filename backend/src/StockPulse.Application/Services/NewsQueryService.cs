using StockPulse.Application.Abstractions;
using StockPulse.Application.DTOs;

namespace StockPulse.Application.Services;

public sealed class NewsQueryService(INewsRepository repository)
{
    public static NewsQueryService CreateForTest() => new(new EmptyNewsRepository());

    public Task<IReadOnlyList<NewsResponseDto>> GetLatestAsync(int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        return repository.GetLatestAsync(limit, cancellationToken);
    }

    public Task<PagedResponseDto<NewsResponseDto>> QueryAsync(NewsQueryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        if (request.PageSize is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        var normalizedRequest = request.Ticker is null
            ? request
            : request with { Ticker = TickerNormalizer.Normalize(request.Ticker) };

        return repository.QueryAsync(normalizedRequest, cancellationToken);
    }

    private sealed class EmptyNewsRepository : INewsRepository
    {
        public Task<IReadOnlyList<NewsResponseDto>> GetLatestAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NewsResponseDto>>([]);

        public Task<PagedResponseDto<NewsResponseDto>> QueryAsync(NewsQueryRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponseDto<NewsResponseDto>([], request.Page, request.PageSize, 0, false));
    }
}
