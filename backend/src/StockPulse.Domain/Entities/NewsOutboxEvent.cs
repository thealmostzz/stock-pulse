using System.Text.Json;

namespace StockPulse.Domain.Entities;

public sealed class NewsOutboxEvent
{
    public Guid EventId { get; set; }
    public long NewsId { get; set; }
    public StockNews News { get; set; } = null!;
    public JsonDocument Payload { get; set; } = JsonDocument.Parse("{}");
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public DateTimeOffset? LockedUntilUtc { get; set; }
    public Guid? LockToken { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
