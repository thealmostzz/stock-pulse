namespace StockPulse.Domain.Entities;

public sealed class StockNewsTicker
{
    public long NewsId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; } = 1m;
    public bool IsPrimary { get; set; }
    public StockNews News { get; set; } = null!;
}
