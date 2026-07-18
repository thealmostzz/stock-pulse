using Npgsql;

namespace StockPulse.Infrastructure.Tests;

internal static class TestDatabaseConnection
{
    public static string GetConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Set STOCKPULSE_TEST_CONNECTION before running integration tests.");
        }

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(connectionStringBuilder.Database, "stockpulse_test", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("STOCKPULSE_TEST_CONNECTION must target the stockpulse_test database.");
        }

        return connectionString;
    }
}
