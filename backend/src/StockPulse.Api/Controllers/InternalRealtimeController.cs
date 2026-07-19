using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using StockPulse.Application.Abstractions;
using StockPulse.Application.DTOs;

namespace StockPulse.Api.Controllers;

[ApiController]
[Route("internal/realtime")]
public sealed class InternalRealtimeController(
    IConfiguration configuration,
    IRealtimePublisher realtimePublisher) : ControllerBase
{
    private const string InternalKeyHeaderName = "X-StockPulse-Internal-Key";

    [HttpPost("news-created")]
    public async Task<IActionResult> NewsCreated(NewsCreatedEvent message, CancellationToken cancellationToken)
    {
        if (!HasValidInternalKey())
        {
            return Unauthorized();
        }

        await realtimePublisher.PublishNewsCreatedAsync(message, cancellationToken);
        return NoContent();
    }

    private bool HasValidInternalKey()
    {
        var expectedKey = configuration["InternalRealtime:SharedKey"];
        if (string.IsNullOrWhiteSpace(expectedKey) ||
            !Request.Headers.TryGetValue(InternalKeyHeaderName, out var suppliedKey) ||
            suppliedKey.Count != 1)
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey[0]!);
        return expectedBytes.Length == suppliedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
