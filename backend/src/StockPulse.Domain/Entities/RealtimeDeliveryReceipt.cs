namespace StockPulse.Domain.Entities;

public sealed class RealtimeDeliveryReceipt
{
    public Guid EventId { get; set; }
    public DateTimeOffset DeliveredAtUtc { get; set; }
}
