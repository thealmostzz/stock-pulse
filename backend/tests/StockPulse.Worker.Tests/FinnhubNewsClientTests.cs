using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using StockPulse.Domain.Entities;
using StockPulse.Infrastructure.Persistence;
using StockPulse.Worker.Configuration;
using StockPulse.Worker.Providers.Finnhub;

namespace StockPulse.Worker.Tests;

public sealed class FinnhubNewsClientTests
{
#pragma warning disable CA1707 // Keep descriptive test names consistent with the task specification.
    [Fact]
    public async Task FetchNewsAsync_EmptyWatchlist_ReturnsEmptyListWithoutHttpRequests()
    {
        var schemaName = await CreateSchemaAsync();

        try
        {
            await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
            using var testClient = CreateClient(dbContext);

            var news = await testClient.Client.FetchNewsAsync(CancellationToken.None);

            Assert.Empty(news);
            Assert.Empty(testClient.Handler.RequestUris);
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    [Fact]
    public async Task FetchNewsAsync_ActiveAapl_MapsValidResponseAndSkipsInactiveWatchlistItem()
    {
        var schemaName = await CreateSchemaAsync();

        try
        {
            await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
            dbContext.WatchlistItems.AddRange(
                CreateWatchlistItem("AAPL", 1, true),
                CreateWatchlistItem("NVDA", 2, false));
            await dbContext.SaveChangesAsync();
            using var testClient = CreateClient(
                dbContext,
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "[{\"category\":\"company\",\"datetime\":1760515200,\"headline\":\"Apple earnings rise\",\"id\":12345,\"image\":\"https://image.example/aapl.png\",\"related\":\"AAPL\",\"source\":\"Example\",\"summary\":\"Quarterly summary\",\"url\":\"https://news.example/aapl\"}]",
                        Encoding.UTF8,
                        "application/json")
                });

            var news = await testClient.Client.FetchNewsAsync(CancellationToken.None);

            var article = Assert.Single(news);
            Assert.Equal("finnhub", article.SourceCode);
            Assert.Equal("12345", article.ProviderNewsKey);
            Assert.Equal(["AAPL"], article.Tickers);
            Assert.Equal(new DateTimeOffset(2025, 10, 15, 8, 0, 0, TimeSpan.Zero), article.PublishedAtUtc);
            Assert.Equal("Apple earnings rise", article.Title);
            Assert.Equal("Quarterly summary", article.Summary);
            Assert.Equal("https://news.example/aapl", article.ExternalUrl);
            Assert.Equal(12345, article.RawPayload.RootElement.GetProperty("id").GetInt64());
            var requestUri = Assert.Single(testClient.Handler.RequestUris);
            Assert.Equal("/api/v1/company-news", requestUri.AbsolutePath);
            Assert.Equal("AAPL", GetQueryValue(requestUri, "symbol"));
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    [Fact]
    public async Task FetchNewsAsync_MoreThanTwentyActiveTickers_RequestsOnlyFirstTwentyInWatchlistOrder()
    {
        var schemaName = await CreateSchemaAsync();

        try
        {
            await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
            dbContext.WatchlistItems.AddRange(Enumerable.Range(1, 21)
                .Select(index => CreateWatchlistItem($"T{index:D2}", index, true)));
            await dbContext.SaveChangesAsync();
            using var testClient = CreateClient(dbContext, Enumerable.Range(0, 20).Select(_ => CreateJsonResponse("[]")));

            var news = await testClient.Client.FetchNewsAsync(CancellationToken.None);

            Assert.Empty(news);
            Assert.Equal(20, testClient.Handler.RequestUris.Count);
            Assert.Equal(
                Enumerable.Range(1, 20).Select(index => $"T{index:D2}"),
                testClient.Handler.RequestUris.Select(requestUri => GetQueryValue(requestUri, "symbol")));
            Assert.DoesNotContain(testClient.Handler.RequestUris, requestUri => GetQueryValue(requestUri, "symbol") == "T21");
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    [Fact]
    public async Task FetchNewsAsync_WhenFirstResponseIsThrottled_LogsSafelyAndFetchesSecondTicker()
    {
        var schemaName = await CreateSchemaAsync();

        try
        {
            await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
            dbContext.WatchlistItems.AddRange(
                CreateWatchlistItem("AAPL", 1, true),
                CreateWatchlistItem("MSFT", 2, true));
            await dbContext.SaveChangesAsync();
            using var testClient = CreateClient(
                dbContext,
                new HttpResponseMessage(HttpStatusCode.TooManyRequests),
                CreateJsonResponse("[{\"category\":\"company\",\"datetime\":1760515200,\"headline\":\"Microsoft update\",\"id\":7,\"image\":\"https://image.example/msft.png\",\"related\":\"MSFT\",\"source\":\"Example\",\"summary\":\"Summary\",\"url\":\"https://news.example/msft\"}]"));

            var news = await testClient.Client.FetchNewsAsync(CancellationToken.None);

            var article = Assert.Single(news);
            Assert.Equal("MSFT", Assert.Single(article.Tickers));
            Assert.Equal(2, testClient.Handler.RequestUris.Count);
            var loggedFailure = Assert.Single(testClient.Logger.Messages);
            Assert.Contains("429", loggedFailure, StringComparison.Ordinal);
            Assert.DoesNotContain("company-news", loggedFailure, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret-finnhub-token", loggedFailure, StringComparison.Ordinal);
            Assert.Equal([TimeSpan.FromSeconds(1)], testClient.TimeProvider.Delays);
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    [Fact]
    public async Task FetchNewsAsync_WhenFirstRequestTimesOut_LogsTimeoutAndFetchesSecondTicker()
    {
        var schemaName = await CreateSchemaAsync();

        try
        {
            await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
            dbContext.WatchlistItems.AddRange(
                CreateWatchlistItem("AAPL", 1, true),
                CreateWatchlistItem("MSFT", 2, true));
            await dbContext.SaveChangesAsync();
            using var testClient = CreateClient(
                dbContext,
                (requestNumber, _, _) => requestNumber == 1
                    ? Task.FromException<HttpResponseMessage>(new OperationCanceledException("provider timeout"))
                    : Task.FromResult(CreateJsonResponse("[{\"datetime\":1760515200,\"headline\":\"Microsoft update\",\"id\":7,\"summary\":\"Summary\",\"url\":\"https://news.example/msft\"}]")));

            var news = await testClient.Client.FetchNewsAsync(CancellationToken.None);

            var article = Assert.Single(news);
            Assert.Equal("MSFT", Assert.Single(article.Tickers));
            var timeoutLog = Assert.Single(testClient.Logger.Entries);
            Assert.Contains("timeout", timeoutLog.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(timeoutLog.Exception);
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    [Fact]
    public async Task FetchNewsAsync_WhenTransportExceptionContainsRequestData_LogsSanitizedErrorAndFetchesSecondTicker()
    {
        var schemaName = await CreateSchemaAsync();

        try
        {
            await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
            dbContext.WatchlistItems.AddRange(
                CreateWatchlistItem("AAPL", 1, true),
                CreateWatchlistItem("MSFT", 2, true));
            await dbContext.SaveChangesAsync();
            using var testClient = CreateClient(
                dbContext,
                (requestNumber, _, _) => requestNumber == 1
                    ? Task.FromException<HttpResponseMessage>(new HttpRequestException("GET https://finnhub.io/api/v1/company-news?token=secret-finnhub-token"))
                    : Task.FromResult(CreateJsonResponse("[{\"datetime\":1760515200,\"headline\":\"Microsoft update\",\"id\":7,\"summary\":\"Summary\",\"url\":\"https://news.example/msft\"}]")));

            var news = await testClient.Client.FetchNewsAsync(CancellationToken.None);

            var article = Assert.Single(news);
            Assert.Equal("MSFT", Assert.Single(article.Tickers));
            var transportLog = Assert.Single(testClient.Logger.Entries);
            Assert.Contains("transport", transportLog.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("company-news", transportLog.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret-finnhub-token", transportLog.Message, StringComparison.Ordinal);
            Assert.Null(transportLog.Exception);
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    [Fact]
    public async Task FetchNewsAsync_UsesLatestPersistedUtcDateOrSevenDaysBeforeToday()
    {
        var schemaName = await CreateSchemaAsync();

        try
        {
            await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
            dbContext.WatchlistItems.AddRange(
                CreateWatchlistItem("AAPL", 1, true),
                CreateWatchlistItem("MSFT", 2, true));
            await SeedPersistedNewsAsync(dbContext, "AAPL", new DateTimeOffset(2026, 7, 12, 23, 59, 0, TimeSpan.Zero));
            await SeedPersistedNewsAsync(dbContext, "AAPL", new DateTimeOffset(2026, 7, 13, 0, 1, 0, TimeSpan.Zero));
            await dbContext.SaveChangesAsync();
            using var testClient = CreateClient(dbContext, [CreateJsonResponse("[]"), CreateJsonResponse("[]")]);

            await testClient.Client.FetchNewsAsync(CancellationToken.None);

            var aaplRequest = testClient.Handler.RequestUris.Single(requestUri => GetQueryValue(requestUri, "symbol") == "AAPL");
            var msftRequest = testClient.Handler.RequestUris.Single(requestUri => GetQueryValue(requestUri, "symbol") == "MSFT");
            Assert.Equal("2026-07-13", GetQueryValue(aaplRequest, "from"));
            Assert.Equal("2026-07-22", GetQueryValue(msftRequest, "from"));
            Assert.All(testClient.Handler.RequestUris, requestUri => Assert.Equal("2026-07-29", GetQueryValue(requestUri, "to")));
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    [Fact]
    public async Task FetchNewsAsync_SkipsMalformedItemsAndKeepsOtherValidItems()
    {
        var schemaName = await CreateSchemaAsync();

        try
        {
            await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
            dbContext.WatchlistItems.Add(CreateWatchlistItem("AAPL", 1, true));
            await dbContext.SaveChangesAsync();
            using var testClient = CreateClient(
                dbContext,
                CreateJsonResponse(
                    "[{\"datetime\":1760515200,\"headline\":\"Missing id\",\"url\":\"https://news.example/missing-id\"},{\"id\":2,\"datetime\":1760515200,\"headline\":\"\",\"url\":\"https://news.example/empty-headline\"},{\"id\":3,\"datetime\":1760515200,\"headline\":\"HTTP URL\",\"url\":\"http://news.example/http\"},{\"id\":4,\"datetime\":0,\"headline\":\"Bad datetime\",\"url\":\"https://news.example/bad-datetime\"},{\"id\":5,\"datetime\":1760515200,\"headline\":\"Valid article\",\"summary\":\"Valid summary\",\"url\":\"https://news.example/valid\"}]"));

            var news = await testClient.Client.FetchNewsAsync(CancellationToken.None);

            var article = Assert.Single(news);
            Assert.Equal("5", article.ProviderNewsKey);
            Assert.Equal("Valid article", article.Title);
            Assert.Equal("https://news.example/valid", article.ExternalUrl);
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    private static TestClient CreateClient(StockPulseDbContext dbContext, params HttpResponseMessage[] responses) =>
        CreateClient(dbContext, (IEnumerable<HttpResponseMessage>)responses);

    private static TestClient CreateClient(
        StockPulseDbContext dbContext,
        Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) =>
        CreateClient(dbContext, new RecordingHttpMessageHandler(responseFactory));

    private static TestClient CreateClient(StockPulseDbContext dbContext, IEnumerable<HttpResponseMessage>? responses = null)
        => CreateClient(dbContext, new RecordingHttpMessageHandler(responses ?? []));

    private static TestClient CreateClient(StockPulseDbContext dbContext, RecordingHttpMessageHandler handler)
    {
        var logger = new RecordingLogger<FinnhubNewsClient>();
        var timeProvider = new ImmediateTimeProvider(new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        services.AddSingleton<IHttpClientFactory>(new RecordingHttpClientFactory(handler));
        services.AddSingleton<IOptions<FinnhubOptions>>(Options.Create(new FinnhubOptions { ApiKey = "secret-finnhub-token" }));
        services.AddSingleton<ILogger<FinnhubNewsClient>>(logger);
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddScoped<FinnhubNewsClient>();
        var serviceProvider = services.BuildServiceProvider();
        var scope = serviceProvider.CreateScope();
        return new TestClient(scope, handler, logger, timeProvider);
    }

    private static WatchlistItem CreateWatchlistItem(string ticker, int sortOrder, bool isActive) =>
        new()
        {
            Ticker = ticker,
            SortOrder = sortOrder,
            IsActive = isActive,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

    private static async Task SeedPersistedNewsAsync(StockPulseDbContext dbContext, string ticker, DateTimeOffset publishedAtUtc)
    {
        var source = new NewsSource { SourceCode = $"source-{Guid.NewGuid():N}", SourceName = "Test source" };
        var news = new StockNews
        {
            Source = source,
            ExternalUrl = $"https://news.example/{Guid.NewGuid():N}",
            Title = "Persisted article",
            PublishedAtUtc = publishedAtUtc,
            ReceivedAtUtc = publishedAtUtc,
            DedupHash = Guid.NewGuid().ToString("N").PadRight(64, '0'),
            RawPayload = JsonDocument.Parse("{}")
        };
        news.Tickers.Add(new StockNewsTicker { Ticker = ticker, IsPrimary = true });
        dbContext.StockNews.Add(news);
        await Task.CompletedTask;
    }

    private static HttpResponseMessage CreateJsonResponse(string content) =>
        new(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    private static string? GetQueryValue(Uri requestUri, string name) =>
        requestUri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .SingleOrDefault(parts => string.Equals(Uri.UnescapeDataString(parts[0]), name, StringComparison.Ordinal)) is { Length: 2 } matchingParts
                ? Uri.UnescapeDataString(matchingParts[1])
                : null;

    private static async Task<string> CreateSchemaAsync()
    {
        var schemaName = $"finnhub_client_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(GetDatabaseConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{schemaName}\";", connection);
        await command.ExecuteNonQueryAsync();
        return schemaName;
    }

    private static async Task DropSchemaAsync(string schemaName)
    {
        await using var connection = new NpgsqlConnection(GetDatabaseConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE;", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<StockPulseDbContext> CreateSchemaDbContextAsync(string schemaName)
    {
        var options = new DbContextOptionsBuilder<StockPulseDbContext>()
            .UseNpgsql($"{GetDatabaseConnectionString()};Search Path={schemaName}")
            .Options;
        var dbContext = new StockPulseDbContext(options);
        await dbContext.Database.ExecuteSqlRawAsync(dbContext.Database.GenerateCreateScript());
        return dbContext;
    }

    private static string GetDatabaseConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("STOCKPULSE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Set STOCKPULSE_TEST_CONNECTION before running integration tests.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, "stockpulse_test", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("STOCKPULSE_TEST_CONNECTION must target the stockpulse_test database.");
        }

        return builder.ConnectionString;
    }

    private sealed class TestClient : IDisposable
    {
        private readonly IServiceScope scope;

        public TestClient(
            IServiceScope scope,
            RecordingHttpMessageHandler handler,
            RecordingLogger<FinnhubNewsClient> logger,
            ImmediateTimeProvider timeProvider)
        {
            this.scope = scope;
            Handler = handler;
            Logger = logger;
            TimeProvider = timeProvider;
        }

        public FinnhubNewsClient Client => scope.ServiceProvider.GetRequiredService<FinnhubNewsClient>();
        public RecordingHttpMessageHandler Handler { get; }
        public RecordingLogger<FinnhubNewsClient> Logger { get; }
        public ImmediateTimeProvider TimeProvider { get; }

        public void Dispose() => scope.Dispose();
    }

    private sealed class RecordingHttpClientFactory(RecordingHttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler) { BaseAddress = new Uri("https://finnhub.io/api/v1/") };
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage>? responses;
        private readonly Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? responseFactory;

        public RecordingHttpMessageHandler(IEnumerable<HttpResponseMessage> responses)
        {
            this.responses = new Queue<HttpResponseMessage>(responses);
        }

        public RecordingHttpMessageHandler(Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null)
            {
                RequestUris.Add(request.RequestUri);
            }

            return responseFactory is not null
                ? responseFactory(RequestUris.Count, request, cancellationToken)
                : Task.FromResult(responses is { Count: > 0 } ? responses.Dequeue() : CreateJsonResponse("[]"));
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            Entries.Add(new LogEntry(message, exception));
            Messages.Add(message);
        }
    }

    private sealed record LogEntry(string Message, Exception? Exception);

    private sealed class ImmediateTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public List<TimeSpan> Delays { get; } = [];

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays.Add(dueTime);
            callback(state);
            return new ImmediateTimer();
        }
    }

    private sealed class ImmediateTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
#pragma warning restore CA1707
}
