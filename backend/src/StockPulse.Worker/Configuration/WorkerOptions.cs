namespace StockPulse.Worker.Configuration;

public sealed class WorkerOptions
{
    public bool UseMockProviders { get; init; } = true;

    public TimeSpan GetPollingInterval() => UseMockProviders
        ? TimeSpan.FromSeconds(15)
        : TimeSpan.FromMinutes(15);

    public void Validate(FinnhubOptions finnhubOptions)
    {
        ArgumentNullException.ThrowIfNull(finnhubOptions);

        if (!UseMockProviders && string.IsNullOrWhiteSpace(finnhubOptions.ApiKey))
        {
            throw new InvalidOperationException("Finnhub:ApiKey must be configured when mock providers are disabled.");
        }
    }
}
