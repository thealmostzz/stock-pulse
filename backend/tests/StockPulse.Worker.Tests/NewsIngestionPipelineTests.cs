using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using StockPulse.Application.DTOs;
using StockPulse.Contracts.News;
using StockPulse.Domain.Entities;
using StockPulse.Infrastructure.Persistence;
using StockPulse.Infrastructure.Persistence.Migrations;
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
    public void Model_HasUniqueOutboxNewsIdAndUniqueSourceCode()
    {
        using var db = CreateDbContext();
        var outboxEvent = db.Model.FindEntityType(typeof(NewsOutboxEvent));
        var newsSource = db.Model.FindEntityType(typeof(NewsSource));

        Assert.NotNull(outboxEvent);
        Assert.NotNull(newsSource);
        Assert.True(outboxEvent
            .GetIndexes()
            .Single(index => index.Properties.Count == 1 &&
                index.Properties[0].Name == nameof(NewsOutboxEvent.NewsId))
            .IsUnique);
        Assert.True(newsSource
            .GetIndexes()
            .Single(index => index.Properties.Count == 1 &&
                index.Properties[0].Name == nameof(NewsSource.SourceCode))
            .IsUnique);
    }

    [Fact]
    public void TestDatabaseConnection_RejectsNonTestDatabase()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ValidateTestDatabaseConnection("Host=localhost;Database=stockpulse"));

        Assert.Contains("stockpulse_test", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddRealtimeOutboxMigration_PreflightsDuplicateSourceCodesBeforeUniqueIndex()
    {
        var migration = new TestAddRealtimeOutboxMigration();
        var operations = migration.GetUpOperations();
        var preflight = operations
            .Select((operation, index) => new { operation, index })
            .Single(item => item.operation is SqlOperation sqlOperation &&
                sqlOperation.Sql.Contains("duplicate non-null SourceCode", StringComparison.Ordinal));
        var preflightSql = Assert.IsType<SqlOperation>(preflight.operation).Sql;
        var uniqueIndexPosition = operations
            .Select((operation, index) => new { operation, index })
            .Single(item => item.operation is CreateIndexOperation createIndexOperation &&
                createIndexOperation.Name == "IX_NewsSources_SourceCode")
            .index;

        Assert.Contains("WHERE \"SourceCode\" IS NOT NULL", preflightSql, StringComparison.Ordinal);
        Assert.Contains("GROUP BY \"SourceCode\"", preflightSql, StringComparison.Ordinal);
        Assert.Contains("HAVING COUNT(*) > 1", preflightSql, StringComparison.Ordinal);
        Assert.True(preflight.index < uniqueIndexPosition);
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

    private static StockPulseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StockPulseDbContext>()
            .UseNpgsql(GetDatabaseConnectionString())
            .Options;
        return new StockPulseDbContext(options);
    }

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
        ValidateTestDatabaseConnection(Environment.GetEnvironmentVariable("STOCKPULSE_TEST_CONNECTION"));

    private static string ValidateTestDatabaseConnection(string? connectionString)
    {
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

    private sealed class TestAddRealtimeOutboxMigration : AddRealtimeOutbox
    {
        public List<MigrationOperation> GetUpOperations()
        {
            var migrationBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(migrationBuilder);
            return migrationBuilder.Operations;
        }
    }

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
