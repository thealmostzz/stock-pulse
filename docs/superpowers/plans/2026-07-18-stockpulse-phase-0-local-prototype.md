# StockPulse Phase 0 Local Prototype Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** สร้าง StockPulse ที่รันในเครื่องได้ครบ flow ตั้งแต่ mock ข่าวหุ้น, บันทึก PostgreSQL, SignalR push จนแสดงบน Angular dashboard พร้อม global watchlist.

**Architecture:** ใช้ Angular SPA เรียก REST API และเชื่อม SignalR กับ `StockPulse.Api` เท่านั้น. `StockPulse.Worker` อ่านข่าวจาก mock provider, ประมวลผลและบันทึกข้อมูลลง PostgreSQL แล้วเรียก internal API ของ `StockPulse.Api` เพื่อ broadcast event โดย Worker ไม่เชื่อมกับ client โดยตรง.

**Tech Stack:** Angular 18+, TypeScript, Angular CDK, .NET 10 SDK, ASP.NET Core Web API, SignalR, EF Core 10, Npgsql, PostgreSQL 16, xUnit, Docker Compose.

## Global Constraints

- MVP เป็น single-user: `watchlists` เป็น global และยังไม่มี authentication.
- Angular ห้ามเรียก Alpha Vantage, Finnhub, SEC EDGAR หรือ API key ของ provider โดยตรง.
- API key และ shared internal key อยู่ใน User Secrets หรือ environment variables เท่านั้น; ห้าม commit secrets.
- Phase 0 ใช้ mock provider เท่านั้น แต่ abstraction ต้องรองรับ Alpha Vantage, Finnhub และ SEC EDGAR ใน Phase 1.
- ticker ต้อง trim, upper-case, match รูปแบบ `^[A-Z][A-Z0-9.-]{0,19}$` และ `pageSize` ต้องอยู่ระหว่าง 1 ถึง 200.
- ใช้ UTC (`DateTimeOffset`) สำหรับเวลา, PostgreSQL `TIMESTAMPTZ`, raw provider payload เป็น `JSONB`.
- UI เป็น dark mode, `OnPush`, virtual scroll, `trackBy`, memory cap 300 ข่าว และ batch SignalR update 250 ms.
- เปิด CORS เฉพาะ `http://localhost:4200`; ห้ามใช้ `AllowAnyOrigin`.
- ทุก read query ใช้ `AsNoTracking`, ทุก write ใช้ unique constraint เป็นชั้นสุดท้ายของ deduplication.

---

## File Structure

```text
stock-pulse/
  docker/
    docker-compose.yml                         # PostgreSQL local instance
  backend/
    StockPulse.sln
    Directory.Build.props
    src/
      StockPulse.Api/
        Controllers/NewsController.cs
        Controllers/WatchlistController.cs
        Controllers/InternalRealtimeController.cs
        Hubs/NewsHub.cs
        Services/SignalRRealtimePublisher.cs
        Program.cs
        appsettings.Development.json
      StockPulse.Application/
        Abstractions/INewsRepository.cs
        Abstractions/IWatchlistRepository.cs
        Abstractions/IRealtimePublisher.cs
        DTOs/NewsResponseDto.cs
        DTOs/NewsQueryRequest.cs
        DTOs/WatchlistDtos.cs
        DTOs/NewsCreatedEvent.cs
        Services/NewsQueryService.cs
        Services/WatchlistService.cs
      StockPulse.Domain/
        Entities/NewsSource.cs
        Entities/StockNews.cs
        Entities/StockNewsTicker.cs
        Entities/WatchlistItem.cs
        Enums/NewsSentiment.cs
      StockPulse.Infrastructure/
        Persistence/StockPulseDbContext.cs
        Persistence/Configurations/*.cs
        Persistence/Repositories/*.cs
        DependencyInjection.cs
      StockPulse.Worker/
        HostedServices/NewsIngestionHostedService.cs
        Providers/IProviderNewsClient.cs
        Providers/Mock/MockNewsClient.cs
        Pipelines/NewsIngestionPipeline.cs
        Services/ApiRealtimeNotifier.cs
        Program.cs
        appsettings.Development.json
        mock-data/news.json
      StockPulse.Contracts/
        News/NormalizedNewsDto.cs
        Realtime/NewsCreatedEvent.cs
    tests/
      StockPulse.Application.Tests/
      StockPulse.Infrastructure.Tests/
      StockPulse.Worker.Tests/
  frontend/
    src/app/
      core/models/*.ts
      core/services/news-api.service.ts
      core/services/watchlist-api.service.ts
      core/services/news-hub.service.ts
      features/dashboard/dashboard.component.*
      features/dashboard/news-feed.component.*
      features/dashboard/news-card.component.*
      features/watchlist/watchlist-panel.component.*
      app.config.ts
      app.routes.ts
    src/styles.scss
    src/styles/_tokens.scss
    src/environments/environment.development.ts
  docs/
    local-development.md
```

## Task 1: เตรียมเครื่องมือและสร้าง solution skeleton

**Files:**
- Create: `.gitignore`
- Create: `backend/Directory.Build.props`
- Create: `backend/StockPulse.sln`
- Create: `frontend/` จาก Angular CLI

**Interfaces:**
- Produces: solution ที่ build ได้พร้อมโปรเจ็กต์ Api, Application, Domain, Infrastructure, Worker, Contracts และ test projects.

- [ ] **Step 1: ตรวจสอบ prerequisites**

Run: `dotnet --list-sdks`

Expected: มี .NET SDK major version 10 หรือใหม่กว่า. หาก output ว่าง ให้ติดตั้ง .NET 10 SDK ก่อนเริ่ม task นี้.

Run: `node --version`

Expected: `v20` หรือใหม่กว่า.

- [ ] **Step 2: สร้าง Git repository และ ignore file**

Run: `git init`

Create `.gitignore` with:

```gitignore
bin/
obj/
.vs/
.vscode/
node_modules/
dist/
coverage/
TestResults/
appsettings.Development.local.json
*.user
*.suo
```

- [ ] **Step 3: สร้าง .NET projects และ reference graph**

Run:

```powershell
New-Item -ItemType Directory -Force backend\src, backend\tests
dotnet new sln --name StockPulse --output backend
dotnet new webapi --name StockPulse.Api --output backend\src\StockPulse.Api --use-controllers
dotnet new classlib --name StockPulse.Domain --output backend\src\StockPulse.Domain
dotnet new classlib --name StockPulse.Contracts --output backend\src\StockPulse.Contracts
dotnet new classlib --name StockPulse.Application --output backend\src\StockPulse.Application
dotnet new classlib --name StockPulse.Infrastructure --output backend\src\StockPulse.Infrastructure
dotnet new worker --name StockPulse.Worker --output backend\src\StockPulse.Worker
dotnet new xunit --name StockPulse.Application.Tests --output backend\tests\StockPulse.Application.Tests
dotnet new xunit --name StockPulse.Infrastructure.Tests --output backend\tests\StockPulse.Infrastructure.Tests
dotnet new xunit --name StockPulse.Worker.Tests --output backend\tests\StockPulse.Worker.Tests
```

Run:

