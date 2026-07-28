using StockPulse.Infrastructure;
using StockPulse.Worker;
using StockPulse.Worker.Configuration;
using StockPulse.Worker.HostedServices;
using StockPulse.Worker.Pipelines;
using StockPulse.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);
var workerOptions = builder.Configuration.GetSection("Worker").Get<WorkerOptions>() ?? new WorkerOptions();
var finnhubOptions = builder.Configuration.GetSection("Finnhub").Get<FinnhubOptions>() ?? new FinnhubOptions();
workerOptions.Validate(finnhubOptions);

var realtimeApiBaseUrl = builder.Configuration["RealtimeApi:BaseUrl"]
    ?? throw new InvalidOperationException("RealtimeApi:BaseUrl must be configured.");
var realtimeApiSharedKey = builder.Configuration["RealtimeApi:SharedKey"];
if (string.IsNullOrWhiteSpace(realtimeApiSharedKey) ||
    string.Equals(realtimeApiSharedKey, "change-me", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("RealtimeApi:SharedKey must be configured and must not use a placeholder value.");
}

builder.Services.AddStockPulseInfrastructure(builder.Configuration);
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<FinnhubOptions>(builder.Configuration.GetSection("Finnhub"));
builder.Services.AddHttpClient<ApiRealtimeNotifier>(client => client.BaseAddress = new Uri(realtimeApiBaseUrl));
builder.Services.AddHttpClient("Finnhub", client => client.BaseAddress = new Uri("https://finnhub.io/api/v1/"));
builder.Services.AddScoped<INewsCreatedNotifier>(serviceProvider => serviceProvider.GetRequiredService<ApiRealtimeNotifier>());
builder.Services.AddSelectedNewsProvider(workerOptions);
builder.Services.AddScoped<NewsIngestionPipeline>();
builder.Services.AddScoped<OutboxDispatcher>();
builder.Services.AddHostedService<NewsIngestionHostedService>();

var host = builder.Build();
host.Run();
