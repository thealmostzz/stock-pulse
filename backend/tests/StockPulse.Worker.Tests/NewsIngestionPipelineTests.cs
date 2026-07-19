using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using StockPulse.Application.DTOs;
using StockPulse.Api.Controllers;
using StockPulse.Application.Abstractions;
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
    public async Task IngestAsync_SkipsDuplicateArticlesAndCreatesOneOutboxEvent()
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

    [Fact]
    public async Task IngestAsync_CreatesOutboxEvent_WhenNewsIsInserted()
    {
        var schemaName = $"worker_test_{Guid.NewGuid():N}";
        await CreateSchemaAsync(schemaName);

        try
        {
            await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
            var pipeline = new NewsIngestionPipeline(dbContext);

            await pipeline.IngestAsync([CreateArticle("https://example.test/outbox", DateTimeOffset.UtcNow)], CancellationToken.None);

            var outboxEvent = await dbContext.NewsOutboxEvents.SingleAsync();
            var message = outboxEvent.Payload.Deserialize<NewsCreatedEvent>();
            Assert.NotEqual(Guid.Empty, outboxEvent.EventId);
            Assert.Equal(0, outboxEvent.AttemptCount);
            Assert.Null(outboxEvent.DeliveredAtUtc);
            Assert.NotNull(message);
            Assert.Equal(outboxEvent.NewsId, message!.News.Id);
            Assert.Equal(outboxEvent.EventId, message.EventId);
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    [Fact]
    public async Task DispatchPendingAsync_RetriesFailedEvent_AndMarksItDelivered()
    {
        var schemaName = $"worker_test_{Guid.NewGuid():N}";
        await CreateSchemaAsync(schemaName);

        try
        {
            await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
            var pipeline = new NewsIngestionPipeline(dbContext);
            await pipeline.IngestAsync([CreateArticle("https://example.test/retry", DateTimeOffset.UtcNow)], CancellationToken.None);
            var outboxEvent = await dbContext.NewsOutboxEvents.SingleAsync();
            var notifier = new FailingThenRecordingNotifier();
            var dispatcher = new OutboxDispatcher(dbContext, notifier, NullLogger<OutboxDispatcher>.Instance);

            await dispatcher.DispatchPendingAsync(CancellationToken.None);

            var failedEvent = await dbContext.NewsOutboxEvents.SingleAsync();
            Assert.Equal(1, failedEvent.AttemptCount);
            Assert.Null(failedEvent.DeliveredAtUtc);
            Assert.NotNull(failedEvent.LastError);
            Assert.True(failedEvent.NextAttemptAtUtc > DateTimeOffset.UtcNow);

            failedEvent.NextAttemptAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync();
            await dispatcher.DispatchPendingAsync(CancellationToken.None);

            Assert.Equal(2, notifier.AttemptCount);
            var deliveredEvent = await dbContext.NewsOutboxEvents.SingleAsync();
            Assert.NotNull(deliveredEvent.DeliveredAtUtc);
            Assert.Null(deliveredEvent.LastError);
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    [Fact]
    public async Task InternalEndpoint_DoesNotPublishTwice_ForSameEventId()
    {
        var schemaName = $"worker_test_{Guid.NewGuid():N}";
        await CreateSchemaAsync(schemaName);

        try
        {
            await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
            var publisher = new RecordingPublisher();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["InternalRealtime:SharedKey"] = "test-internal-key" })
                .Build();
            var controller = new InternalRealtimeController(configuration, dbContext, publisher);
            var eventId = Guid.NewGuid();
            var message = new NewsCreatedEvent(Guid.Empty, DateTimeOffset.UtcNow, new NewsResponseDto(42, "test", null, "mock", "https://example.test", DateTimeOffset.UtcNow, [], "Neutral", 0m, []));
            controller.ControllerContext = CreateControllerContext(eventId);

            var first = await controller.NewsCreated(message, CancellationToken.None);
            controller.ControllerContext = CreateControllerContext(eventId);
            var second = await controller.NewsCreated(message, CancellationToken.None);

            Assert.IsType<NoContentResult>(first);
            Assert.IsType<NoContentResult>(second);
            Assert.Single(publisher.Messages);
            Assert.Equal(eventId, publisher.Messages.Single().EventId);
            Assert.Equal(eventId, await dbContext.RealtimeDeliveryReceipts.Select(receipt => receipt.EventId).SingleAsync());
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    [Fact]
    public async Task DispatchPendingAsync_ClaimsEventForOnlyOneDispatcher()
    {
        var schemaName = $"worker_test_{Guid.NewGuid():N}";
        await CreateSchemaAsync(schemaName);

        try
        {
            await using var firstDbContext = await CreateSchemaDbContextAsync(schemaName);
            await using var secondDbContext = CreateSchemaDbContext(schemaName);
            var pipeline = new NewsIngestionPipeline(firstDbContext);
            await pipeline.IngestAsync([CreateArticle("https://example.test/lease", DateTimeOffset.UtcNow)], CancellationToken.None);
            var firstNotifier = new BlockingNotifier();
            var secondNotifier = new RecordingNotifier();
            var firstDispatcher = new OutboxDispatcher(firstDbContext, firstNotifier, NullLogger<OutboxDispatcher>.Instance);
            var secondDispatcher = new OutboxDispatcher(secondDbContext, secondNotifier, NullLogger<OutboxDispatcher>.Instance);

            var firstDispatch = firstDispatcher.DispatchPendingAsync(CancellationToken.None);
            await firstNotifier.Started.WaitAsync(TimeSpan.FromSeconds(5));
            await secondDispatcher.DispatchPendingAsync(CancellationToken.None);
            firstNotifier.Release();
            await firstDispatch;

            Assert.Equal(1, firstNotifier.AttemptCount);
            Assert.Empty(secondNotifier.Messages);
            firstDbContext.ChangeTracker.Clear();
            Assert.NotNull((await firstDbContext.NewsOutboxEvents.SingleAsync()).DeliveredAtUtc);
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    [Fact]
    public async Task DispatchPendingAsync_ClaimsAtMostOneBoundedBatch()
    {
        var schemaName = $"worker_test_{Guid.NewGuid():N}";
        await CreateSchemaAsync(schemaName);

        try
        {
            await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
            var source = new NewsSource { SourceCode = "batch", SourceName = "batch" };
            dbContext.NewsSources.Add(source);
            await dbContext.SaveChangesAsync();
            var news = Enumerable.Range(0, 101)
                .Select(index => CreateSeedNews(1000 + index, source.Id))
                .ToArray();
            dbContext.StockNews.AddRange(news);
            await dbContext.SaveChangesAsync();
            dbContext.NewsOutboxEvents.AddRange(news.Select(CreateSeedOutboxEvent));
            await dbContext.SaveChangesAsync();
            var notifier = new RecordingNotifier();
            var dispatcher = new OutboxDispatcher(dbContext, notifier, NullLogger<OutboxDispatcher>.Instance);

            await dispatcher.DispatchPendingAsync(CancellationToken.None);

            Assert.Equal(100, notifier.Messages.Count);
            dbContext.ChangeTracker.Clear();
            Assert.Equal(1, await dbContext.NewsOutboxEvents.CountAsync(outboxEvent => outboxEvent.DeliveredAtUtc == null));
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    [Fact]
    public async Task DispatchPendingAsync_DoesNotFinalizeEvent_WhenLeaseTokenChanges()
    {
        var schemaName = $"worker_test_{Guid.NewGuid():N}";
        await CreateSchemaAsync(schemaName);

        try
        {
            await using var dispatchDbContext = await CreateSchemaDbContextAsync(schemaName);
            await using var leaseDbContext = CreateSchemaDbContext(schemaName);
            var pipeline = new NewsIngestionPipeline(dispatchDbContext);
            await pipeline.IngestAsync([CreateArticle("https://example.test/stale-lease", DateTimeOffset.UtcNow)], CancellationToken.None);
            var notifier = new BlockingNotifier();
            var dispatcher = new OutboxDispatcher(dispatchDbContext, notifier, NullLogger<OutboxDispatcher>.Instance);
            var dispatchTask = dispatcher.DispatchPendingAsync(CancellationToken.None);
            await notifier.Started.WaitAsync(TimeSpan.FromSeconds(5));
            var replacementToken = Guid.NewGuid();
            await leaseDbContext.NewsOutboxEvents.ExecuteUpdateAsync(setters => setters
                .SetProperty(outboxEvent => outboxEvent.LockToken, replacementToken)
                .SetProperty(outboxEvent => outboxEvent.LockedUntilUtc, DateTimeOffset.UtcNow.AddMinutes(5)));

            notifier.Release();
            await dispatchTask;

            leaseDbContext.ChangeTracker.Clear();
            var outboxEvent = await leaseDbContext.NewsOutboxEvents.SingleAsync();
            Assert.Equal(replacementToken, outboxEvent.LockToken);
            Assert.Null(outboxEvent.DeliveredAtUtc);
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    [Fact]
    public async Task IngestAsync_PreservesUnrelatedArticles_WhenConcurrentBatchCollides()
    {
        var schemaName = $"worker_test_{Guid.NewGuid():N}";
        await CreateSchemaAsync(schemaName);

        try
        {
            await using var firstDbContext = await CreateSchemaDbContextAsync(schemaName);
            await using var secondDbContext = CreateSchemaDbContext(schemaName);
            var firstPipeline = new NewsIngestionPipeline(firstDbContext);
            var secondPipeline = new NewsIngestionPipeline(secondDbContext);
            var publishedAt = DateTimeOffset.UtcNow;

            await Task.WhenAll(
                firstPipeline.IngestAsync(
                    [CreateArticle("https://example.test/common", publishedAt), CreateArticle("https://example.test/first", publishedAt)],
                    CancellationToken.None),
                secondPipeline.IngestAsync(
                    [CreateArticle("https://example.test/common", publishedAt), CreateArticle("https://example.test/second", publishedAt)],
                    CancellationToken.None));

            firstDbContext.ChangeTracker.Clear();
            Assert.Equal(3, await firstDbContext.StockNews.CountAsync());
            Assert.Equal(3, await firstDbContext.NewsOutboxEvents.CountAsync());
        }
        finally
        {
            await DropSchemaAsync(schemaName);
        }
    }

    [Fact]
    public async Task UseHiLoMigration_AllocatesAboveExistingIdsAcrossMultipleBlocks()
    {
        var schemaName = $"worker_test_{Guid.NewGuid():N}";
        await CreateSchemaAsync(schemaName);

        try
        {
            await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
            var source = new NewsSource { SourceCode = "seed", SourceName = "seed" };
            dbContext.NewsSources.Add(source);
            await dbContext.SaveChangesAsync();
            dbContext.StockNews.AddRange(
                CreateSeedNews(1, source.Id),
                CreateSeedNews(11, source.Id),
                CreateSeedNews(29, source.Id));
            await dbContext.SaveChangesAsync();
            var migration = new TestUseHiLoForStockNewsIdsMigration();
            var resetSql = migration.GetUpOperations()
                .OfType<SqlOperation>()
                .Single(operation => operation.Sql.Contains("setval", StringComparison.Ordinal));
            await dbContext.Database.ExecuteSqlRawAsync(resetSql.Sql);

            var nextHighValue = await dbContext.Database
                .SqlQueryRaw<long>("SELECT nextval('stock_news_hilo') AS \"Value\"")
                .SingleAsync();
            Assert.True(nextHighValue - 10 > 29, $"Expected the next HiLo block to start above 29 but received high value {nextHighValue}.");
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
        var pipeline = new NewsIngestionPipeline(dbContext);
        var publishedAt = new DateTimeOffset(2026, 7, 18, 8, 10, 0, TimeSpan.Zero);
        var first = CreateArticle("https://example.test/nvda?utm_source=worker", publishedAt);
        var duplicate = CreateArticle("https://example.test/nvda", publishedAt.AddSeconds(30));

        await pipeline.IngestAsync([first, duplicate], CancellationToken.None);

        var news = await dbContext.StockNews.Include(item => item.Tickers).SingleAsync(CancellationToken.None);
        Assert.Equal("mock", news.Source.SourceCode);
        Assert.Single(news.Tickers);
        Assert.Equal("NVDA", news.Tickers.Single().Ticker);
        var outboxEvent = await dbContext.NewsOutboxEvents.SingleAsync(CancellationToken.None);
        Assert.Equal(news.Id, outboxEvent.NewsId);
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

    private static StockPulseDbContext CreateSchemaDbContext(string schemaName)
    {
        var options = new DbContextOptionsBuilder<StockPulseDbContext>()
            .UseNpgsql($"{GetDatabaseConnectionString()};Search Path={schemaName}")
            .Options;
        return new StockPulseDbContext(options);
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

    private static ControllerContext CreateControllerContext(Guid eventId)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-StockPulse-Internal-Key"] = "test-internal-key";
        context.Request.Headers["X-StockPulse-Event-Id"] = eventId.ToString();
        return new ControllerContext { HttpContext = context };
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

    private sealed class TestUseHiLoForStockNewsIdsMigration : UseHiLoForStockNewsIds
    {
        public List<MigrationOperation> GetUpOperations()
        {
            var migrationBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(migrationBuilder);
            return migrationBuilder.Operations;
        }
    }

    private static StockNews CreateSeedNews(long id, short sourceId) =>
        new()
        {
            Id = id,
            SourceId = sourceId,
            ExternalUrl = $"https://example.test/seed/{id}",
            Title = $"Seed {id}",
            DedupHash = $"{id:D64}",
            PublishedAtUtc = DateTimeOffset.UtcNow,
            ReceivedAtUtc = DateTimeOffset.UtcNow,
            RawPayload = JsonDocument.Parse("{}")
        };

    private static NewsOutboxEvent CreateSeedOutboxEvent(StockNews news)
    {
        var eventId = Guid.NewGuid();
        return new NewsOutboxEvent
        {
            EventId = eventId,
            NewsId = news.Id,
            Payload = JsonSerializer.SerializeToDocument(
                new NewsCreatedEvent(
                    eventId,
                    DateTimeOffset.UtcNow,
                    new NewsResponseDto(news.Id, news.Title, news.Summary, "batch", news.ExternalUrl, news.PublishedAtUtc, [], "Neutral", 0m, []))),
            NextAttemptAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private sealed class RecordingNotifier : INewsCreatedNotifier
    {
        public List<NewsCreatedEvent> Messages { get; } = [];

        public Task NotifyAsync(Guid eventId, NewsCreatedEvent message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingThenRecordingNotifier : INewsCreatedNotifier
    {
        public int AttemptCount { get; private set; }

        public Task NotifyAsync(Guid eventId, NewsCreatedEvent message, CancellationToken cancellationToken)
        {
            AttemptCount++;
            return AttemptCount == 1
                ? Task.FromException(new HttpRequestException("API is temporarily unavailable."))
                : Task.CompletedTask;
        }
    }

    private sealed class BlockingNotifier : INewsCreatedNotifier
    {
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => started.Task;
        public int AttemptCount { get; private set; }

        public async Task NotifyAsync(Guid eventId, NewsCreatedEvent message, CancellationToken cancellationToken)
        {
            AttemptCount++;
            started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        }

        public void Release() => release.TrySetResult();
    }

    private sealed class RecordingPublisher : IRealtimePublisher
    {
        public List<NewsCreatedEvent> Messages { get; } = [];

        public Task PublishNewsCreatedAsync(NewsCreatedEvent message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
#pragma warning restore CA1707
}
