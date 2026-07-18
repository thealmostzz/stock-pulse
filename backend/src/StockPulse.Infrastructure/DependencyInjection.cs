using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockPulse.Infrastructure.Persistence;

namespace StockPulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStockPulseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<StockPulseDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("StockPulse")));
        return services;
    }
}
