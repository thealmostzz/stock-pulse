using System.Text.RegularExpressions;

namespace StockPulse.Application.Services;

public static partial class TickerNormalizer
{
    public static string Normalize(string ticker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);

        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        if (!TickerPattern().IsMatch(normalizedTicker))
        {
            throw new ArgumentException("Ticker format is invalid.", nameof(ticker));
        }

        return normalizedTicker;
    }

    [GeneratedRegex("^[A-Z][A-Z0-9.-]{0,19}$")]
    private static partial Regex TickerPattern();
}
