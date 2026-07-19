using StockPulse.Infrastructure;
using StockPulse.Worker;
using StockPulse.Worker.HostedServices;
using StockPulse.Worker.Pipelines;
using StockPulse.Worker.Providers;
using StockPulse.Worker.Providers.Mock;
using StockPulse.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);
if (!builder.Configuration.GetValue<bool>("Worker:UseMockProviders"))
{
    throw new InvalidOperationException("Phase 0 supports mock news providers only.");
}

var realtimeApiBaseUrl = builder.Configuration["RealtimeApi:BaseUrl"]
    ?? throw new InvalidOperationException("RealtimeApi:BaseUrl must be configured.");
builder.Services.AddStockPulseInfrastructure(builder.Configuration);
builder.Services.AddHttpClient<ApiRealtimeNotifier>(client => client.BaseAddress = new Uri(realtimeApiBaseUrl));
builder.Services.AddScoped<INewsCreatedNotifier>(serviceProvider => serviceProvider.GetRequiredService<ApiRealtimeNotifier>());
builder.Services.AddSingleton<IProviderNewsClient, MockNewsClient>();
builder.Services.AddScoped<NewsIngestionPipeline>();
builder.Services.AddHostedService<NewsIngestionHostedService>();

var host = builder.Build();
host.Run();
