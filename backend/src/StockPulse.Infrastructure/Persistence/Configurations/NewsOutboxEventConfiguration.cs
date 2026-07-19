using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockPulse.Domain.Entities;

namespace StockPulse.Infrastructure.Persistence.Configurations;

public sealed class NewsOutboxEventConfiguration : IEntityTypeConfiguration<NewsOutboxEvent>
{
    public void Configure(EntityTypeBuilder<NewsOutboxEvent> builder)
    {
        builder.ToTable("news_outbox_events");
        builder.HasKey(outboxEvent => outboxEvent.EventId);
        builder.Property(outboxEvent => outboxEvent.EventId).HasColumnName("event_id");
        builder.Property(outboxEvent => outboxEvent.NewsId).HasColumnName("news_id");
        builder.Property(outboxEvent => outboxEvent.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(outboxEvent => outboxEvent.AttemptCount).HasColumnName("attempt_count");
        builder.Property(outboxEvent => outboxEvent.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(outboxEvent => outboxEvent.DeliveredAtUtc).HasColumnName("delivered_at_utc");
        builder.Property(outboxEvent => outboxEvent.LastError).HasColumnName("last_error");
        builder.Property(outboxEvent => outboxEvent.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasIndex(outboxEvent => new { outboxEvent.DeliveredAtUtc, outboxEvent.NextAttemptAtUtc });
        builder.HasOne(outboxEvent => outboxEvent.News)
            .WithOne()
            .HasForeignKey<NewsOutboxEvent>(outboxEvent => outboxEvent.NewsId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
