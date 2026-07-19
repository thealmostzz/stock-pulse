using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockPulse.Application.DTOs;
using StockPulse.Contracts.News;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Enums;
using StockPulse.Infrastructure.Persistence;
using StockPulse.Worker.Services;

namespace StockPulse.Worker.Pipelines;

public sealed class NewsIngestionPipeline(
    StockPulseDbContext dbContext,
    INewsCreatedNotifier notifier)
{
    public async Task IngestAsync(IReadOnlyList<NormalizedNewsDto> articles, CancellationToken cancellationToken)
    {
        var insertedNews = new List<StockNews>();
        var batchHashes = new HashSet<string>(StringComparer.Ordinal);
        var sources = new Dictionary<string, NewsSource>(StringComparer.OrdinalIgnoreCase);

        foreach (var article in articles)
        {
            var hash = CreateDedupHash(article.Title, article.ExternalUrl, article.PublishedAtUtc, article.SourceCode);
            if (!batchHashes.Add(hash) ||
                await dbContext.StockNews.AnyAsync(news => news.DedupHash == hash, cancellationToken))
            {
                continue;
            }

            var source = await GetSourceAsync(article.SourceCode, sources, cancellationToken);
            var news = CreateNews(article, source, hash);
            dbContext.StockNews.Add(news);
            insertedNews.Add(news);
        }

        if (insertedNews.Count == 0)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var news in insertedNews)
        {
            await notifier.NotifyAsync(CreateNewsCreatedEvent(news), cancellationToken);
        }
    }

    public static string CreateDedupHash(string title, string url, DateTimeOffset publishedAtUtc, string sourceCode)
    {
        var canonicalUrl = CanonicalizeUrl(url);
        var normalizedTitle = string.Join(' ', title.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var roundedTime = new DateTimeOffset(
            publishedAtUtc.Year,
            publishedAtUtc.Month,
            publishedAtUtc.Day,
            publishedAtUtc.Hour,
            publishedAtUtc.Minute,
            0,
            TimeSpan.Zero);
        var payload = $"{normalizedTitle}|{canonicalUrl}|{roundedTime:O}|{sourceCode.Trim().ToLowerInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private async Task<NewsSource> GetSourceAsync(
        string sourceCode,
        Dictionary<string, NewsSource> sources,
        CancellationToken cancellationToken)
    {
        var normalizedSourceCode = sourceCode.Trim().ToLowerInvariant();
        if (sources.TryGetValue(normalizedSourceCode, out var cachedSource))
        {
            return cachedSource;
        }

        var source = await dbContext.NewsSources.SingleOrDefaultAsync(
            item => item.SourceCode == normalizedSourceCode,
            cancellationToken);
        source ??= new NewsSource
        {
            SourceCode = normalizedSourceCode,
            SourceName = normalizedSourceCode,
            IsEnabled = true
        };

        if (source.Id == 0)
        {
            dbContext.NewsSources.Add(source);
        }

        sources[normalizedSourceCode] = source;
        return source;
    }

    private static StockNews CreateNews(NormalizedNewsDto article, NewsSource source, string hash)
    {
        var tickers = article.Tickers
            .Select(ticker => ticker.Trim().ToUpperInvariant())
            .Where(ticker => ticker.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var news = new StockNews
        {
            Source = source,
            ProviderNewsKey = article.ProviderNewsKey,
            ExternalUrl = article.ExternalUrl,
            CanonicalUrl = CanonicalizeUrl(article.ExternalUrl),
            Title = article.Title.Trim(),
            Summary = article.Summary?.Trim(),
            Sentiment = NewsSentiment.Neutral,
            SentimentScore = 0m,
            ImpactScore = 10m,
            PublishedAtUtc = article.PublishedAtUtc.ToUniversalTime(),
            ReceivedAtUtc = DateTimeOffset.UtcNow,
            DedupHash = hash,
            RawPayload = JsonDocument.Parse(article.RawPayload.RootElement.GetRawText()),
            Tags = []
        };

        for (var index = 0; index < tickers.Length; index++)
        {
            news.Tickers.Add(new StockNewsTicker
            {
                Ticker = tickers[index],
                ConfidenceScore = 1m,
                IsPrimary = index == 0
            });
        }

        return news;
    }

    private static NewsCreatedEvent CreateNewsCreatedEvent(StockNews news) =>
        new(
            DateTimeOffset.UtcNow,
            new NewsResponseDto(
                news.Id,
                news.Title,
                news.Summary,
                news.Source.SourceCode,
                news.ExternalUrl,
                news.PublishedAtUtc,
                news.Tickers.Select(ticker => ticker.Ticker).ToArray(),
                news.Sentiment.ToString(),
                news.ImpactScore,
                news.Tags));

    private static string CanonicalizeUrl(string url) =>
        url.Split('?', 2)[0].Trim().ToLowerInvariant();
}