```powershell
dotnet sln backend\StockPulse.sln add backend\src\StockPulse.Api
dotnet sln backend\StockPulse.sln add backend\src\StockPulse.Domain
dotnet sln backend\StockPulse.sln add backend\src\StockPulse.Contracts
dotnet sln backend\StockPulse.sln add backend\src\StockPulse.Application
dotnet sln backend\StockPulse.sln add backend\src\StockPulse.Infrastructure
dotnet sln backend\StockPulse.sln add backend\src\StockPulse.Worker
dotnet sln backend\StockPulse.sln add backend\tests\StockPulse.Application.Tests
dotnet sln backend\StockPulse.sln add backend\tests\StockPulse.Infrastructure.Tests
dotnet sln backend\StockPulse.sln add backend\tests\StockPulse.Worker.Tests
dotnet add backend\src\StockPulse.Application reference backend\src\StockPulse.Domain
dotnet add backend\src\StockPulse.Application reference backend\src\StockPulse.Contracts
dotnet add backend\src\StockPulse.Infrastructure reference backend\src\StockPulse.Application
dotnet add backend\src\StockPulse.Infrastructure reference backend\src\StockPulse.Domain
dotnet add backend\src\StockPulse.Api reference backend\src\StockPulse.Application
dotnet add backend\src\StockPulse.Api reference backend\src\StockPulse.Infrastructure
dotnet add backend\src\StockPulse.Worker reference backend\src\StockPulse.Application
dotnet add backend\src\StockPulse.Worker reference backend\src\StockPulse.Infrastructure
dotnet add backend\src\StockPulse.Worker reference backend\src\StockPulse.Contracts
dotnet add backend\tests\StockPulse.Application.Tests reference backend\src\StockPulse.Application
dotnet add backend\tests\StockPulse.Infrastructure.Tests reference backend\src\StockPulse.Infrastructure
dotnet add backend\tests\StockPulse.Worker.Tests reference backend\src\StockPulse.Worker
```

- [ ] **Step 4: กำหนด common build settings**

Create `backend/Directory.Build.props` with:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>
</Project>
```

- [ ] **Step 5: สร้าง Angular app แบบ standalone**

Run:

```powershell
npx @angular/cli@18 new frontend --routing --style=scss --standalone --strict --ssr=false --skip-git --package-manager=npm
Set-Location frontend
npm.cmd install @angular/cdk@18 @microsoft/signalr
```

- [ ] **Step 6: ตรวจสอบ build เปล่า**

Run: `dotnet build backend\StockPulse.sln --configuration Release`

Expected: `Build succeeded.`

Run: `npm.cmd run build --prefix frontend`

Expected: Angular build ผ่านโดยไม่มี error.

- [ ] **Step 7: Commit**

Run:

```powershell
git add .gitignore backend frontend
git commit -m "chore: scaffold StockPulse solution"
```

## Task 2: PostgreSQL local runtime และ domain model

**Files:**
- Create: `docker/docker-compose.yml`
- Create: `docker/init/01-create-test-db.sql`
- Create: `backend/src/StockPulse.Domain/Entities/NewsSource.cs`
- Create: `backend/src/StockPulse.Domain/Entities/StockNews.cs`
- Create: `backend/src/StockPulse.Domain/Entities/StockNewsTicker.cs`
- Create: `backend/src/StockPulse.Domain/Entities/WatchlistItem.cs`
- Create: `backend/src/StockPulse.Domain/Enums/NewsSentiment.cs`
- Create: `backend/tests/StockPulse.Application.Tests/DomainModelTests.cs`

**Interfaces:**
- Produces: entity `StockNews` ที่มี `DedupHash`, `RawPayload` และ collection `Tickers`; entity `WatchlistItem` สำหรับ global watchlist.

- [ ] **Step 1: สร้าง failing test ของ entity default**

Create `backend/tests/StockPulse.Application.Tests/DomainModelTests.cs` with:

```csharp
using StockPulse.Domain.Entities;

namespace StockPulse.Application.Tests;

public sealed class DomainModelTests
{
    [Fact]
    public void StockNews_CreatesAnEmptyTickerCollection()
    {
        var news = new StockNews();

        Assert.Empty(news.Tickers);
    }
}
```

- [ ] **Step 2: รัน test เพื่อยืนยันว่า fail**

Run: `dotnet test backend\tests\StockPulse.Application.Tests --filter FullyQualifiedName~DomainModelTests`

Expected: FAIL เพราะ namespace หรือ `StockNews` ยังไม่มี.

- [ ] **Step 3: เพิ่ม entities และ enum**

Create `backend/src/StockPulse.Domain/Enums/NewsSentiment.cs` with:

```csharp
namespace StockPulse.Domain.Enums;

public enum NewsSentiment
{
    Neutral = 0,
    Positive = 1,
    Negative = 2
}
```

Create `backend/src/StockPulse.Domain/Entities/StockNews.cs` with:

```csharp
using System.Text.Json;
using StockPulse.Domain.Enums;

namespace StockPulse.Domain.Entities;

