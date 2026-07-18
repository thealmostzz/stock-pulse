namespace StockPulse.Domain.Entities;

public sealed class WatchlistItem
{
    public long Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Market { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
