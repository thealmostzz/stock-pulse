namespace StockPulse.Application.DTOs;

public sealed record WatchlistItemDto(long Id, string Ticker, string? DisplayName, string? Market, int SortOrder, bool IsActive);

public sealed record CreateWatchlistRequest(string Ticker, string? DisplayName, string? Market);