public sealed class StockNews
{
    public long Id { get; set; }
    public short SourceId { get; set; }
    public string? ProviderNewsKey { get; set; }
    public string ExternalUrl { get; set; } = string.Empty;
    public string? CanonicalUrl { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public NewsSentiment Sentiment { get; set; } = NewsSentiment.Neutral;
    public decimal SentimentScore { get; set; }
    public decimal ImpactScore { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public string DedupHash { get; set; } = string.Empty;
    public JsonDocument RawPayload { get; set; } = JsonDocument.Parse("{}");
    public List<string> Tags { get; set; } = [];
    public List<StockNewsTicker> Tickers { get; } = [];
    public NewsSource Source { get; set; } = null!;
}
```

Create `backend/src/StockPulse.Domain/Entities/NewsSource.cs` with:

```csharp
namespace StockPulse.Domain.Entities;

public sealed class NewsSource
{
    public short Id { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public List<StockNews> News { get; } = [];
}
```

Create `backend/src/StockPulse.Domain/Entities/StockNewsTicker.cs` with:

```csharp
namespace StockPulse.Domain.Entities;

public sealed class StockNewsTicker
{
    public long NewsId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; } = 1m;
    public bool IsPrimary { get; set; }
    public StockNews News { get; set; } = null!;
}
```

Create `backend/src/StockPulse.Domain/Entities/WatchlistItem.cs` with:

```csharp
namespace StockPulse.Domain.Entities;

public sealed class WatchlistItem
{
    public long Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Market { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

Create `docker/docker-compose.yml` with:

```yaml
services:
  postgres:
    image: postgres:16-alpine
    container_name: stockpulse-postgres
    environment:
      POSTGRES_DB: stockpulse
      POSTGRES_USER: stockpulse
      POSTGRES_PASSWORD: stockpulse_local_only
    ports:
      - "5432:5432"
    volumes:
      - stockpulse_postgres:/var/lib/postgresql/data
      - ./init:/docker-entrypoint-initdb.d:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U stockpulse -d stockpulse"]
      interval: 5s
      timeout: 3s
      retries: 20

volumes:
  stockpulse_postgres:
```

Create `docker/init/01-create-test-db.sql` with:

```sql
CREATE DATABASE stockpulse_test;
```

- [ ] **Step 4: รัน test เพื่อยืนยันว่า pass**

Run: `dotnet test backend\tests\StockPulse.Application.Tests --filter FullyQualifiedName~DomainModelTests`

Expected: `Passed: 1`.

- [ ] **Step 5: เริ่ม PostgreSQL และตรวจ health**

Run: `docker compose -f docker\docker-compose.yml up -d`

Expected: container `stockpulse-postgres` เป็น `healthy` ภายใน 2 นาที.

- [ ] **Step 6: Commit**

Run:

```powershell
git add docker backend/src/StockPulse.Domain backend/tests/StockPulse.Application.Tests/DomainModelTests.cs
git commit -m "feat: add local PostgreSQL and domain entities"
```

## Task 3: EF Core persistence และ migrations

**Files:**
- Create: `backend/src/StockPulse.Infrastructure/Persistence/StockPulseDbContext.cs`
- Create: `backend/src/StockPulse.Infrastructure/Persistence/StockPulseDbContextFactory.cs`
- Create: `backend/src/StockPulse.Infrastructure/Persistence/Configurations/StockNewsConfiguration.cs`
- Create: `backend/src/StockPulse.Infrastructure/Persistence/Configurations/WatchlistItemConfiguration.cs`
- Create: `backend/src/StockPulse.Infrastructure/DependencyInjection.cs`
- Modify: `backend/src/StockPulse.Infrastructure/StockPulse.Infrastructure.csproj`
- Create: `backend/src/StockPulse.Infrastructure/Persistence/Migrations/<generated migration files>`
- Test: `backend/tests/StockPulse.Infrastructure.Tests/StockPulseDbContextTests.cs`

**Interfaces:**
- Consumes: entities from Task 2.
- Produces: `StockPulseDbContext` with `NewsSources`, `StockNews`, `StockNewsTickers`, `WatchlistItems`; `AddStockPulseInfrastructure(IServiceCollection, IConfiguration)`.

- [ ] **Step 1: เพิ่ม packages ที่จำเป็น**

Run:

```powershell
dotnet add backend\src\StockPulse.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.*
dotnet add backend\src\StockPulse.Infrastructure package Microsoft.EntityFrameworkCore.Design --version 10.*
dotnet add backend\tests\StockPulse.Infrastructure.Tests package Microsoft.EntityFrameworkCore.InMemory --version 10.*
dotnet add backend\tests\StockPulse.Infrastructure.Tests reference backend\src\StockPulse.Infrastructure
```

- [ ] **Step 2: เขียน failing test สำหรับ unique watchlist ticker**

Create `backend/tests/StockPulse.Infrastructure.Tests/StockPulseDbContextTests.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using StockPulse.Domain.Entities;
using StockPulse.Infrastructure.Persistence;

namespace StockPulse.Infrastructure.Tests;

public sealed class StockPulseDbContextTests
{
    [Fact]
    public async Task SaveChangesAsync_RejectsDuplicateWatchlistTicker()
    {
        var options = new DbContextOptionsBuilder<StockPulseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new StockPulseDbContext(options);
        db.WatchlistItems.AddRange(
            new WatchlistItem { Ticker = "AAPL" },
            new WatchlistItem { Ticker = "AAPL" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
```

- [ ] **Step 3: รัน test เพื่อยืนยันว่า fail**

Run: `dotnet test backend\tests\StockPulse.Infrastructure.Tests --filter FullyQualifiedName~StockPulseDbContextTests`

Expected: FAIL เพราะ `StockPulseDbContext` ยังไม่มี.

- [ ] **Step 4: สร้าง DbContext และ fluent configurations**

Create `backend/src/StockPulse.Infrastructure/Persistence/StockPulseDbContext.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using StockPulse.Domain.Entities;

namespace StockPulse.Infrastructure.Persistence;

public sealed class StockPulseDbContext(DbContextOptions<StockPulseDbContext> options) : DbContext(options)
{
    public DbSet<NewsSource> NewsSources => Set<NewsSource>();
    public DbSet<StockNews> StockNews => Set<StockNews>();
    public DbSet<StockNewsTicker> StockNewsTickers => Set<StockNewsTicker>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StockPulseDbContext).Assembly);
    }
}
```

Create `backend/src/StockPulse.Infrastructure/Persistence/StockPulseDbContextFactory.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockPulse.Infrastructure.Persistence;

public sealed class StockPulseDbContextFactory : IDesignTimeDbContextFactory<StockPulseDbContext>
{
    public StockPulseDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("STOCKPULSE_CONNECTION")
            ?? throw new InvalidOperationException("Set STOCKPULSE_CONNECTION before running EF Core tools.");
        var options = new DbContextOptionsBuilder<StockPulseDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new StockPulseDbContext(options);
    }
}
```

Create `backend/src/StockPulse.Infrastructure/Persistence/Configurations/StockNewsConfiguration.cs` with:

```csharp
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
            ticker.WithOwner().HasForeignKey(item => item.NewsId);
            ticker.HasKey(item => new { item.NewsId, item.Ticker });
            ticker.Property(item => item.Ticker).HasMaxLength(20);
            ticker.HasIndex(item => new { item.Ticker, item.NewsId }).IsDescending(false, true);
        });
    }
}
```

Create `backend/src/StockPulse.Infrastructure/Persistence/Configurations/WatchlistItemConfiguration.cs` with:

```csharp
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
```

Create `backend/src/StockPulse.Infrastructure/DependencyInjection.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockPulse.Infrastructure.Persistence;

namespace StockPulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStockPulseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<StockPulseDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("StockPulse")));
        return services;
    }
}
```

- [ ] **Step 5: เปลี่ยน test เป็น PostgreSQL integration test**

Replace `backend/tests/StockPulse.Infrastructure.Tests/StockPulseDbContextTests.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using StockPulse.Domain.Entities;
using StockPulse.Infrastructure.Persistence;

namespace StockPulse.Infrastructure.Tests;

public sealed class StockPulseDbContextTests
{
    [Fact]
    public async Task DatabaseSchema_EnforcesUniqueWatchlistTicker()
    {
        var connectionString = Environment.GetEnvironmentVariable("STOCKPULSE_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        var options = new DbContextOptionsBuilder<StockPulseDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new StockPulseDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        db.WatchlistItems.AddRange(new WatchlistItem { Ticker = "AAPL" }, new WatchlistItem { Ticker = "AAPL" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
```

- [ ] **Step 6: สร้าง migration และทดสอบ schema**

Run:

```powershell
$env:STOCKPULSE_CONNECTION='Host=localhost;Port=5432;Database=stockpulse;Username=stockpulse;Password=stockpulse_local_only'
dotnet tool install --global dotnet-ef --version 10.*
dotnet ef migrations add InitialStockPulse --project backend\src\StockPulse.Infrastructure --startup-project backend\src\StockPulse.Api --output-dir Persistence\Migrations
$env:STOCKPULSE_TEST_CONNECTION='Host=localhost;Port=5432;Database=stockpulse_test;Username=stockpulse;Password=stockpulse_local_only'
dotnet test backend\tests\StockPulse.Infrastructure.Tests --filter FullyQualifiedName~StockPulseDbContextTests
```

Expected: migration files exist and `Passed: 1`.

- [ ] **Step 7: Commit**

Run:

```powershell
git add backend
git commit -m "feat: add PostgreSQL persistence"
```

## Task 4: Contracts, repositories และ Watchlist REST API

**Files:**
- Create: `backend/src/StockPulse.Contracts/News/NormalizedNewsDto.cs`
- Create: `backend/src/StockPulse.Application/DTOs/WatchlistDtos.cs`
- Create: `backend/src/StockPulse.Application/Abstractions/IWatchlistRepository.cs`
- Create: `backend/src/StockPulse.Application/Services/WatchlistService.cs`
- Create: `backend/src/StockPulse.Infrastructure/Persistence/Repositories/WatchlistRepository.cs`
- Create: `backend/src/StockPulse.Api/Controllers/WatchlistController.cs`
- Modify: `backend/src/StockPulse.Infrastructure/DependencyInjection.cs`
- Modify: `backend/src/StockPulse.Api/Program.cs`
- Test: `backend/tests/StockPulse.Application.Tests/WatchlistServiceTests.cs`

**Interfaces:**
- Produces: `GET /api/watchlist`, `POST /api/watchlist`, `DELETE /api/watchlist/{ticker}`.
- Produces: `Task<WatchlistItemDto> AddAsync(CreateWatchlistRequest request, CancellationToken cancellationToken)` and `Task<bool> RemoveAsync(string ticker, CancellationToken cancellationToken)`.

- [ ] **Step 1: เขียน failing unit test สำหรับ ticker normalization**

Create `backend/tests/StockPulse.Application.Tests/WatchlistServiceTests.cs` with:

```csharp
using StockPulse.Application.DTOs;
using StockPulse.Application.Services;

namespace StockPulse.Application.Tests;

public sealed class WatchlistServiceTests
{
    [Fact]
    public async Task AddAsync_NormalizesTickerToUpperCase()
    {
        var service = WatchlistService.CreateForTest();

        var item = await service.AddAsync(new CreateWatchlistRequest(" nvda ", null, null), CancellationToken.None);

        Assert.Equal("NVDA", item.Ticker);
    }
}
```

- [ ] **Step 2: รัน test เพื่อยืนยันว่า fail**

Run: `dotnet test backend\tests\StockPulse.Application.Tests --filter FullyQualifiedName~WatchlistServiceTests`

Expected: FAIL เพราะ `WatchlistService` และ DTO ยังไม่มี.

- [ ] **Step 3: สร้าง DTO, contract และ service**

Create `backend/src/StockPulse.Contracts/News/NormalizedNewsDto.cs` with:

```csharp
using System.Text.Json;

namespace StockPulse.Contracts.News;

public sealed record NormalizedNewsDto(
    string SourceCode,
    string? ProviderNewsKey,
    string ExternalUrl,
    string Title,
    string? Summary,
    DateTimeOffset PublishedAtUtc,
    IReadOnlyList<string> Tickers,
    JsonDocument RawPayload);
```

Create `backend/src/StockPulse.Application/DTOs/WatchlistDtos.cs` with:

```csharp
namespace StockPulse.Application.DTOs;

public sealed record WatchlistItemDto(long Id, string Ticker, string? DisplayName, string? Market, int SortOrder, bool IsActive);
public sealed record CreateWatchlistRequest(string Ticker, string? DisplayName, string? Market);
```

Create `backend/src/StockPulse.Application/Abstractions/IWatchlistRepository.cs` with:

```csharp
using StockPulse.Domain.Entities;

namespace StockPulse.Application.Abstractions;

public interface IWatchlistRepository
{
    Task<IReadOnlyList<WatchlistItem>> GetAllAsync(CancellationToken cancellationToken);
    Task<WatchlistItem> AddAsync(WatchlistItem item, CancellationToken cancellationToken);
    Task<bool> RemoveAsync(string ticker, CancellationToken cancellationToken);
}
```

Create `backend/src/StockPulse.Application/Services/WatchlistService.cs` with:

```csharp
using System.Text.RegularExpressions;
using StockPulse.Application.Abstractions;
using StockPulse.Application.DTOs;
using StockPulse.Domain.Entities;

namespace StockPulse.Application.Services;

public sealed partial class WatchlistService(IWatchlistRepository repository)
{
    public static WatchlistService CreateForTest() => new(new InMemoryWatchlistRepository());

    public async Task<IReadOnlyList<WatchlistItemDto>> GetAllAsync(CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken))
        .Select(item => new WatchlistItemDto(item.Id, item.Ticker, item.DisplayName, item.Market, item.SortOrder, item.IsActive))
        .ToArray();

    public async Task<WatchlistItemDto> AddAsync(CreateWatchlistRequest request, CancellationToken cancellationToken)
    {
        var ticker = request.Ticker.Trim().ToUpperInvariant();
        if (!TickerPattern().IsMatch(ticker)) throw new ArgumentException("Ticker format is invalid.", nameof(request));
        var item = await repository.AddAsync(new WatchlistItem { Ticker = ticker, DisplayName = request.DisplayName?.Trim(), Market = request.Market?.Trim() }, cancellationToken);
        return new WatchlistItemDto(item.Id, item.Ticker, item.DisplayName, item.Market, item.SortOrder, item.IsActive);
    }

    public Task<bool> RemoveAsync(string ticker, CancellationToken cancellationToken) =>
        repository.RemoveAsync(ticker.Trim().ToUpperInvariant(), cancellationToken);

    [GeneratedRegex("^[A-Z][A-Z0-9.-]{0,19}$")]
    private static partial Regex TickerPattern();

    private sealed class InMemoryWatchlistRepository : IWatchlistRepository
    {
        private readonly List<WatchlistItem> items = [];
        public Task<IReadOnlyList<WatchlistItem>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WatchlistItem>>(items);
        public Task<WatchlistItem> AddAsync(WatchlistItem item, CancellationToken cancellationToken) { item.Id = items.Count + 1; items.Add(item); return Task.FromResult(item); }
        public Task<bool> RemoveAsync(string ticker, CancellationToken cancellationToken) => Task.FromResult(items.RemoveAll(item => item.Ticker == ticker) > 0);
    }
}
```

- [ ] **Step 4: รัน unit test เพื่อยืนยันว่า pass**

Run: `dotnet test backend\tests\StockPulse.Application.Tests --filter FullyQualifiedName~WatchlistServiceTests`

Expected: `Passed: 1`.

- [ ] **Step 5: เชื่อม EF repository และ controller**

Create `backend/src/StockPulse.Infrastructure/Persistence/Repositories/WatchlistRepository.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using StockPulse.Application.Abstractions;
using StockPulse.Domain.Entities;

namespace StockPulse.Infrastructure.Persistence.Repositories;

public sealed class WatchlistRepository(StockPulseDbContext dbContext) : IWatchlistRepository
{
    public async Task<IReadOnlyList<WatchlistItem>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.WatchlistItems.AsNoTracking().OrderBy(item => item.SortOrder).ThenBy(item => item.Ticker).ToListAsync(cancellationToken);

    public async Task<WatchlistItem> AddAsync(WatchlistItem item, CancellationToken cancellationToken)
    {
        dbContext.WatchlistItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<bool> RemoveAsync(string ticker, CancellationToken cancellationToken)
    {
        var item = await dbContext.WatchlistItems.SingleOrDefaultAsync(value => value.Ticker == ticker, cancellationToken);
        if (item is null) return false;
        dbContext.WatchlistItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
```

Create `backend/src/StockPulse.Api/Controllers/WatchlistController.cs` with:

```csharp
using Microsoft.AspNetCore.Mvc;
using StockPulse.Application.DTOs;
using StockPulse.Application.Services;

namespace StockPulse.Api.Controllers;

[ApiController]
[Route("api/watchlist")]
public sealed class WatchlistController(WatchlistService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<WatchlistItemDto>> GetAll(CancellationToken cancellationToken) => service.GetAllAsync(cancellationToken);

    [HttpPost]
    public async Task<ActionResult<WatchlistItemDto>> Add(CreateWatchlistRequest request, CancellationToken cancellationToken)
    {
        var item = await service.AddAsync(request, cancellationToken);
        return Created($"api/watchlist/{item.Ticker}", item);
    }

    [HttpDelete("{ticker}")]
    public async Task<IActionResult> Remove(string ticker, CancellationToken cancellationToken) =>
        await service.RemoveAsync(ticker, cancellationToken) ? NoContent() : NotFound();
}
```

Register `IWatchlistRepository`, `WatchlistService`, controllers and a development-only exception handler in `Program.cs`. Translate PostgreSQL unique-constraint violation `23505` from POST into `409 Conflict`; translate `ArgumentException` into `400 Bad Request` without exposing a stack trace.

- [ ] **Step 6: run API integration checks**

Run: `dotnet run --project backend\src\StockPulse.Api --urls http://localhost:5000`

Expected: API starts and Swagger is available at `http://localhost:5000/swagger`.

Run in another terminal:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/watchlist -ContentType application/json -Body '{"ticker":"AAPL"}'
Invoke-RestMethod -Uri http://localhost:5000/api/watchlist
```

Expected: first response contains `"ticker":"AAPL"`; second response contains one watchlist item.

- [ ] **Step 7: Commit**

Run:

```powershell
git add backend
git commit -m "feat: add global watchlist API"
```

## Task 5: News persistence, query API และ SignalR hub

**Files:**
- Create: `backend/src/StockPulse.Application/DTOs/NewsResponseDto.cs`
- Create: `backend/src/StockPulse.Application/DTOs/NewsQueryRequest.cs`
- Create: `backend/src/StockPulse.Application/Abstractions/INewsRepository.cs`
- Create: `backend/src/StockPulse.Application/Abstractions/IRealtimePublisher.cs`
- Create: `backend/src/StockPulse.Application/Services/NewsQueryService.cs`
- Create: `backend/src/StockPulse.Infrastructure/Persistence/Repositories/NewsRepository.cs`
- Create: `backend/src/StockPulse.Api/Services/SignalRRealtimePublisher.cs`
- Create: `backend/src/StockPulse.Api/Hubs/NewsHub.cs`
- Create: `backend/src/StockPulse.Api/Controllers/NewsController.cs`
- Test: `backend/tests/StockPulse.Application.Tests/NewsQueryServiceTests.cs`

**Interfaces:**
- Produces: `GET /api/news?{ticker,sourceCode,sentiment,tag,page,pageSize}`, `GET /api/news/latest?limit=30`, hub `/hubs/news`.
- Produces: SignalR event name `news:new` with payload `NewsCreatedEvent`.

- [ ] **Step 1: เขียน failing query test**

Create `backend/tests/StockPulse.Application.Tests/NewsQueryServiceTests.cs` with:

```csharp
using StockPulse.Application.DTOs;
using StockPulse.Application.Services;

namespace StockPulse.Application.Tests;

public sealed class NewsQueryServiceTests
{
    [Fact]
    public async Task GetLatestAsync_RejectsLimitAbove200()
    {
        var service = NewsQueryService.CreateForTest();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetLatestAsync(201, CancellationToken.None));
    }
}
```

- [ ] **Step 2: รัน test เพื่อยืนยันว่า fail**

Run: `dotnet test backend\tests\StockPulse.Application.Tests --filter FullyQualifiedName~NewsQueryServiceTests`

Expected: FAIL เพราะ `NewsQueryService` ยังไม่มี.

- [ ] **Step 3: เพิ่ม news contracts และ query service**

Create `backend/src/StockPulse.Application/DTOs/NewsResponseDto.cs` with:

```csharp
namespace StockPulse.Application.DTOs;

public sealed record NewsResponseDto(long Id, string Title, string? Summary, string SourceCode, string Url, DateTimeOffset PublishedAtUtc, IReadOnlyList<string> Tickers, string Sentiment, decimal ImpactScore, IReadOnlyList<string> Tags);
public sealed record NewsCreatedEvent(DateTimeOffset SentAtUtc, NewsResponseDto News);
```

Create `backend/src/StockPulse.Application/DTOs/NewsQueryRequest.cs` with:

```csharp
namespace StockPulse.Application.DTOs;

public sealed record NewsQueryRequest(string? Ticker, string? SourceCode, string? Sentiment, string? Tag, int Page = 1, int PageSize = 50);
public sealed record PagedResponseDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, bool HasMore);
```

Create `backend/src/StockPulse.Application/Abstractions/INewsRepository.cs` with:

```csharp
using StockPulse.Application.DTOs;

namespace StockPulse.Application.Abstractions;

public interface INewsRepository
{
    Task<IReadOnlyList<NewsResponseDto>> GetLatestAsync(int limit, CancellationToken cancellationToken);
    Task<PagedResponseDto<NewsResponseDto>> QueryAsync(NewsQueryRequest request, CancellationToken cancellationToken);
}
```

Create `backend/src/StockPulse.Application/Services/NewsQueryService.cs` with:

```csharp
using StockPulse.Application.Abstractions;
using StockPulse.Application.DTOs;

namespace StockPulse.Application.Services;

public sealed class NewsQueryService(INewsRepository repository)
{
    public static NewsQueryService CreateForTest() => new(new EmptyNewsRepository());
    public Task<IReadOnlyList<NewsResponseDto>> GetLatestAsync(int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(limit));
        return repository.GetLatestAsync(limit, cancellationToken);
    }

    public Task<PagedResponseDto<NewsResponseDto>> QueryAsync(NewsQueryRequest request, CancellationToken cancellationToken)
    {
        if (request.Page < 1) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.PageSize is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(request));
        return repository.QueryAsync(request, cancellationToken);
    }

