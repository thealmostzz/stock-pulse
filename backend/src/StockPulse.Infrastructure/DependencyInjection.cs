using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockPulse.Application.Abstractions;
using StockPulse.Infrastructure.Persistence;
using StockPulse.Infrastructure.Persistence.Repositories;

namespace StockPulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStockPulseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<StockPulseDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("StockPulse")));
        services.AddScoped<IWatchlistRepository, WatchlistRepository>();
        services.AddScoped<INewsRepository, NewsRepository>();
        return services;
    }
}
