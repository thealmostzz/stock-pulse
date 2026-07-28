using StockPulse.Worker.Configuration;

namespace StockPulse.Worker.Tests;

public sealed class WorkerConfigurationTests
{
#pragma warning disable CA1707 // Keep descriptive test names consistent with the task specification.
    [Fact]
    public void GetPollingInterval_WhenMockProvidersAreEnabled_ReturnsFifteenSeconds()
    {
        var options = new WorkerOptions { UseMockProviders = true };

        var pollingInterval = options.GetPollingInterval();

        Assert.Equal(TimeSpan.FromSeconds(15), pollingInterval);
    }

    [Fact]
    public void GetPollingInterval_WhenFinnhubProvidersAreEnabled_ReturnsFifteenMinutes()
    {
        var options = new WorkerOptions { UseMockProviders = false };

        var pollingInterval = options.GetPollingInterval();

        Assert.Equal(TimeSpan.FromMinutes(15), pollingInterval);
    }

    [Fact]
    public void Validate_WhenFinnhubIsEnabledWithoutAnApiKey_ThrowsConfigurationException()
    {
        var options = new WorkerOptions { UseMockProviders = false };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate(new FinnhubOptions()));

        Assert.Equal("Finnhub:ApiKey must be configured when mock providers are disabled.", exception.Message);
    }
#pragma warning restore CA1707
}