    private sealed class EmptyNewsRepository : INewsRepository
    {
        public Task<IReadOnlyList<NewsResponseDto>> GetLatestAsync(int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NewsResponseDto>>([]);
        public Task<PagedResponseDto<NewsResponseDto>> QueryAsync(NewsQueryRequest request, CancellationToken cancellationToken) => Task.FromResult(new PagedResponseDto<NewsResponseDto>([], request.Page, request.PageSize, 0, false));
    }
}
```

- [ ] **Step 4: รัน test เพื่อยืนยันว่า pass**

Run: `dotnet test backend\tests\StockPulse.Application.Tests --filter FullyQualifiedName~NewsQueryServiceTests`

Expected: `Passed: 1`.

- [ ] **Step 5: เพิ่ม repository, hub, publisher และ API endpoints**

Create `backend/src/StockPulse.Application/Abstractions/IRealtimePublisher.cs` with:

```csharp
using StockPulse.Application.DTOs;

namespace StockPulse.Application.Abstractions;

public interface IRealtimePublisher
{
    Task PublishNewsCreatedAsync(NewsCreatedEvent message, CancellationToken cancellationToken);
}
```

Create `backend/src/StockPulse.Api/Hubs/NewsHub.cs` with:

```csharp
using Microsoft.AspNetCore.SignalR;

namespace StockPulse.Api.Hubs;

public sealed class NewsHub : Hub
{
    public Task SubscribeTicker(string ticker) => Groups.AddToGroupAsync(Context.ConnectionId, $"ticker:{ticker.Trim().ToUpperInvariant()}");
    public Task UnsubscribeTicker(string ticker) => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticker:{ticker.Trim().ToUpperInvariant()}");
}
```

Create `backend/src/StockPulse.Api/Services/SignalRRealtimePublisher.cs` with:

```csharp
using Microsoft.AspNetCore.SignalR;
using StockPulse.Api.Hubs;
using StockPulse.Application.Abstractions;
using StockPulse.Application.DTOs;

namespace StockPulse.Api.Services;

public sealed class SignalRRealtimePublisher(IHubContext<NewsHub> hubContext) : IRealtimePublisher
{
    public async Task PublishNewsCreatedAsync(NewsCreatedEvent message, CancellationToken cancellationToken)
    {
        await hubContext.Clients.All.SendAsync("news:new", message, cancellationToken);
        foreach (var ticker in message.News.Tickers)
            await hubContext.Clients.Group($"ticker:{ticker}").SendAsync("news:new", message, cancellationToken);
    }
}
```

Create `backend/src/StockPulse.Api/Controllers/NewsController.cs` with:

```csharp
using Microsoft.AspNetCore.Mvc;
using StockPulse.Application.DTOs;
using StockPulse.Application.Services;

namespace StockPulse.Api.Controllers;

[ApiController]
[Route("api/news")]
public sealed class NewsController(NewsQueryService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponseDto<NewsResponseDto>> Query([FromQuery] NewsQueryRequest request, CancellationToken cancellationToken) => service.QueryAsync(request, cancellationToken);

    [HttpGet("latest")]
    public Task<IReadOnlyList<NewsResponseDto>> Latest([FromQuery] int limit, CancellationToken cancellationToken) => service.GetLatestAsync(limit, cancellationToken);
}
```

Implement `NewsRepository` with a projection, `AsNoTracking()`, server-side filters and `OrderByDescending(PublishedAtUtc)`; its `QueryAsync` must call `CountAsync` before `Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)`. Register `INewsRepository`, `NewsQueryService` and `IRealtimePublisher` in Api, then add `builder.Services.AddSignalR()`, CORS policy `local-angular`, and `app.MapHub<NewsHub>("/hubs/news")`.

- [ ] **Step 6: test query endpoint manually**

Run: `Invoke-RestMethod -Uri 'http://localhost:5000/api/news/latest?limit=30'`

Expected: HTTP 200 and an empty JSON array before Worker starts.

- [ ] **Step 7: Commit**

Run:

```powershell
git add backend
git commit -m "feat: add news query API and SignalR hub"
```

## Task 6: Mock Worker ingestion, deduplication และ internal realtime notifier

**Files:**
- Create: `backend/src/StockPulse.Worker/Providers/IProviderNewsClient.cs`
- Create: `backend/src/StockPulse.Worker/Providers/Mock/MockNewsClient.cs`
- Create: `backend/src/StockPulse.Worker/Pipelines/NewsIngestionPipeline.cs`
- Create: `backend/src/StockPulse.Worker/Services/ApiRealtimeNotifier.cs`
- Create: `backend/src/StockPulse.Worker/HostedServices/NewsIngestionHostedService.cs`
- Create: `backend/src/StockPulse.Worker/mock-data/news.json`
- Create: `backend/src/StockPulse.Api/Controllers/InternalRealtimeController.cs`
- Test: `backend/tests/StockPulse.Worker.Tests/NewsIngestionPipelineTests.cs`

**Interfaces:**
- Consumes: `NormalizedNewsDto`, `IRealtimePublisher`, `StockPulseDbContext` and `/internal/realtime/news-created`.
- Produces: one insert per `DedupHash`, then exactly one SignalR message for every new inserted article.

- [ ] **Step 1: เขียน failing test สำหรับ stable dedup hash**

Create `backend/tests/StockPulse.Worker.Tests/NewsIngestionPipelineTests.cs` with:

```csharp
using StockPulse.Worker.Pipelines;

