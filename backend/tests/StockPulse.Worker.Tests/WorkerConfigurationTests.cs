using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StockPulse.Worker.Configuration;
using StockPulse.Worker.HostedServices;
using StockPulse.Worker.Providers;
using StockPulse.Worker.Providers.Finnhub;
using StockPulse.Worker.Providers.Mock;

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

    [Theory]
    [InlineData(true, typeof(MockNewsClient), ServiceLifetime.Singleton)]
    [InlineData(false, typeof(FinnhubNewsClient), ServiceLifetime.Scoped)]
    public void BuildServiceProvider_WhenProviderModeIsConfigured_ResolvesHostedServiceAndSelectedProvider(
        bool useMockProviders,
        Type expectedProviderType,
        ServiceLifetime expectedLifetime)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("Worker:UseMockProviders", useMockProviders.ToString()),
            new KeyValuePair<string, string?>("Finnhub:ApiKey", "test-finnhub-key")
        ]);
        var workerOptions = builder.Configuration.GetSection("Worker").Get<WorkerOptions>() ?? new WorkerOptions();
        var finnhubOptions = builder.Configuration.GetSection("Finnhub").Get<FinnhubOptions>() ?? new FinnhubOptions();
        workerOptions.Validate(finnhubOptions);
        builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
        builder.Services.Configure<FinnhubOptions>(builder.Configuration.GetSection("Finnhub"));
        builder.Services.AddHttpClient("Finnhub", client => client.BaseAddress = new Uri("https://finnhub.io/api/v1/"));

        builder.Services.AddSelectedNewsProvider(workerOptions);
        builder.Services.AddHostedService<NewsIngestionHostedService>();

        var providerDescriptor = Assert.Single(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(IProviderNewsClient));
        Assert.Equal(expectedLifetime, providerDescriptor.Lifetime);
        Assert.Equal(expectedProviderType, providerDescriptor.ImplementationType);

        using var serviceProvider = builder.Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.IsType<NewsIngestionHostedService>(serviceProvider.GetRequiredService<IEnumerable<IHostedService>>().Single());

        using var scope = serviceProvider.CreateScope();
        Assert.IsType(expectedProviderType, scope.ServiceProvider.GetRequiredService<IProviderNewsClient>());
    }
#pragma warning restore CA1707
}
