using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockPulse.Domain.Entities;

namespace StockPulse.Infrastructure.Persistence.Configurations;

public sealed class RealtimeDeliveryReceiptConfiguration : IEntityTypeConfiguration<RealtimeDeliveryReceipt>
{
    public void Configure(EntityTypeBuilder<RealtimeDeliveryReceipt> builder)
    {
        builder.ToTable("realtime_delivery_receipts");
        builder.HasKey(receipt => receipt.EventId);
        builder.Property(receipt => receipt.EventId).HasColumnName("event_id");
        builder.Property(receipt => receipt.DeliveredAtUtc).HasColumnName("delivered_at_utc");
    }
}
