using System.Text.Json;
using StockPulse.Domain.Enums;

namespace StockPulse.Domain.Entities;

public sealed class StockNews
{
    public long Id { get; set; }
    public short SourceId { get; set; }
    public string? ProviderNewsKey { get; set; }
    public string ExternalUrl { get; set; } = string.Empty;
    public string? CanonicalUrl { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public NewsSentiment Sentiment { get; set; } = NewsSentiment.Neutral;
    public decimal SentimentScore { get; set; }
    public decimal ImpactScore { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public string DedupHash { get; set; } = string.Empty;
    public JsonDocument RawPayload { get; set; } = JsonDocument.Parse("{}");
    public List<string> Tags { get; set; } = [];
    public List<StockNewsTicker> Tickers { get; } = [];
    public NewsSource Source { get; set; } = null!;
}
