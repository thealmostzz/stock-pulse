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
        await hubContext.Clients.All.SendAsync("news:new", message, cancellationToken);

        foreach (var ticker in message.News.Tickers)
        {
            await hubContext.Clients
                .Group($"ticker:{TickerNormalizer.Normalize(ticker)}")
                .SendAsync("news:new", message, cancellationToken);
        }
    }
}
