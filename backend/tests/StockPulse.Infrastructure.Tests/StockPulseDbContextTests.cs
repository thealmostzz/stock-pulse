using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockPulse.Domain.Entities;
using StockPulse.Infrastructure.Persistence;

namespace StockPulse.Infrastructure.Tests;

public sealed class StockPulseDbContextTests
{
    [Fact]
    public void GetConnectionStringRejectsNonTestDatabase()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseConnection.GetConnectionString("Host=localhost;Database=stockpulse"));

        Assert.Contains("stockpulse_test", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StockNewsTickerOwnershipUsesNewsIdAsForeignKey()
    {
        var options = new DbContextOptionsBuilder<StockPulseDbContext>()
            .UseNpgsql("Host=localhost;Database=stockpulse")
            .Options;
        using var db = new StockPulseDbContext(options);

        var tickerEntity = db.Model.FindEntityType(typeof(StockNewsTicker));

        Assert.NotNull(tickerEntity);
        Assert.Null(tickerEntity.FindProperty("NewsId1"));
        Assert.Equal("NewsId", Assert.Single(tickerEntity.FindOwnership()!.Properties).Name);
    }

    [Fact]
    public async Task DatabaseSchemaEnforcesUniqueWatchlistTicker()
    {
        var connectionString = TestDatabaseConnection.GetConnectionString(
            Environment.GetEnvironmentVariable("STOCKPULSE_TEST_CONNECTION"));
        var schemaName = $"infrastructure_test_{Guid.NewGuid():N}";
        await CreateSchemaAsync(connectionString, schemaName);

        try
        {
            var options = new DbContextOptionsBuilder<StockPulseDbContext>()
                .UseNpgsql($"{connectionString};Search Path={schemaName}")
                .Options;
            await using var db = new StockPulseDbContext(options);
            await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
            db.WatchlistItems.AddRange(
                new WatchlistItem { Ticker = "AAPL" },
                new WatchlistItem { Ticker = "AAPL" });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally
        {
            await DropSchemaAsync(connectionString, schemaName);
        }
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