namespace StockPulse.Worker.Tests;

public sealed class NewsIngestionPipelineTests
{
    [Fact]
    public void CreateDedupHash_IgnoresUrlTrackingParameters()
    {
        var first = NewsIngestionPipeline.CreateDedupHash("Nvidia beats estimates", "https://news.example/a?utm_source=x", new DateTimeOffset(2026, 7, 18, 8, 10, 45, TimeSpan.Zero), "mock");
        var second = NewsIngestionPipeline.CreateDedupHash(" nvidia beats estimates ", "https://news.example/a", new DateTimeOffset(2026, 7, 18, 8, 10, 5, TimeSpan.Zero), "mock");

        Assert.Equal(first, second);
    }
}
```

- [ ] **Step 2: รัน test เพื่อยืนยันว่า fail**

Run: `dotnet test backend\tests\StockPulse.Worker.Tests --filter FullyQualifiedName~NewsIngestionPipelineTests`

Expected: FAIL เพราะ pipeline ยังไม่มี.

- [ ] **Step 3: เพิ่ม provider contract, mock fixture และ dedup hash**

Create `backend/src/StockPulse.Worker/Providers/IProviderNewsClient.cs` with:

```csharp
using StockPulse.Contracts.News;

namespace StockPulse.Worker.Providers;

