using StockPulse.Application.DTOs;

namespace StockPulse.Application.Abstractions;

public interface INewsRepository
{
    Task<IReadOnlyList<NewsResponseDto>> GetLatestAsync(int limit, CancellationToken cancellationToken);

    Task<PagedResponseDto<NewsResponseDto>> QueryAsync(NewsQueryRequest request, CancellationToken cancellationToken);
}
