using Microsoft.EntityFrameworkCore;
using StockPulse.Domain.Entities;

namespace StockPulse.Infrastructure.Persistence;

public sealed class StockPulseDbContext(DbContextOptions<StockPulseDbContext> options) : DbContext(options)
{
    public DbSet<NewsSource> NewsSources => Set<NewsSource>();
    public DbSet<StockNews> StockNews => Set<StockNews>();
    public DbSet<StockNewsTicker> StockNewsTickers => Set<StockNewsTicker>();
    public DbSet<NewsOutboxEvent> NewsOutboxEvents => Set<NewsOutboxEvent>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StockPulseDbContext).Assembly);
        modelBuilder.Entity<NewsSource>().HasIndex(source => source.SourceCode).IsUnique();
    }
}