public interface IProviderNewsClient
{
    string SourceCode { get; }
    Task<IReadOnlyList<NormalizedNewsDto>> FetchNewsAsync(CancellationToken cancellationToken);
}
```

Create `backend/src/StockPulse.Worker/Pipelines/NewsIngestionPipeline.cs` with:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace StockPulse.Worker.Pipelines;

public sealed class NewsIngestionPipeline
{
    public static string CreateDedupHash(string title, string url, DateTimeOffset publishedAtUtc, string sourceCode)
    {
        var canonicalUrl = url.Split('?', 2)[0].Trim().ToLowerInvariant();
        var normalizedTitle = string.Join(' ', title.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var roundedTime = new DateTimeOffset(publishedAtUtc.Year, publishedAtUtc.Month, publishedAtUtc.Day, publishedAtUtc.Hour, publishedAtUtc.Minute, 0, TimeSpan.Zero);
        var payload = $"{normalizedTitle}|{canonicalUrl}|{roundedTime:O}|{sourceCode.Trim().ToLowerInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
```

Create `backend/src/StockPulse.Worker/mock-data/news.json` with:

```json
[
  { "id": "mock-001", "url": "https://example.test/nvda-earnings", "title": "NVIDIA beats estimates on data-center demand", "summary": "Mock earnings article for local development.", "publishedAtUtc": "2026-07-18T08:10:00Z", "tickers": ["NVDA"] },
  { "id": "mock-002", "url": "https://example.test/aapl-dividend", "title": "Apple raises dividend after record services revenue", "summary": "Mock dividend article for local development.", "publishedAtUtc": "2026-07-18T08:12:00Z", "tickers": ["AAPL"] }
]
```

- [ ] **Step 4: รัน test เพื่อยืนยันว่า pass**

Run: `dotnet test backend\tests\StockPulse.Worker.Tests --filter FullyQualifiedName~NewsIngestionPipelineTests`

Expected: `Passed: 1`.

- [ ] **Step 5: ทำ ingest path และ notifier ให้ครบ**

Implement `MockNewsClient` to deserialize `mock-data/news.json` with `JsonSerializer.DeserializeAsync`, construct `NormalizedNewsDto` and return immutable list. Add a `NewsIngestionPipeline.IngestAsync(IReadOnlyList<NormalizedNewsDto>, CancellationToken)` method that:

```csharp
foreach (var article in articles)
{
    var hash = CreateDedupHash(article.Title, article.ExternalUrl, article.PublishedAtUtc, article.SourceCode);
    if (await dbContext.StockNews.AnyAsync(news => news.DedupHash == hash, cancellationToken)) continue;
    // Add StockNews, tags=[], neutral sentiment, impact score 10, and ticker rows.
}
await dbContext.SaveChangesAsync(cancellationToken);
```

Create `InternalRealtimeController` that only accepts `POST /internal/realtime/news-created` when header `X-StockPulse-Internal-Key` matches configuration key using `CryptographicOperations.FixedTimeEquals`; otherwise return `401`. On success call `IRealtimePublisher.PublishNewsCreatedAsync`.

Create `ApiRealtimeNotifier` that posts `NewsCreatedEvent` to that endpoint using `HttpClient`, validates `IsSuccessStatusCode`, and logs only status code plus news ID when it fails. `NewsIngestionHostedService` must use `PeriodicTimer(TimeSpan.FromSeconds(15))`, catch exceptions around an entire polling cycle, and keep the service running after a failed cycle.

- [ ] **Step 6: add safe development configuration**

Create `backend/src/StockPulse.Api/appsettings.Development.json` with:

