using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockPulse.Application.DTOs;
using StockPulse.Domain.Entities;
using StockPulse.Infrastructure.Persistence;
using StockPulse.Infrastructure.Persistence.Repositories;

namespace StockPulse.Infrastructure.Tests;

public sealed class NewsRepositoryTests
{
    [Fact]
    public async Task QueryAsyncImpactSortOrdersByImpactThenPublishedAt()
    {
        await RunWithDatabaseAsync(
            hasActiveWatchlist: true,
            async dbContext =>
            {
                var repository = new NewsRepository(dbContext);

                var result = await repository.QueryAsync(
                    new NewsQueryRequest(null, null, null, null, SortBy: "impact"),
                    CancellationToken.None);

                Assert.Equal(
                    ["newsC", "newsB", "newsA"],
                    result.Items.Select(item => item.Title));
            });
    }

    [Fact]
    public async Task QueryAsyncWatchlistOnlyReturnsNewsForActiveWatchlistTickers()
    {
        await RunWithDatabaseAsync(
            hasActiveWatchlist: true,
            async dbContext =>
            {
                var repository = new NewsRepository(dbContext);

                var result = await repository.QueryAsync(
                    new NewsQueryRequest(null, null, null, null, SortBy: "publishedAt", WatchlistOnly: true),
                    CancellationToken.None);

                Assert.Equal(1, result.TotalCount);
                Assert.Equal("newsA", Assert.Single(result.Items).Title);
            });
    }

    [Fact]
    public async Task QueryAsyncWatchlistOnlyWithNoActiveTickersReturnsEmptyPage()
    {
        await RunWithDatabaseAsync(
            hasActiveWatchlist: false,
            async dbContext =>
            {
                var repository = new NewsRepository(dbContext);

                var result = await repository.QueryAsync(
                    new NewsQueryRequest(null, null, null, null, SortBy: "publishedAt", WatchlistOnly: true),
                    CancellationToken.None);

                Assert.Empty(result.Items);
                Assert.Equal(0, result.TotalCount);
            });
    }

    private static async Task RunWithDatabaseAsync(
        bool hasActiveWatchlist,
        Func<StockPulseDbContext, Task> test)
    {
        var connectionString = TestDatabaseConnection.GetConnectionString(
            Environment.GetEnvironmentVariable("STOCKPULSE_TEST_CONNECTION"));
        var schemaName = $"news_repository_test_{Guid.NewGuid():N}";
        await CreateSchemaAsync(connectionString, schemaName);

        try
        {
            var options = new DbContextOptionsBuilder<StockPulseDbContext>()
                .UseNpgsql($"{connectionString};Search Path={schemaName}")
                .Options;
            await using var dbContext = new StockPulseDbContext(options);
            await dbContext.Database.ExecuteSqlRawAsync(dbContext.Database.GenerateCreateScript());
            await SeedAsync(dbContext, hasActiveWatchlist);

            await test(dbContext);
        }
        finally
        {
            await DropSchemaAsync(connectionString, schemaName);
        }
    }

    private static async Task SeedAsync(StockPulseDbContext dbContext, bool hasActiveWatchlist)
    {
        var source = new NewsSource
        {
            SourceCode = "test",
            SourceName = "Test Source"
        };
        var newsA = CreateNews(source, "newsA", "AAPL", 0.20m, 10);
        var newsB = CreateNews(source, "newsB", "NVDA", 0.90m, 9);
        var newsC = CreateNews(source, "newsC", "MSFT", 0.90m, 11);
        var timestamp = new DateTimeOffset(2026, 7, 29, 8, 0, 0, TimeSpan.Zero);

        dbContext.StockNews.AddRange(newsA, newsB, newsC);
        dbContext.WatchlistItems.AddRange(
            new WatchlistItem
            {
                Ticker = "AAPL",
                IsActive = hasActiveWatchlist,
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            },
            new WatchlistItem
            {
                Ticker = "NVDA",
                IsActive = false,
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });
        await dbContext.SaveChangesAsync();
    }

    private static StockNews CreateNews(
        NewsSource source,
        string title,
        string ticker,
        decimal impactScore,
        int publishedHour)
    {
        var news = new StockNews
        {
            Source = source,
            ExternalUrl = $"https://example.test/{title}",
            Title = title,
            ImpactScore = impactScore,
            PublishedAtUtc = new DateTimeOffset(2026, 7, 29, publishedHour, 0, 0, TimeSpan.Zero),
            ReceivedAtUtc = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            DedupHash = $"hash-{title}"
        };
        news.Tickers.Add(new StockNewsTicker { Ticker = ticker, IsPrimary = true });
        return news;
    }

    private static async Task CreateSchemaAsync(string connectionString, string schemaName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{schemaName}\";", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropSchemaAsync(string connectionString, string schemaName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE;", connection);
        await command.ExecuteNonQueryAsync();
    }
}
