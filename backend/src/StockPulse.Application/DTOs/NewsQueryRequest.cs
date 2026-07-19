namespace StockPulse.Application.DTOs;

public sealed record NewsQueryRequest(string? Ticker, string? SourceCode, string? Sentiment, string? Tag, int Page = 1, int PageSize = 50);

public sealed record PagedResponseDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, bool HasMore);