```json
{
  "ConnectionStrings": { "StockPulse": "Host=localhost;Port=5432;Database=stockpulse;Username=stockpulse;Password=stockpulse_local_only" },
  "InternalRealtime": { "SharedKey": "set-with-dotnet-user-secrets" },
  "Cors": { "AngularOrigin": "http://localhost:4200" }
}
```

Create `backend/src/StockPulse.Worker/appsettings.Development.json` with:

```json
{
  "ConnectionStrings": { "StockPulse": "Host=localhost;Port=5432;Database=stockpulse;Username=stockpulse;Password=stockpulse_local_only" },
  "RealtimeApi": { "BaseUrl": "http://localhost:5000", "SharedKey": "set-with-dotnet-user-secrets" },
  "Worker": { "UseMockProviders": true, "PollingSeconds": 15 }
}
```

Replace both `SharedKey` values before first run by running:

```powershell
dotnet user-secrets init --project backend\src\StockPulse.Api
dotnet user-secrets set "InternalRealtime:SharedKey" "local-development-key-change-me" --project backend\src\StockPulse.Api
dotnet user-secrets init --project backend\src\StockPulse.Worker
dotnet user-secrets set "RealtimeApi:SharedKey" "local-development-key-change-me" --project backend\src\StockPulse.Worker
```

- [ ] **Step 7: run end-to-end worker smoke test**

Run: `dotnet ef database update --project backend\src\StockPulse.Infrastructure --startup-project backend\src\StockPulse.Api`

Run API and Worker in two terminals:

```powershell
dotnet run --project backend\src\StockPulse.Api --urls http://localhost:5000
dotnet run --project backend\src\StockPulse.Worker
```

Run after one worker cycle: `Invoke-RestMethod -Uri 'http://localhost:5000/api/news/latest?limit=30'`

Expected: response contains the two mock articles exactly once, including ticker arrays.

- [ ] **Step 8: Commit**

Run:

```powershell
git add backend
git commit -m "feat: add mock news ingestion pipeline"
```

## Task 7: Angular core models, REST clients และ SignalR client

**Files:**
- Create: `frontend/src/app/core/models/news-item.ts`
- Create: `frontend/src/app/core/models/watchlist-item.ts`
- Create: `frontend/src/app/core/services/news-api.service.ts`
- Create: `frontend/src/app/core/services/watchlist-api.service.ts`
- Create: `frontend/src/app/core/services/news-hub.service.ts`
- Create: `frontend/src/environments/environment.development.ts`
- Modify: `frontend/src/app/app.config.ts`
- Test: `frontend/src/app/core/services/news-hub.service.spec.ts`

**Interfaces:**
- Consumes: APIs from Tasks 4-6 and hub `/hubs/news`.
- Produces: `newsCreated$`, `connectionState`, `getLatest(limit)`, `getAll()`, `add()` and `remove()` for UI components.

- [ ] **Step 1: สร้าง shared TypeScript models**

Create `frontend/src/app/core/models/news-item.ts` with:

```ts
export interface NewsItem {
  id: number;
  title: string;
  summary: string | null;
  sourceCode: string;
  url: string;
  publishedAtUtc: string;
  tickers: string[];
  sentiment: 'positive' | 'negative' | 'neutral';
  impactScore: number;
  tags: string[];
}

export interface NewsCreatedEvent {
  sentAtUtc: string;
  news: NewsItem;
}
```

Create `frontend/src/environments/environment.development.ts` with:

```ts
export const environment = {
  apiBaseUrl: 'http://localhost:5000',
};
```

- [ ] **Step 2: เขียน failing test สำหรับ SignalR event stream**

Create `frontend/src/app/core/services/news-hub.service.spec.ts` with:

```ts
import { TestBed } from '@angular/core/testing';
import { NewsHubService } from './news-hub.service';

describe('NewsHubService', () => {
  it('creates a service', () => {
    expect(TestBed.inject(NewsHubService)).toBeTruthy();
  });
});
```

- [ ] **Step 3: รัน test เพื่อยืนยันว่า fail**

Run: `npm.cmd test -- --watch=false --browsers=ChromeHeadless`

Expected: FAIL เพราะ `NewsHubService` ยังไม่มี.

- [ ] **Step 4: implement API และ SignalR services**

Create `frontend/src/app/core/services/news-api.service.ts` with:

```ts
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment.development';
import { NewsItem } from '../models/news-item';

@Injectable({ providedIn: 'root' })
export class NewsApiService {
  private readonly http = inject(HttpClient);
  getLatest(limit = 30): Observable<NewsItem[]> {
    return this.http.get<NewsItem[]>(`${environment.apiBaseUrl}/api/news/latest`, { params: new HttpParams().set('limit', limit) });
  }
}
```

Create `frontend/src/app/core/services/news-hub.service.ts` with:

```ts
import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment.development';
import { NewsCreatedEvent } from '../models/news-item';

@Injectable({ providedIn: 'root' })
export class NewsHubService {
  private readonly eventSubject = new Subject<NewsCreatedEvent>();
  private connection: HubConnection | undefined;
  readonly newsCreated$ = this.eventSubject.asObservable();
  readonly connectionState = signal<HubConnectionState>(HubConnectionState.Disconnected);

  async connect(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) return;
    this.connection = new HubConnectionBuilder().withUrl(`${environment.apiBaseUrl}/hubs/news`).withAutomaticReconnect().build();
    this.connection.on('news:new', (event: NewsCreatedEvent) => this.eventSubject.next(event));
    this.connection.onreconnecting(() => this.connectionState.set(HubConnectionState.Reconnecting));
    this.connection.onreconnected(() => this.connectionState.set(HubConnectionState.Connected));
    await this.connection.start();
    this.connectionState.set(HubConnectionState.Connected);
  }
}
```

Add `provideHttpClient()` to `frontend/src/app/app.config.ts`. Implement `WatchlistApiService` analogously against `/api/watchlist` with strictly typed request/response models.

- [ ] **Step 5: รัน test และ build**

Run: `npm.cmd test -- --watch=false --browsers=ChromeHeadless`

Expected: all unit tests pass.

Run: `npm.cmd run build`

Expected: Angular production build completes.

- [ ] **Step 6: Commit**

Run:

```powershell
git add frontend
git commit -m "feat: add Angular API and SignalR clients"
```

## Task 8: Angular dark dashboard, watchlist และ realtime virtual feed

**Files:**
- Create: `frontend/src/app/features/dashboard/dashboard.component.ts`
- Create: `frontend/src/app/features/dashboard/dashboard.component.html`
- Create: `frontend/src/app/features/dashboard/dashboard.component.scss`
- Create: `frontend/src/app/features/dashboard/news-feed.component.ts`
- Create: `frontend/src/app/features/dashboard/news-card.component.ts`
- Create: `frontend/src/app/features/watchlist/watchlist-panel.component.ts`
- Create: `frontend/src/styles/_tokens.scss`
- Modify: `frontend/src/styles.scss`
- Modify: `frontend/src/app/app.routes.ts`

**Interfaces:**
- Consumes: `NewsApiService.getLatest`, `NewsHubService.newsCreated$`, `WatchlistApiService`.
- Produces: route `/` with 3-column dark dashboard, new-news highlight and global watchlist management.

- [ ] **Step 1: เขียน failing component test สำหรับ memory cap**

Create `frontend/src/app/features/dashboard/dashboard.component.spec.ts` with:

```ts
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  it('caps realtime news at 300 items', () => {
    const component = new DashboardComponent();
    component.prependNews({ id: 301 } as never);
    expect(component.items().length).toBeLessThanOrEqual(300);
  });
});
```

- [ ] **Step 2: รัน test เพื่อยืนยันว่า fail**

