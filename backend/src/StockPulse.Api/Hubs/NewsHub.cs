using Microsoft.AspNetCore.SignalR;
using StockPulse.Application.Services;

namespace StockPulse.Api.Hubs;

public sealed class NewsHub : Hub
{
    public Task SubscribeTicker(string ticker) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"ticker:{TickerNormalizer.Normalize(ticker)}");

    public Task UnsubscribeTicker(string ticker) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticker:{TickerNormalizer.Normalize(ticker)}");
}
