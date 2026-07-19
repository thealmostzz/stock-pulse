using Microsoft.AspNetCore.SignalR;
using StockPulse.Api.Hubs;
using StockPulse.Application.Abstractions;
using StockPulse.Application.DTOs;

namespace StockPulse.Api.Services;

public sealed class SignalRRealtimePublisher(IHubContext<NewsHub> hubContext) : IRealtimePublisher
{
    public async Task PublishNewsCreatedAsync(NewsCreatedEvent message, CancellationToken cancellationToken)
    {
        await hubContext.Clients.All.SendAsync("news:new", message, cancellationToken);
    }
}
