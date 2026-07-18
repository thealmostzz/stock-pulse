using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockPulse.Infrastructure.Persistence;

public sealed class StockPulseDbContextFactory : IDesignTimeDbContextFactory<StockPulseDbContext>
{
    public StockPulseDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("STOCKPULSE_CONNECTION")
            ?? throw new InvalidOperationException("Set STOCKPULSE_CONNECTION before running EF Core tools.");
        var options = new DbContextOptionsBuilder<StockPulseDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new StockPulseDbContext(options);
    }
}
