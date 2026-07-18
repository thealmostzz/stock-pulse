using System.Text.Json;

namespace StockPulse.Contracts.News;

public sealed record NormalizedNewsDto(
    string SourceCode,
    string? ProviderNewsKey,
    string ExternalUrl,
    string Title,
    string? Summary,
    DateTimeOffset PublishedAtUtc,
    IReadOnlyList<string> Tickers,
    JsonDocument RawPayload);
