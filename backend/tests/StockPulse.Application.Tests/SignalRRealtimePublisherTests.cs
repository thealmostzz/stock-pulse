using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using StockPulse.Api.Hubs;
using StockPulse.Api.Services;
using StockPulse.Application.DTOs;

namespace StockPulse.Application.Tests;

public sealed class SignalRRealtimePublisherTests
{
#pragma warning disable CA1707 // Keep the descriptive test name required by the task specification.
    [Fact]
    public async Task PublishNewsCreatedAsync_SendsOneEventPerConnection()
    {
        var client = new RecordingClientProxy();
        var clients = DispatchProxy.Create<IHubClients, HubClientsProxy>();
        ((HubClientsProxy)(object)clients).Client = client;
        var hubContext = DispatchProxy.Create<IHubContext<NewsHub>, HubContextProxy>();
        ((HubContextProxy)(object)hubContext).Clients = clients;
        var publisher = new SignalRRealtimePublisher(hubContext);
        var eventId = Guid.NewGuid();
        var message = new NewsCreatedEvent(
            eventId,
            DateTimeOffset.UtcNow,
            new NewsResponseDto(1, "Title", null, "source", "https://example.test/news", DateTimeOffset.UtcNow, ["NVDA"], "Neutral", 0m, []));

        await publisher.PublishNewsCreatedAsync(message, CancellationToken.None);

        Assert.Equal(["news:new"], client.MethodNames);
        Assert.Equal(eventId, Assert.IsType<NewsCreatedEvent>(Assert.Single(client.Arguments)).EventId);
    }
#pragma warning restore CA1707

    private sealed class RecordingClientProxy : IClientProxy
    {
        public List<string> MethodNames { get; } = [];
        public List<object?> Arguments { get; } = [];

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken)
        {
            MethodNames.Add(method);
            Arguments.Add(args.Single());
            return Task.CompletedTask;
        }
    }

#pragma warning disable CA1852 // DispatchProxy generates a derived runtime type.
    private class HubContextProxy : DispatchProxy
    {
        public IHubClients Clients { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == "get_Clients"
                ? Clients
                : throw new NotSupportedException(targetMethod?.Name);
    }

    private class HubClientsProxy : DispatchProxy
    {
        public IClientProxy Client { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == "get_All" || targetMethod?.ReturnType == typeof(IClientProxy)
                ? Client
                : throw new NotSupportedException(targetMethod?.Name);
#pragma warning restore CA1852
    }
}
