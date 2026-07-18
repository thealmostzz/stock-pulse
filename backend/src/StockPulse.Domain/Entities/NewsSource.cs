namespace StockPulse.Domain.Entities;

public sealed class NewsSource
{
    public short Id { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public List<StockNews> News { get; } = [];
}
