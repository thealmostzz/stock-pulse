using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using StockPulse.Application.Abstractions;
using StockPulse.Application.DTOs;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Enums;

namespace StockPulse.Infrastructure.Persistence.Repositories;

public sealed class NewsRepository(StockPulseDbContext dbContext) : INewsRepository
{
    public async Task<IReadOnlyList<NewsResponseDto>> GetLatestAsync(int limit, CancellationToken cancellationToken)
    {
        var news = await CreateBaseQuery()
            .OrderByDescending(news => news.PublishedAtUtc)
            .Take(limit)
            .Select(ProjectNews())
            .ToListAsync(cancellationToken);

        return news.Select(MapToDto).ToArray();
    }

    public async Task<PagedResponseDto<NewsResponseDto>> QueryAsync(NewsQueryRequest request, CancellationToken cancellationToken)
    {
        var query = CreateBaseQuery();

        if (request.Ticker is not null)
        {
            query = query.Where(news => news.Tickers.Any(ticker => ticker.Ticker == request.Ticker));
        }

        if (!string.IsNullOrWhiteSpace(request.SourceCode))
        {
            query = query.Where(news => news.Source.SourceCode == request.SourceCode.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.Sentiment) &&
            Enum.TryParse<NewsSentiment>(request.Sentiment, true, out var sentiment))
        {
            query = query.Where(news => news.Sentiment == sentiment);
        }

        if (!string.IsNullOrWhiteSpace(request.Tag))
        {
            query = query.Where(news => news.Tags.Contains(request.Tag.Trim()));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(news => news.PublishedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ProjectNews())
            .ToListAsync(cancellationToken);

        return new PagedResponseDto<NewsResponseDto>(
            items.Select(MapToDto).ToArray(),
            request.Page,
            request.PageSize,
            totalCount,
            request.Page * request.PageSize < totalCount);
    }

    private IQueryable<StockNews> CreateBaseQuery() =>
        dbContext.StockNews
            .AsNoTracking();

    private static Expression<Func<StockNews, NewsProjection>> ProjectNews() =>
        news => new NewsProjection(
                news.Id,
                news.Title,
                news.Summary,
                news.Source.SourceCode,
                news.ExternalUrl,
                news.PublishedAtUtc,
                news.Tickers.Select(ticker => ticker.Ticker).ToList(),
                news.Sentiment,
                news.ImpactScore,
                news.Tags);

    private static NewsResponseDto MapToDto(NewsProjection news) =>
        new(
            news.Id,
            news.Title,
            news.Summary,
            news.SourceCode,
            news.Url,
            news.PublishedAtUtc,
            news.Tickers,
            news.Sentiment.ToString(),
            news.ImpactScore,
            news.Tags);

    private sealed record NewsProjection(
        long Id,
        string Title,
        string? Summary,
        string SourceCode,
        string Url,
        DateTimeOffset PublishedAtUtc,
        IReadOnlyList<string> Tickers,
        NewsSentiment Sentiment,
        decimal ImpactScore,
        IReadOnlyList<string> Tags);
}
