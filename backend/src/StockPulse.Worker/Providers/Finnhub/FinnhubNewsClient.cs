using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockPulse.Contracts.News;
using StockPulse.Infrastructure.Persistence;
using StockPulse.Worker.Configuration;

namespace StockPulse.Worker.Providers.Finnhub;

public sealed class FinnhubNewsClient : IProviderNewsClient
{
    private const int MaximumTickersPerPollingCycle = 20;
    private static readonly TimeSpan RequestDelay = TimeSpan.FromSeconds(1);
    private static readonly Action<ILogger, int, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<int, string>(
            LogLevel.Warning,
            new EventId(1, "FinnhubRequestFailure"),
            "Finnhub company news request failed with status code {StatusCode} for ticker {Ticker}.");
    private static readonly Action<ILogger, string, Exception?> LogProcessingFailure =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, "FinnhubProcessingFailure"),
            "Finnhub company news request could not be processed for ticker {Ticker}.");

    private readonly StockPulseDbContext dbContext;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly string apiKey;
    private readonly ILogger<FinnhubNewsClient> logger;
    private readonly TimeProvider timeProvider;

    public FinnhubNewsClient(
        StockPulseDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IOptions<FinnhubOptions> finnhubOptions,
        ILogger<FinnhubNewsClient> logger,
        TimeProvider? timeProvider = null)
    {
        this.dbContext = dbContext;
        this.httpClientFactory = httpClientFactory;
        apiKey = finnhubOptions.Value.ApiKey
            ?? throw new InvalidOperationException("Finnhub:ApiKey must be configured when mock providers are disabled.");
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string SourceCode => "finnhub";

    public async Task<IReadOnlyList<NormalizedNewsDto>> FetchNewsAsync(CancellationToken cancellationToken)
    {
        var tickers = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Ticker)
            .Select(item => item.Ticker)
            .Take(MaximumTickersPerPollingCycle)
            .ToListAsync(cancellationToken);

        if (tickers.Count == 0)
        {
            return [];
        }

        var currentUtcDate = timeProvider.GetUtcNow().UtcDateTime.Date;
        var latestPublishedAtUtcByTicker = await dbContext.StockNews
            .AsNoTracking()
            .SelectMany(
                stockNews => stockNews.Tickers,
                (stockNews, stockNewsTicker) => new { stockNewsTicker.Ticker, stockNews.PublishedAtUtc })
            .Where(item => tickers.Contains(item.Ticker))
            .GroupBy(item => item.Ticker)
            .Select(group => new { Ticker = group.Key, PublishedAtUtc = group.Max(item => item.PublishedAtUtc) })
            .ToDictionaryAsync(item => item.Ticker, item => item.PublishedAtUtc, cancellationToken);
        var client = httpClientFactory.CreateClient("Finnhub");
        var articles = new List<NormalizedNewsDto>();

        for (var index = 0; index < tickers.Count; index++)
        {
            var ticker = tickers[index];
            var fromDate = latestPublishedAtUtcByTicker.TryGetValue(ticker, out var latestPublishedAtUtc)
                ? latestPublishedAtUtc.UtcDateTime.Date
                : currentUtcDate.AddDays(-7);

            try
            {
                using var response = await client.GetAsync(
                    CreateCompanyNewsRequestUri(ticker, fromDate, currentUtcDate),
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    LogRequestFailure(logger, (int)response.StatusCode, ticker, null);
                }
                else
                {
                    articles.AddRange(await ParseResponseAsync(response, ticker, cancellationToken));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException)
            {
                LogProcessingFailure(logger, ticker, exception);
            }

            if (index < tickers.Count - 1)
            {
                await Task.Delay(RequestDelay, timeProvider, cancellationToken);
            }
        }

        return articles;
    }

    private string CreateCompanyNewsRequestUri(string ticker, DateTime fromDate, DateTime toDate)
    {
        var escapedTicker = Uri.EscapeDataString(ticker);
        var escapedFromDate = Uri.EscapeDataString(fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var escapedToDate = Uri.EscapeDataString(toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var escapedApiKey = Uri.EscapeDataString(apiKey);

        return $"company-news?symbol={escapedTicker}&from={escapedFromDate}&to={escapedToDate}&token={escapedApiKey}";
    }

    private static async Task<IEnumerable<NormalizedNewsDto>> ParseResponseAsync(
        HttpResponseMessage response,
        string ticker,
        CancellationToken cancellationToken)
    {
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var articles = new List<NormalizedNewsDto>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!TryMapArticle(item, ticker, out var article))
            {
                continue;
            }

            articles.Add(article);
        }

        return articles;
    }

    private static bool TryMapArticle(JsonElement item, string ticker, out NormalizedNewsDto article)
    {
        article = null!;

        if (item.ValueKind != JsonValueKind.Object ||
            !TryGetProviderNewsKey(item, out var providerNewsKey) ||
            !TryGetNonEmptyString(item, "headline", out var headline) ||
            !TryGetHttpsUrl(item, out var externalUrl) ||
            !TryGetPublishedAtUtc(item, out var publishedAtUtc))
        {
            return false;
        }

        var summary = item.TryGetProperty("summary", out var summaryElement) && summaryElement.ValueKind == JsonValueKind.String
            ? summaryElement.GetString()
            : null;
        article = new NormalizedNewsDto(
            "finnhub",
            providerNewsKey,
            externalUrl,
            headline,
            summary,
            publishedAtUtc,
            [ticker],
            JsonDocument.Parse(item.GetRawText()));
        return true;
    }

    private static bool TryGetProviderNewsKey(JsonElement item, out string providerNewsKey)
    {
        providerNewsKey = string.Empty;
        if (!item.TryGetProperty("id", out var idElement))
        {
            return false;
        }

        providerNewsKey = idElement.ValueKind switch
        {
            JsonValueKind.String => idElement.GetString() ?? string.Empty,
            JsonValueKind.Number => idElement.GetRawText(),
            _ => string.Empty
        };
        return !string.IsNullOrWhiteSpace(providerNewsKey);
    }

    private static bool TryGetNonEmptyString(JsonElement item, string propertyName, out string value)
    {
        value = string.Empty;
        if (!item.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetHttpsUrl(JsonElement item, out string externalUrl)
    {
        externalUrl = string.Empty;
        if (!TryGetNonEmptyString(item, "url", out var url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        externalUrl = uri.AbsoluteUri;
        return true;
    }

    private static bool TryGetPublishedAtUtc(JsonElement item, out DateTimeOffset publishedAtUtc)
    {
        publishedAtUtc = default;
        if (!item.TryGetProperty("datetime", out var datetimeElement) ||
            !datetimeElement.TryGetInt64(out var unixSeconds) ||
            unixSeconds <= 0)
        {
            return false;
        }

        try
        {
            publishedAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
