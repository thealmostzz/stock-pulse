using Microsoft.Extensions.DependencyInjection;
using StockPulse.Worker.Providers;
using StockPulse.Worker.Providers.Finnhub;
using StockPulse.Worker.Providers.Mock;

namespace StockPulse.Worker.Configuration;

public static class WorkerServiceCollectionExtensions
{
    public static IServiceCollection AddSelectedNewsProvider(
        this IServiceCollection services,
        WorkerOptions workerOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(workerOptions);

        if (workerOptions.UseMockProviders)
        {
            services.AddSingleton<IProviderNewsClient, MockNewsClient>();
        }
        else
        {
            services.AddScoped<IProviderNewsClient, FinnhubNewsClient>();
        }

        return services;
    }
}
