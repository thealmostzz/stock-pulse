namespace StockPulse.Application.DTOs;

public sealed record NewsResponseDto(long Id, string Title, string? Summary, string SourceCode, string Url, DateTimeOffset PublishedAtUtc, IReadOnlyList<string> Tickers, string Sentiment, decimal ImpactScore, IReadOnlyList<string> Tags);

public sealed record NewsCreatedEvent(Guid EventId, DateTimeOffset SentAtUtc, NewsResponseDto News);
