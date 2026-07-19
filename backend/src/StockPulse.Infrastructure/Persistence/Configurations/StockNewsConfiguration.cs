using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockPulse.Domain.Entities;

namespace StockPulse.Infrastructure.Persistence.Configurations;

public sealed class StockNewsConfiguration : IEntityTypeConfiguration<StockNews>
{
    public void Configure(EntityTypeBuilder<StockNews> builder)
    {
        builder.ToTable("stock_news");
        builder.HasKey(news => news.Id);
        builder.Property(news => news.Id).UseHiLo("stock_news_hilo");
        builder.Property(news => news.Title).IsRequired();
        builder.Property(news => news.ExternalUrl).IsRequired();
        builder.Property(news => news.DedupHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(news => news.DedupHash).IsUnique();
        builder.HasIndex(news => news.PublishedAtUtc).IsDescending();
        builder.HasIndex(news => news.ImpactScore).IsDescending();
        builder.Property(news => news.RawPayload).HasColumnType("jsonb");
        builder.Property(news => news.Tags).HasColumnType("jsonb");
        builder.HasOne(news => news.Source).WithMany(source => source.News).HasForeignKey(news => news.SourceId);

        builder.OwnsMany(news => news.Tickers, ticker =>
        {
            ticker.ToTable("stock_news_tickers");
            ticker.WithOwner(item => item.News).HasForeignKey(item => item.NewsId);
            ticker.HasKey(item => new { item.NewsId, item.Ticker });
            ticker.Property(item => item.Ticker).HasMaxLength(20);
            ticker.HasIndex(item => new { item.Ticker, item.NewsId }).IsDescending(false, true);
        });
    }
}
