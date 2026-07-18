using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockPulse.Domain.Entities;

namespace StockPulse.Infrastructure.Persistence.Configurations;

public sealed class WatchlistItemConfiguration : IEntityTypeConfiguration<WatchlistItem>
{
    public void Configure(EntityTypeBuilder<WatchlistItem> builder)
    {
        builder.ToTable("watchlists");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Ticker).HasMaxLength(20).IsRequired();
        builder.HasIndex(item => item.Ticker).IsUnique();
    }
}