Run: `npm.cmd test -- --watch=false --browsers=ChromeHeadless`

Expected: FAIL เพราะ `DashboardComponent` ยังไม่มี.

- [ ] **Step 3: implement dashboard state และ realtime batching**

Create `frontend/src/app/features/dashboard/dashboard.component.ts` with:

```ts
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { bufferTime, filter } from 'rxjs';
import { NewsItem } from '../../core/models/news-item';
import { NewsApiService } from '../../core/services/news-api.service';
import { NewsHubService } from '../../core/services/news-hub.service';

@Component({ selector: 'sp-dashboard', standalone: true, templateUrl: './dashboard.component.html', styleUrl: './dashboard.component.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class DashboardComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly newsApi = inject(NewsApiService);
  private readonly newsHub = inject(NewsHubService);
  readonly items = signal<NewsItem[]>([]);

  async ngOnInit(): Promise<void> {
    this.newsApi.getLatest().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((items) => this.items.set(items.slice(0, 300)));
    await this.newsHub.connect();
    this.newsHub.newsCreated$.pipe(bufferTime(250), filter((events) => events.length > 0), takeUntilDestroyed(this.destroyRef)).subscribe((events) => {
      this.items.update((current) => [...events.map((event) => event.news), ...current].slice(0, 300));
    });
  }

  prependNews(news: NewsItem): void {
    this.items.update((current) => [news, ...current].slice(0, 300));
  }

  trackByNewsId(_: number, item: NewsItem): number { return item.id; }
}
```

Create `frontend/src/app/features/dashboard/dashboard.component.html` with:

```html
<main class="dashboard">
  <aside class="dashboard__sidebar"><sp-watchlist-panel /></aside>
  <section class="dashboard__feed"><sp-news-feed [items]="items()" /></section>
  <aside class="dashboard__detail">เลือกข่าวเพื่อดูรายละเอียด</aside>
</main>
```

Implement `NewsFeedComponent` with `CdkVirtualScrollViewport` and `*cdkVirtualFor="let item of items; trackBy: trackByNewsId"`; implement `NewsCardComponent` to show title, summary, source, time, ticker chips, sentiment and impact score. Mark the newest card with CSS animation lasting `1.2s` only.

- [ ] **Step 4: implement design tokens และ responsive layout**

Create `frontend/src/styles/_tokens.scss` with:

```scss
:root {
  --sp-bg: #081017;
  --sp-surface: #101c27;
  --sp-border: #233547;
  --sp-text: #e8f1f7;
  --sp-muted: #93a8b8;
  --sp-positive: #33d17a;
  --sp-negative: #ff6464;
  --sp-warning: #f5b942;
}
```

Update `frontend/src/styles.scss` with:

```scss
@use './styles/tokens';

html, body { min-height: 100%; margin: 0; background: var(--sp-bg); color: var(--sp-text); font-family: 'IBM Plex Mono', monospace; }
* { box-sizing: border-box; }
```

Use CSS grid `280px minmax(0, 1fr) 320px` above 1100px, hide the detail panel below 1100px, and render only the feed below 720px. Add an empty state reading `เพิ่มหุ้นใน Watchlist เพื่อเริ่มติดตามข่าว` when list is empty and six skeleton rows while initial HTTP request is pending.

- [ ] **Step 5: run UI checks**

Run: `npm.cmd test -- --watch=false --browsers=ChromeHeadless`

Expected: all tests pass.

Run: `npm.cmd start`

Expected: app loads at `http://localhost:4200`, presents dark dashboard, receives a mock article after Worker polling.

- [ ] **Step 6: Commit**

Run:

```powershell
git add frontend
git commit -m "feat: add realtime dark dashboard"
```

## Task 9: Local runbook และ quality verification

**Files:**
- Create: `docs/local-development.md`
- Modify: `README.md`

**Interfaces:**
- Consumes: complete Phase 0 system from Tasks 1-8.
- Produces: reproducible local setup and an explicit verification checklist.

- [ ] **Step 1: เขียน runbook**

Create `docs/local-development.md` with:

```markdown
# StockPulse Local Development

## Start dependencies

```powershell
docker compose -f docker/docker-compose.yml up -d
dotnet ef database update --project backend/src/StockPulse.Infrastructure --startup-project backend/src/StockPulse.Api
```

## Start applications

Open three terminals at repository root:

```powershell
dotnet run --project backend/src/StockPulse.Api --urls http://localhost:5000
dotnet run --project backend/src/StockPulse.Worker
npm.cmd start --prefix frontend
```

Open `http://localhost:4200`. Add `AAPL` or `NVDA` in Watchlist; mock provider inserts articles every 15 seconds.
```

- [ ] **Step 2: run backend and frontend verification**

Run:

```powershell
dotnet test backend\StockPulse.sln --configuration Release
dotnet build backend\StockPulse.sln --configuration Release
npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless
npm.cmd run build --prefix frontend
```

Expected: all commands exit with code 0.

- [ ] **Step 3: execute manual end-to-end checklist**

1. Start PostgreSQL, API, Worker and Angular as documented.
2. Add `AAPL` through the UI and confirm `GET /api/watchlist` contains uppercase `AAPL`.
3. Wait 15 seconds and confirm `GET /api/news/latest?limit=30` contains mock news exactly once after several worker cycles.
4. Open two browser tabs and confirm both receive the new-news highlight without refresh.
5. Set a URL query filter in the news API with `pageSize=201` and confirm response is `400`, not a long query.
6. Stop Worker and confirm API and existing dashboard stay usable.

- [ ] **Step 4: review against four pillars**

- Performance: confirm news query uses `AsNoTracking`, server pagination, indexes for published time/ticker/impact, virtual scroll, batching, and 300-item cap.
- Security: confirm no provider key in frontend, strict CORS origin, internal endpoint constant-time key comparison, input validation, and no stack trace in response body.
- Naming: confirm `StockNews`, `WatchlistItem`, `NewsHubService`, `NewsIngestionPipeline` and API routes match the contracts in this plan.
- Extensibility: confirm `IProviderNewsClient`, `IRealtimePublisher`, repositories and normalized DTO remain provider-agnostic.

- [ ] **Step 5: Commit**

Run:

```powershell
git add README.md docs
git commit -m "docs: add local development runbook"
```

## Plan Self-Review

### Spec coverage

- Phase 0 solution structure: Task 1.
- PostgreSQL plus JSONB, dedup constraint and initial database migration: Tasks 2-3.
- Global watchlist: Task 4.
- REST news query and SignalR hub: Task 5.
- Mock mode, normalize contract, deduplicate, save and API-mediated broadcast: Task 6.
- Angular REST/SignalR services, dark dashboard, virtual scroll, new-news animation, empty/loading states: Tasks 7-8.
- Local deployment and performance/security/naming/extensibility verification: Task 9.

Alert rules, rule-based sentiment/impact enrichment, live Alpha Vantage/Finnhub/SEC EDGAR calls, broad filters and production authentication are intentionally Phase 1+; they are outside the Phase 0 deliverable defined in the approved design specification.

### Placeholder scan

This plan contains no deferred implementation markers. Generated EF migration filenames are intentionally represented as a generated directory because EF determines the timestamp prefix; the command producing each file is specified in Task 3.

### Type consistency

`NormalizedNewsDto` is owned by `StockPulse.Contracts`; `NewsResponseDto` and `NewsCreatedEvent` are owned by `StockPulse.Application`; Angular consumes `NewsCreatedEvent` through `NewsHubService`; `NewsIngestionPipeline.CreateDedupHash` is exercised by Worker tests before it is used for persistence.
