using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockPulse.Application.DTOs;
using StockPulse.Contracts.News;
using StockPulse.Infrastructure.Persistence;
using StockPulse.Worker.Pipelines;
using StockPulse.Worker.Services;

namespace StockPulse.Worker.Tests;

public sealed class NewsIngestionPipelineTests
{
#pragma warning disable CA1707 // Keep descriptive test names consistent with the task specification.
    [Fact]
    public void CreateDedupHash_IgnoresUrlTrackingParameters()
    {
        var first = NewsIngestionPipeline.CreateDedupHash(
            "Nvidia beats estimates",
            "https://news.example/a?utm_source=x",
            new DateTimeOffset(2026, 7, 18, 8, 10, 45, TimeSpan.Zero),
            "mock");
        var second = NewsIngestionPipeline.CreateDedupHash(
            " nvidia beats estimates ",
            "https://news.example/a",
            new DateTimeOffset(2026, 7, 18, 8, 10, 5, TimeSpan.Zero),
            "mock");

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task IngestAsync_SkipsDuplicateArticlesAndNotifiesOnce()
    {
        var schemaName = $"worker_test_{Guid.NewGuid():N}";
        await CreateSchemaAsync(schemaName);

        try
        {
            await VerifyIngestionAsync(schemaName);
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    private static async Task VerifyIngestionAsync(string schemaName)
    {
        var options = new DbContextOptionsBuilder<StockPulseDbContext>()
            .UseNpgsql($"{GetDatabaseConnectionString()};Search Path={schemaName}")
            .Options;
        await using var dbContext = new StockPulseDbContext(options);
        await dbContext.Database.ExecuteSqlRawAsync(dbContext.Database.GenerateCreateScript());
        var notifier = new RecordingNotifier();
        var pipeline = new NewsIngestionPipeline(dbContext, notifier);
        var publishedAt = new DateTimeOffset(2026, 7, 18, 8, 10, 0, TimeSpan.Zero);
        var first = CreateArticle("https://example.test/nvda?utm_source=worker", publishedAt);
        var duplicate = CreateArticle("https://example.test/nvda", publishedAt.AddSeconds(30));

        await pipeline.IngestAsync([first, duplicate], CancellationToken.None);

        var news = await dbContext.StockNews.Include(item => item.Tickers).SingleAsync(CancellationToken.None);
        Assert.Equal("mock", news.Source.SourceCode);
        Assert.Single(news.Tickers);
        Assert.Equal("NVDA", news.Tickers.Single().Ticker);
        var notification = Assert.Single(notifier.Messages);
        Assert.Equal(news.Id, notification.News.Id);
    }

    private static NormalizedNewsDto CreateArticle(string url, DateTimeOffset publishedAt) =>
        new(
            "mock",
            "mock-001",
            url,
            "Nvidia beats estimates",
            "Mock article",
            publishedAt,
            ["NVDA"],
            JsonDocument.Parse("{}"));

    private static async Task CreateSchemaAsync(string schemaName)
    {
        await using var connection = new NpgsqlConnection(GetDatabaseConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{schemaName}\";", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropSchemaAsync(string schemaName)
    {
        await using var connection = new NpgsqlConnection(GetDatabaseConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE;", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string GetDatabaseConnectionString() =>
        "Host=localhost;Port=5432;Database=stockpulse;Username=stockpulse;Password=stockpulse_local_only";

    private sealed class RecordingNotifier : INewsCreatedNotifier
    {
        public List<NewsCreatedEvent> Messages { get; } = [];

        public Task NotifyAsync(NewsCreatedEvent message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
#pragma warning restore CA1707
}
