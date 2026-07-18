using Microsoft.EntityFrameworkCore;
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
        var options = new DbContextOptionsBuilder<StockPulseDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new StockPulseDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        db.WatchlistItems.AddRange(
            new WatchlistItem { Ticker = "AAPL" },
            new WatchlistItem { Ticker = "AAPL" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
