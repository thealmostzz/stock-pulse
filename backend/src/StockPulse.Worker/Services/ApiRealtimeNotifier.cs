using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using StockPulse.Application.DTOs;

namespace StockPulse.Worker.Services;

public sealed partial class ApiRealtimeNotifier(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<ApiRealtimeNotifier> logger) : INewsCreatedNotifier
{
    private const string InternalKeyHeaderName = "X-StockPulse-Internal-Key";
    private const string EventIdHeaderName = "X-StockPulse-Event-Id";

    public async Task NotifyAsync(Guid eventId, NewsCreatedEvent message, CancellationToken cancellationToken)
    {
        var sharedKey = configuration["RealtimeApi:SharedKey"]
            ?? throw new InvalidOperationException("RealtimeApi:SharedKey must be configured.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/realtime/news-created")
        {
            Content = JsonContent.Create(message)
        };
        request.Headers.Add(InternalKeyHeaderName, sharedKey);
        request.Headers.Add(EventIdHeaderName, eventId.ToString());

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        LogNotificationFailed(logger, (int)response.StatusCode, message.News.Id);
        response.EnsureSuccessStatusCode();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Realtime notification failed with status {StatusCode} for news {NewsId}.")]
    private static partial void LogNotificationFailed(ILogger logger, int statusCode, long newsId);
}
