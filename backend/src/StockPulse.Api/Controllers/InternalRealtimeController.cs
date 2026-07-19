using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockPulse.Application.Abstractions;
using StockPulse.Application.DTOs;
using StockPulse.Infrastructure.Persistence;

namespace StockPulse.Api.Controllers;

[ApiController]
[Route("internal/realtime")]
public sealed class InternalRealtimeController(
    IConfiguration configuration,
    StockPulseDbContext dbContext,
    IRealtimePublisher realtimePublisher) : ControllerBase
{
    private const string InternalKeyHeaderName = "X-StockPulse-Internal-Key";
    private const string EventIdHeaderName = "X-StockPulse-Event-Id";

    [HttpPost("news-created")]
    public async Task<IActionResult> NewsCreated(NewsCreatedEvent message, CancellationToken cancellationToken)
    {
        if (!HasValidInternalKey())
        {
            return Unauthorized();
        }

        if (!Guid.TryParse(Request.Headers[EventIdHeaderName], out var eventId))
        {
            return BadRequest();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var receiptInserted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO realtime_delivery_receipts (event_id, delivered_at_utc) VALUES ({eventId}, {DateTimeOffset.UtcNow}) ON CONFLICT (event_id) DO NOTHING",
            cancellationToken);
        if (receiptInserted != 0)
        {
            await realtimePublisher.PublishNewsCreatedAsync(message, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
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
