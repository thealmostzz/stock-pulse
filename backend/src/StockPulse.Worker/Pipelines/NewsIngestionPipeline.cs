using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockPulse.Application.DTOs;
using StockPulse.Contracts.News;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Enums;
using StockPulse.Infrastructure.Persistence;
using StockPulse.Worker.Services;

namespace StockPulse.Worker.Pipelines;

public sealed class NewsIngestionPipeline(
    StockPulseDbContext dbContext)
{
    public async Task IngestAsync(IReadOnlyList<NormalizedNewsDto> articles, CancellationToken cancellationToken)
    {
        var batchHashes = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<(NormalizedNewsDto Article, string Hash)>();

        foreach (var article in articles)
        {
            var hash = CreateDedupHash(article.Title, article.ExternalUrl, article.PublishedAtUtc, article.SourceCode);
            if (batchHashes.Add(hash))
            {
                candidates.Add((article, hash));
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var existingHashes = await dbContext.StockNews
            .Where(news => batchHashes.Contains(news.DedupHash))
            .Select(news => news.DedupHash)
            .ToHashSetAsync(cancellationToken);
        var sourceCodes = candidates
            .Where(candidate => !existingHashes.Contains(candidate.Hash))
            .Select(candidate => candidate.Article.SourceCode.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        await EnsureSourcesExistAsync(sourceCodes, cancellationToken);
        var sourceIds = await dbContext.NewsSources
            .Where(source => sourceCodes.Contains(source.SourceCode))
            .ToDictionaryAsync(source => source.SourceCode, source => source.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var pendingCandidates = candidates
            .Where(candidate => !existingHashes.Contains(candidate.Hash))
            .ToArray();
        foreach (var (article, hash) in pendingCandidates)
        {
            AddNewsWithOutboxEvent(article, hash, sourceIds);
        }

        if (pendingCandidates.Length == 0)
        {
            return;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            await SaveCandidatesAfterConcurrentConflictAsync(pendingCandidates, sourceIds, cancellationToken);
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

    private async Task EnsureSourcesExistAsync(string[] sourceCodes, CancellationToken cancellationToken)
    {
        if (sourceCodes.Length == 0)
        {
            return;
        }

        var parameters = new List<NpgsqlParameter>(sourceCodes.Length * 2);
        var values = new List<string>(sourceCodes.Length);
        for (var index = 0; index < sourceCodes.Length; index++)
        {
            var sourceCode = sourceCodes[index];
            var codeParameterName = $"sourceCode{index}";
            var nameParameterName = $"sourceName{index}";
            parameters.Add(new NpgsqlParameter(codeParameterName, sourceCode));
            parameters.Add(new NpgsqlParameter(nameParameterName, sourceCode));
            values.Add($"(@{codeParameterName}, @{nameParameterName}, TRUE)");
        }

#pragma warning disable EF1002 // Only generated parameter names are interpolated; every source value is an NpgsqlParameter.
        await dbContext.Database.ExecuteSqlRawAsync(
            $"INSERT INTO \"NewsSources\" (\"SourceCode\", \"SourceName\", \"IsEnabled\") VALUES {string.Join(", ", values)} ON CONFLICT (\"SourceCode\") DO NOTHING",
            parameters,
            cancellationToken);
#pragma warning restore EF1002
    }

    private void AddNewsWithOutboxEvent(
        NormalizedNewsDto article,
        string hash,
        IReadOnlyDictionary<string, short> sourceIds)
    {
        var normalizedSourceCode = article.SourceCode.Trim().ToLowerInvariant();
        var news = CreateNews(article, sourceIds[normalizedSourceCode], hash);
        var eventId = Guid.NewGuid();
        dbContext.StockNews.Add(news);
        dbContext.NewsOutboxEvents.Add(new NewsOutboxEvent
        {
            EventId = eventId,
            News = news,
            Payload = JsonSerializer.SerializeToDocument(CreateNewsCreatedEvent(eventId, news, normalizedSourceCode)),
            NextAttemptAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private async Task SaveCandidatesAfterConcurrentConflictAsync(
        IReadOnlyList<(NormalizedNewsDto Article, string Hash)> candidates,
        IReadOnlyDictionary<string, short> sourceIds,
        CancellationToken cancellationToken)
    {
        var currentHashes = await dbContext.StockNews
            .Where(news => candidates.Select(candidate => candidate.Hash).Contains(news.DedupHash))
            .Select(news => news.DedupHash)
            .ToHashSetAsync(cancellationToken);

        foreach (var candidate in candidates.Where(candidate => !currentHashes.Contains(candidate.Hash)))
        {
            AddNewsWithOutboxEvent(candidate.Article, candidate.Hash, sourceIds);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static StockNews CreateNews(NormalizedNewsDto article, short sourceId, string hash)
    {
        var tickers = article.Tickers
            .Select(ticker => ticker.Trim().ToUpperInvariant())
            .Where(ticker => ticker.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var news = new StockNews
        {
            SourceId = sourceId,
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

    private static NewsCreatedEvent CreateNewsCreatedEvent(Guid eventId, StockNews news, string sourceCode) =>
        new(
            eventId,
            DateTimeOffset.UtcNow,
            new NewsResponseDto(
                news.Id,
                news.Title,
                news.Summary,
                sourceCode,
                news.ExternalUrl,
                news.PublishedAtUtc,
                news.Tickers.Select(ticker => ticker.Ticker).ToArray(),
                news.Sentiment.ToString(),
                news.ImpactScore,
                news.Tags));

    private static string CanonicalizeUrl(string url) =>
        url.Split('?', 2)[0].Trim().ToLowerInvariant();
}
