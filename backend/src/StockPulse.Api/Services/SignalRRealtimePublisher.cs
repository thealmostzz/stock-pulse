using Microsoft.AspNetCore.SignalR;
using StockPulse.Api.Hubs;
using StockPulse.Application.Abstractions;
using StockPulse.Application.DTOs;
using StockPulse.Application.Services;

namespace StockPulse.Api.Services;

public sealed class SignalRRealtimePublisher(IHubContext<NewsHub> hubContext) : IRealtimePublisher
{
    public async Task PublishNewsCreatedAsync(NewsCreatedEvent message, CancellationToken cancellationToken)
    {
        var tickers = new List<string>();
        foreach (var messageTicker in message.News.Tickers)
        {
            try
            {
                var ticker = TickerNormalizer.Normalize(messageTicker);
                if (!tickers.Contains(ticker, StringComparer.Ordinal))
                {
                    tickers.Add(ticker);
                }
            }
            catch (ArgumentException)
            {
                // Historical malformed ticker data must not block global realtime delivery.
            }
        }

        await hubContext.Clients.All.SendAsync("news:new", message, cancellationToken);

        foreach (var ticker in tickers)
        {
            await hubContext.Clients.Group($"ticker:{ticker}").SendAsync("news:new", message, cancellationToken);
        }
    }
}
