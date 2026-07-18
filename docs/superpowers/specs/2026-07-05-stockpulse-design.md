# StockPulse Design Specification

**Date:** 2026-07-05  
**Scope:** Local-first MVP for a real-time stock news dashboard  
**Decisions locked in:** Single-user MVP, Angular frontend, .NET Web API backend, dedicated .NET Worker, SignalR, PostgreSQL, in-memory cache, Alpha Vantage + Finnhub + SEC EDGAR from day one, rule-based sentiment/impact, ticker scope = watchlist + popular tickers

---

## 1. Architecture Overview

### 1.1 High-level Architecture

StockPulse ใช้สถาปัตยกรรมแบบ `Split API + Dedicated Worker Service` เพื่อแยกภาระหน้าที่ชัดเจนตั้งแต่ต้น แต่ยังคง local setup ที่เรียบง่ายพอสำหรับ MVP

Components หลัก:

- `Angular Web App`
  - แสดง dashboard แบบ real-time
  - จัดการ watchlist, alert rules, filters
  - เชื่อม REST API และ SignalR
- `StockPulse.Api`
  - ให้บริการ REST API สำหรับ query และ command
  - เปิด `NewsHub` สำหรับ SignalR
  - query ข้อมูลจาก PostgreSQL
  - trigger realtime broadcast ผ่าน notifier service
- `StockPulse.Worker`
  - polling ข่าวจาก Alpha Vantage, Finnhub, SEC EDGAR
  - normalize ข้อมูล
  - deduplicate ข่าว
  - คำนวณ sentiment, impact score, tags
  - save ลง PostgreSQL
  - ประเมิน alert rule และแจ้ง API ให้ broadcast
- `PostgreSQL`
  - เก็บข่าว, ticker mapping, watchlist, alert rules, sync states
- `In-memory Cache`
  - cache metadata เช่น source list, ticker universe, short-lived dedup window

### 1.2 End-to-end Flow

```text
Provider API
  -> Worker Scheduler
  -> Provider Client
  -> Raw Response Parser
  -> Normalizer
  -> Deduplicator
  -> Ticker Mapper
  -> Sentiment Scorer
  -> Impact Scorer
  -> Tag Classifier
  -> PostgreSQL Save
  -> Alert Evaluator
  -> Realtime Notifier
  -> SignalR Hub
  -> Angular Dashboard
```

### 1.3 Flow อธิบายแบบเข้าใจง่าย

1. Worker โหลด `watchlist + popular tickers`
2. Worker เรียก provider ตาม schedule ของแต่ละเจ้า
3. Response จาก provider ถูกแปลงเป็น DTO กลาง
4. ระบบสร้าง `dedup_hash` จากข้อมูลหลักของข่าว
5. ถ้ายังไม่เคยมีข่าวนี้ใน DB ให้ enrich ต่อ
6. ระบบ map ticker ที่เกี่ยวข้อง, ให้ sentiment, impact score และ tags
7. บันทึกข่าวลง PostgreSQL พร้อม `raw_payload` แบบ JSONB
8. ประเมิน alert rules ที่ active
9. API broadcast event ผ่าน SignalR ไปยัง Angular
10. Angular อัปเดตข่าวบน dashboard แบบ real-time

### 1.4 Architecture Boundary

- Angular ห้ามเรียก provider โดยตรง
- API key อยู่ฝั่ง backend เท่านั้น
- Worker ไม่ push ตรงเข้า client
- Provider-specific mapping อยู่ใน adapter/client ของแต่ละ provider เท่านั้น
- รูปแบบข่าวกลางต้องไม่ผูกกับ shape ของ provider รายใดรายหนึ่ง

---

## 2. Project Structure

### 2.1 Top-level Structure

```text
stock-pulse/
  frontend/
  backend/
  docs/
    superpowers/
      specs/
      plans/
  scripts/
  docker/
```

### 2.2 Angular Structure

```text
frontend/
  src/
    app/
      core/
        constants/
        interceptors/
        models/
        services/
        store/
      features/
        dashboard/
          components/
            dashboard-shell/
            top-bar/
            news-feed/
            news-card/
            stock-detail-panel/
            filter-drawer/
          pages/
          services/
          models/
        watchlist/
          components/
          services/
          models/
        alerts/
          components/
          services/
          models/
      shared/
        components/
        directives/
        pipes/
        utils/
      layouts/
      app.routes.ts
      app.config.ts
    assets/
    styles/
      _tokens.scss
      _theme.dark.scss
      _animations.scss
      styles.scss
```

Responsibility:

- `core/` ของใช้ร่วมทั้งแอป
- `features/dashboard/` ของ dashboard real-time โดยเฉพาะ
- `shared/` reusable UI และ utility
- `styles/` design tokens, dark theme, animation

### 2.3 .NET Backend Structure

```text
backend/
  src/
    StockPulse.Api/
      Controllers/
      Hubs/
      Middleware/
      Extensions/
      Program.cs
      appsettings.json

    StockPulse.Application/
      Abstractions/
      DTOs/
      Interfaces/
      Mappers/
      Notifications/
      Rules/
      Services/

    StockPulse.Domain/
      Entities/
      Enums/
      ValueObjects/
      Constants/

    StockPulse.Infrastructure/
      Persistence/
        Configurations/
        Repositories/
        Migrations/
      Providers/
        AlphaVantage/
        Finnhub/
        SecEdgar/
      Caching/
      Logging/
      Options/
      SignalR/

    StockPulse.Worker/
      HostedServices/
      Jobs/
      Pipelines/
      Providers/
      Schedulers/
      Program.cs
      appsettings.json

    StockPulse.Contracts/
      News/
      Watchlist/
      Alerts/
      Realtime/

  tests/
    StockPulse.Api.Tests/
    StockPulse.Application.Tests/
    StockPulse.Infrastructure.Tests/
    StockPulse.Worker.Tests/
```

### 2.4 Layer Responsibility

- `Api`
  - REST controllers
  - SignalR hub
  - request validation entry point
- `Application`
  - use cases และ business orchestration
- `Domain`
  - entity และค่ากลางทางธุรกิจ
- `Infrastructure`
  - EF Core/PostgreSQL, provider clients, repositories, cache
- `Worker`
  - polling และ ingest pipeline
- `Contracts`
  - shared DTO/SignalR payload contract

---

## 3. PostgreSQL Database Design

### 3.1 Table: `news_sources`

```sql
CREATE TABLE news_sources (
    id                   SMALLSERIAL PRIMARY KEY,
    source_code          VARCHAR(50) NOT NULL UNIQUE,
    source_name          VARCHAR(100) NOT NULL,
    provider_type        VARCHAR(50) NOT NULL,
    base_url             VARCHAR(255) NULL,
    is_enabled           BOOLEAN NOT NULL DEFAULT TRUE,
    default_poll_seconds INT NOT NULL DEFAULT 300,
    created_at_utc       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 3.2 Table: `stock_news`

```sql
CREATE TABLE stock_news (
    id                   BIGSERIAL PRIMARY KEY,
    source_id            SMALLINT NOT NULL REFERENCES news_sources(id),
    provider_news_key    VARCHAR(200) NULL,
    external_url         TEXT NOT NULL,
    canonical_url        TEXT NULL,
    title                TEXT NOT NULL,
    summary              TEXT NULL,
    content              TEXT NULL,
    language_code        VARCHAR(10) NULL,
    sentiment            VARCHAR(20) NOT NULL DEFAULT 'neutral',
    sentiment_score      NUMERIC(5,2) NOT NULL DEFAULT 0,
    impact_score         NUMERIC(5,2) NOT NULL DEFAULT 0,
    impact_level         VARCHAR(20) NOT NULL DEFAULT 'low',
    published_at_utc     TIMESTAMPTZ NOT NULL,
    received_at_utc      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    dedup_hash           CHAR(64) NOT NULL,
    raw_payload          JSONB NOT NULL,
    tags                 JSONB NOT NULL DEFAULT '[]'::jsonb,
    processing_version   INT NOT NULL DEFAULT 1,
    is_deleted           BOOLEAN NOT NULL DEFAULT FALSE
);
```

### 3.3 Table: `stock_news_tickers`

```sql
CREATE TABLE stock_news_tickers (
    news_id           BIGINT NOT NULL REFERENCES stock_news(id) ON DELETE CASCADE,
    ticker            VARCHAR(20) NOT NULL,
    match_type        VARCHAR(30) NOT NULL,
    confidence_score  NUMERIC(5,2) NOT NULL DEFAULT 1.00,
    is_primary        BOOLEAN NOT NULL DEFAULT FALSE,
    created_at_utc    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (news_id, ticker)
);
```

### 3.4 Table: `watchlists`

```sql
CREATE TABLE watchlists (
    id              BIGSERIAL PRIMARY KEY,
    ticker          VARCHAR(20) NOT NULL UNIQUE,
    display_name    VARCHAR(120) NULL,
    market          VARCHAR(30) NULL,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order      INT NOT NULL DEFAULT 0,
    created_at_utc  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 3.5 Table: `popular_tickers`

```sql
CREATE TABLE popular_tickers (
    id              BIGSERIAL PRIMARY KEY,
    ticker          VARCHAR(20) NOT NULL UNIQUE,
    display_name    VARCHAR(120) NULL,
    priority        INT NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 3.6 Table: `alert_rules`

```sql
CREATE TABLE alert_rules (
    id                BIGSERIAL PRIMARY KEY,
    rule_name         VARCHAR(120) NOT NULL,
    is_enabled        BOOLEAN NOT NULL DEFAULT TRUE,
    ticker            VARCHAR(20) NULL,
    keyword           VARCHAR(120) NULL,
    sentiment         VARCHAR(20) NULL,
    min_impact_score  NUMERIC(5,2) NULL,
    source_id         SMALLINT NULL REFERENCES news_sources(id),
    tag               VARCHAR(50) NULL,
    cooldown_seconds  INT NOT NULL DEFAULT 300,
    created_at_utc    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 3.7 Table: `alert_events`

```sql
CREATE TABLE alert_events (
    id                BIGSERIAL PRIMARY KEY,
    alert_rule_id     BIGINT NOT NULL REFERENCES alert_rules(id) ON DELETE CASCADE,
    news_id           BIGINT NOT NULL REFERENCES stock_news(id) ON DELETE CASCADE,
    triggered_at_utc  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    delivery_channel  VARCHAR(30) NOT NULL DEFAULT 'signalr',
    delivery_status   VARCHAR(30) NOT NULL DEFAULT 'sent',
    UNIQUE (alert_rule_id, news_id)
);
```

### 3.8 Table: `provider_sync_states`

```sql
CREATE TABLE provider_sync_states (
    id                    BIGSERIAL PRIMARY KEY,
    source_id             SMALLINT NOT NULL REFERENCES news_sources(id),
    sync_scope            VARCHAR(50) NOT NULL,
    scope_key             VARCHAR(120) NOT NULL,
    last_cursor           VARCHAR(255) NULL,
    last_published_at_utc TIMESTAMPTZ NULL,
    last_success_at_utc   TIMESTAMPTZ NULL,
    last_error_at_utc     TIMESTAMPTZ NULL,
    last_error_message    TEXT NULL,
    retry_count           INT NOT NULL DEFAULT 0,
    updated_at_utc        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (source_id, sync_scope, scope_key)
);
```

### 3.9 Table: `app_settings`

```sql
CREATE TABLE app_settings (
    setting_key     VARCHAR(100) PRIMARY KEY,
    setting_value   JSONB NOT NULL,
    updated_at_utc  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 3.10 Index Strategy

```sql
CREATE UNIQUE INDEX ux_stock_news_dedup_hash
    ON stock_news (dedup_hash);

CREATE UNIQUE INDEX ux_stock_news_source_provider_key
    ON stock_news (source_id, provider_news_key)
    WHERE provider_news_key IS NOT NULL;

CREATE INDEX ix_stock_news_published_at_desc
    ON stock_news (published_at_utc DESC);

CREATE INDEX ix_stock_news_source_published_at_desc
    ON stock_news (source_id, published_at_utc DESC);

CREATE INDEX ix_stock_news_impact_score_desc
    ON stock_news (impact_score DESC);

CREATE INDEX ix_stock_news_sentiment
    ON stock_news (sentiment);

CREATE INDEX ix_stock_news_tags_gin
    ON stock_news USING GIN (tags);

CREATE INDEX ix_stock_news_raw_payload_gin
    ON stock_news USING GIN (raw_payload);

CREATE INDEX ix_stock_news_tickers_ticker_news
    ON stock_news_tickers (ticker, news_id DESC);

CREATE INDEX ix_alert_rules_enabled
    ON alert_rules (is_enabled);

CREATE INDEX ix_provider_sync_states_lookup
    ON provider_sync_states (source_id, sync_scope, scope_key);
```

### 3.11 Query Notes

- feed หลัก query จาก `stock_news` join `stock_news_tickers`
- filter ตาม ticker ใช้ `stock_news_tickers`
- filter ตาม source/sentiment/impact ใช้ column ตรง
- filter ตาม tags ใช้ GIN บน JSONB
- `raw_payload` เก็บไว้สำหรับ debug และ reprocess

---

## 4. DTO / Model Design

### 4.1 Normalized News DTO

```csharp
public sealed class NormalizedNewsDto
{
    public string SourceCode { get; init; } = default!;
    public string? ProviderNewsKey { get; init; }
    public string ExternalUrl { get; init; } = default!;
    public string? CanonicalUrl { get; init; }
    public string Title { get; init; } = default!;
    public string? Summary { get; init; }
    public string? Content { get; init; }
    public DateTimeOffset PublishedAtUtc { get; init; }
    public IReadOnlyList<string> Tickers { get; init; } = Array.Empty<string>();
    public string Sentiment { get; init; } = "neutral";
    public decimal SentimentScore { get; init; }
    public decimal ImpactScore { get; init; }
    public string ImpactLevel { get; init; } = "low";
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string DedupHash { get; init; } = default!;
    public JsonDocument RawPayload { get; init; } = default!;
}
```

### 4.2 News Response DTO

```csharp
public sealed class NewsResponseDto
{
    public long Id { get; init; }
    public string Title { get; init; } = default!;
    public string? Summary { get; init; }
    public string Source { get; init; } = default!;
    public string SourceCode { get; init; } = default!;
    public string Url { get; init; } = default!;
    public DateTimeOffset PublishedAtUtc { get; init; }
    public IReadOnlyList<string> Tickers { get; init; } = Array.Empty<string>();
    public string Sentiment { get; init; } = "neutral";
    public decimal SentimentScore { get; init; }
    public decimal ImpactScore { get; init; }
    public string ImpactLevel { get; init; } = "low";
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public bool IsBreaking { get; init; }
}
```

### 4.3 Query DTOs

```csharp
public sealed class NewsQueryRequest
{
    public string? Ticker { get; init; }
    public string? SourceCode { get; init; }
    public string? Sentiment { get; init; }
    public string? Tag { get; init; }
    public decimal? MinImpactScore { get; init; }
    public DateTimeOffset? PublishedFromUtc { get; init; }
    public DateTimeOffset? PublishedToUtc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed class PagedResponseDto<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public bool HasMore { get; init; }
}
```

### 4.4 Watchlist DTOs

```csharp
public sealed class WatchlistItemDto
{
    public long Id { get; init; }
    public string Ticker { get; init; } = default!;
    public string? DisplayName { get; init; }
    public string? Market { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class CreateWatchlistRequest
{
    public string Ticker { get; init; } = default!;
    public string? DisplayName { get; init; }
    public string? Market { get; init; }
}
```

### 4.5 Alert Rule DTOs

```csharp
public sealed class AlertRuleDto
{
    public long Id { get; init; }
    public string RuleName { get; init; } = default!;
    public bool IsEnabled { get; init; }
    public string? Ticker { get; init; }
    public string? Keyword { get; init; }
    public string? Sentiment { get; init; }
    public decimal? MinImpactScore { get; init; }
    public string? SourceCode { get; init; }
    public string? Tag { get; init; }
    public int CooldownSeconds { get; init; }
}

public sealed class CreateAlertRuleRequest
{
    public string RuleName { get; init; } = default!;
    public string? Ticker { get; init; }
    public string? Keyword { get; init; }
    public string? Sentiment { get; init; }
    public decimal? MinImpactScore { get; init; }
    public string? SourceCode { get; init; }
    public string? Tag { get; init; }
    public int CooldownSeconds { get; init; } = 300;
}
```

### 4.6 SignalR Contracts

```csharp
public sealed class NewsCreatedEvent
{
    public string EventName { get; init; } = "news:new";
    public DateTimeOffset SentAtUtc { get; init; }
    public NewsResponseDto News { get; init; } = default!;
}

public sealed class NewsUpdatedEvent
{
    public string EventName { get; init; } = "news:updated";
    public DateTimeOffset SentAtUtc { get; init; }
    public NewsResponseDto News { get; init; } = default!;
}

public sealed class AlertTriggeredEvent
{
    public string EventName { get; init; } = "alert:triggered";
    public DateTimeOffset SentAtUtc { get; init; }
    public long AlertRuleId { get; init; }
    public string RuleName { get; init; } = default!;
    public string Message { get; init; } = default!;
    public NewsResponseDto News { get; init; } = default!;
}

public sealed class WatchlistUpdatedEvent
{
    public string EventName { get; init; } = "watchlist:updated";
    public DateTimeOffset SentAtUtc { get; init; }
    public IReadOnlyList<string> Tickers { get; init; } = Array.Empty<string>();
}
```

### 4.7 Validation Rules

- ticker ต้อง uppercase และ trim
- `PageSize` จำกัดไม่เกิน 200
- sentiment รับเฉพาะ `positive`, `negative`, `neutral`
- impact score อยู่ในช่วง `0-100`
- tag ใช้ lowercase kebab-case เช่น `sec-filing`

---

## 5. Backend API Design

### 5.1 News APIs

#### `GET /api/news`

Query params:

- `ticker`
- `sourceCode`
- `sentiment`
- `tag`
- `minImpactScore`
- `publishedFromUtc`
- `publishedToUtc`
- `page`
- `pageSize`

Example:

```http
GET /api/news?ticker=NVDA&sentiment=positive&minImpactScore=60&page=1&pageSize=20
```

Response:

```json
{
  "items": [
    {
      "id": 101,
      "title": "NVIDIA expands AI chip supply agreement",
      "summary": "The company signed a multi-year supply deal...",
      "source": "Finnhub",
      "sourceCode": "finnhub",
      "url": "https://example.com/news/101",
      "publishedAtUtc": "2026-07-05T08:30:00Z",
      "tickers": ["NVDA"],
      "sentiment": "positive",
      "sentimentScore": 72.5,
      "impactScore": 84.0,
      "impactLevel": "high",
      "tags": ["ai", "partnership"],
      "isBreaking": true
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 245,
  "hasMore": true
}
```

#### `GET /api/news/latest`

ใช้ดึงข่าวล่าสุดช่วง initial load

```http
GET /api/news/latest?limit=30
```

#### `GET /api/news/by-ticker/{ticker}`

```http
GET /api/news/by-ticker/TSLA?page=1&pageSize=25
```

#### `GET /api/news/{id}`

ใช้ดึงรายละเอียดข่าวเดี่ยว

### 5.2 Watchlist APIs

#### `GET /api/watchlist`

```json
[
  {
    "id": 1,
    "ticker": "AAPL",
    "displayName": "Apple Inc.",
    "market": "NASDAQ",
    "sortOrder": 1,
    "isActive": true
  }
]
```

#### `POST /api/watchlist`

```json
{
  "ticker": "MSFT",
  "displayName": "Microsoft",
  "market": "NASDAQ"
}
```

#### `DELETE /api/watchlist/{ticker}`

```http
DELETE /api/watchlist/MSFT
```

#### `PUT /api/watchlist/reorder`

```json
{
  "tickers": ["NVDA", "AAPL", "TSLA", "MSFT"]
}
```

### 5.3 Alert APIs

#### `GET /api/alerts`
#### `POST /api/alerts`
#### `PUT /api/alerts/{id}`
#### `PATCH /api/alerts/{id}/toggle`
#### `DELETE /api/alerts/{id}`
#### `GET /api/alerts/events`

ตัวอย่าง create request:

```json
{
  "ruleName": "SEC filing on TSLA",
  "ticker": "TSLA",
  "keyword": null,
  "sentiment": null,
  "minImpactScore": 50,
  "sourceCode": "sec_edgar",
  "tag": "sec-filing",
  "cooldownSeconds": 600
}
```

### 5.4 Support APIs

#### `GET /api/sources`
#### `GET /api/tags`
#### `GET /api/system/status`

### 5.5 Error Response Contract

```json
{
  "code": "validation_error",
  "message": "Ticker is required.",
  "details": {
    "ticker": ["Ticker is required."]
  },
  "traceId": "00-abc123xyz"
}
```

### 5.6 Status Code Policy

- `200` success
- `201` created
- `204` deleted
- `400` validation error
- `404` not found
- `409` duplicate
- `429` rate limited
- `500` server error

---

## 6. SignalR Design

### 6.1 Hub Name

- `NewsHub`
- route: `/hubs/news`

### 6.2 Groups

- `feed:global`
- `ticker:{SYMBOL}`
- `alerts:all`

### 6.3 Event Names

- `news:new`
- `news:updated`
- `alert:triggered`
- `watchlist:updated`

### 6.4 .NET Hub Example

```csharp
using Microsoft.AspNetCore.SignalR;

namespace StockPulse.Api.Hubs;

public sealed class NewsHub : Hub
{
    public Task SubscribeGlobalFeed()
        => Groups.AddToGroupAsync(Context.ConnectionId, "feed:global");

    public Task UnsubscribeGlobalFeed()
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, "feed:global");

    public Task SubscribeTicker(string ticker)
    {
        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        return Groups.AddToGroupAsync(Context.ConnectionId, $"ticker:{normalizedTicker}");
    }

    public Task UnsubscribeTicker(string ticker)
    {
        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticker:{normalizedTicker}");
    }

    public Task SubscribeAlerts()
        => Groups.AddToGroupAsync(Context.ConnectionId, "alerts:all");

    public Task UnsubscribeAlerts()
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, "alerts:all");
}
```

### 6.5 Realtime Notifier Example

```csharp
public interface IRealtimeNotifier
{
    Task BroadcastNewsCreatedAsync(NewsResponseDto news, CancellationToken cancellationToken);
    Task BroadcastAlertTriggeredAsync(AlertTriggeredEvent alertEvent, CancellationToken cancellationToken);
}
```

Implementation:

- broadcast `news:new` ไปที่ `feed:global`
- broadcast ซ้ำไปยังทุก `ticker:{symbol}` ที่ข่าวเกี่ยวข้อง
- broadcast `alert:triggered` ไปที่ `alerts:all`

### 6.6 Angular SignalR Service Example

```ts
import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { Subject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private hubConnection?: HubConnection;
  private readonly newsCreatedSubject = new Subject<any>();
  private readonly alertTriggeredSubject = new Subject<any>();

  readonly newsCreated$ = this.newsCreatedSubject.asObservable();
  readonly alertTriggered$ = this.alertTriggeredSubject.asObservable();

  async connect(): Promise<void> {
    if (this.hubConnection?.state === HubConnectionState.Connected) {
      return;
    }

    this.hubConnection = new HubConnectionBuilder()
      .withUrl('http://localhost:5000/hubs/news')
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('news:new', (payload) => this.newsCreatedSubject.next(payload));
    this.hubConnection.on('alert:triggered', (payload) => this.alertTriggeredSubject.next(payload));

    await this.hubConnection.start();
    await this.hubConnection.invoke('SubscribeGlobalFeed');
    await this.hubConnection.invoke('SubscribeAlerts');
  }

  async subscribeTicker(ticker: string): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.invoke('SubscribeTicker', ticker);
    }
  }
}
```

### 6.7 Reconnect Policy

- ใช้ `withAutomaticReconnect()`
- แสดงสถานะ connection บน top bar
- reconnect แล้ว join groups ซ้ำ
- ถ้าหลุดนาน ให้ fallback ไป `GET /api/news/latest`

---

## 7. Background Worker Design

### 7.1 Worker Responsibilities

- schedule polling ตาม source
- ดึง `watchlist + popular tickers`
- เรียก provider clients
- normalize response เป็น DTO กลาง
- deduplicate, enrich, save
- update provider sync state
- evaluate alert rules
- ส่ง event สำหรับ realtime

### 7.2 Worker Modules

```text
StockPulse.Worker/
  HostedServices/
    NewsIngestionHostedService.cs
  Schedulers/
    ProviderScheduler.cs
  Jobs/
    AlphaVantageNewsJob.cs
    FinnhubNewsJob.cs
    SecEdgarNewsJob.cs
  Pipelines/
    NewsIngestionPipeline.cs
  Providers/
    IProviderNewsClient.cs
    IMockProviderNewsClient.cs
```

### 7.3 Polling Strategy per Provider

#### Alpha Vantage

- polling frequency เริ่มที่ `300` วินาที
- ใช้ endpoint news/sentiment ถ้ามี field ที่เป็นข่าวโดยตรง
- scope ตาม `watchlist + popular tickers`
- ถ้า rate limit ชน ให้ลด concurrency และถอยรอบถัดไป

#### Finnhub

- polling frequency เริ่มที่ `60-180` วินาที ตาม quota
- ใช้ endpoint company news หรือ market news ตาม ticker scope
- ข่าวที่ไม่ผูก ticker ต้องผ่าน ticker mapping อีกชั้น

#### SEC EDGAR

- polling frequency เริ่มที่ `300-600` วินาที
- เน้น filing/press release/specific company feed
- map ticker จาก CIK-to-ticker reference table

### 7.4 Provider Client Interface

```csharp
public interface IProviderNewsClient
{
    string SourceCode { get; }
    Task<IReadOnlyList<NormalizedNewsDto>> FetchNewsAsync(
        ProviderFetchContext context,
        CancellationToken cancellationToken);
}
```

### 7.5 Retry and Rate Limit Handling

- ใช้ exponential backoff สำหรับ transient error
- จำกัด retry เช่น `3` ครั้งต่อรอบ
- ถ้า response เป็น `429`
  - log warning
  - update `provider_sync_states.last_error_at_utc`
  - respect `Retry-After` ถ้ามี
  - skip provider ชั่วคราวในรอบนั้น
- อย่าให้ provider หนึ่งล้มแล้วทั้ง worker หยุด

### 7.6 Error Logging

ต้อง log:

- source code
- ticker scope
- request start/end time
- response status
- inserted count
- duplicate count
- error message
- trace id / correlation id

### 7.7 Mock Provider Mode

ใช้เมื่อ:

- API key ยังไม่มี
- free limit หมด
- dev ต้องการทดสอบ feed/UI

การทำงาน:

- implement `MockAlphaVantageNewsClient`, `MockFinnhubNewsClient`, `MockSecEdgarNewsClient`
- อ่าน sample JSON จาก `backend/mock-data/`
- random published time เล็กน้อยเพื่อจำลองข่าวใหม่
- config ผ่าน `UseMockProviders = true`

### 7.8 Worker Runtime Config

```json
{
  "Worker": {
    "DefaultPollingSeconds": 300,
    "MaxRetryCount": 3,
    "UseMockProviders": false,
    "PopularTickers": ["AAPL", "NVDA", "TSLA", "MSFT", "AMD", "META"]
  }
}
```

---

## 8. News Processing Design

### 8.1 Normalize Strategy

ทุก provider ต้อง map เป็น `NormalizedNewsDto` โดยมี field ขั้นต่ำ:

- sourceCode
- providerNewsKey
- externalUrl
- canonicalUrl
- title
- summary
- publishedAtUtc
- rawPayload

### 8.2 Dedup Strategy

ใช้ `dedup_hash` จาก:

```text
normalized(title) + "|" +
normalized(canonicalUrl or externalUrl) + "|" +
publishedAtUtc(round to minute) + "|" +
source family
```

แนวทาง normalize:

- trim whitespace
- lower-case title/url
- remove tracking query string ที่ไม่จำเป็น
- convert published time เป็น UTC

ป้องกัน duplicate สองชั้น:

- unique index `(source_id, provider_news_key)` ถ้ามี provider key
- unique index `dedup_hash` สำหรับ cross-provider duplicate

### 8.3 Impact Score Rule-based

ให้คะแนนช่วง `0-100` จาก weighted factors:

- recency: `0-25`
- source weight: `0-20`
- ticker match confidence: `0-10`
- keyword significance: `0-30`
- tag severity: `0-15`

ตัวอย่าง source weight:

- SEC EDGAR: `20`
- Finnhub: `16`
- Alpha Vantage: `14`

ตัวอย่าง keyword weight:

- `acquisition`, `merger`, `lawsuit`, `investigation`, `guidance cut`, `bankruptcy`: `25-30`
- `earnings beat`, `upgrade`, `approval`, `partnership`: `18-24`
- `product launch`, `analyst note`: `10-17`

Impact level:

- `0-29` = low
- `30-59` = medium
- `60-79` = high
- `80-100` = critical

### 8.4 Sentiment Rule-based

เริ่มด้วย dictionary scoring:

- positive keywords:
  - `beat`, `approval`, `upgrade`, `partnership`, `growth`, `record revenue`
- negative keywords:
  - `miss`, `downgrade`, `lawsuit`, `probe`, `offering`, `delay`, `recall`

ผลลัพธ์:

- score > `20` = positive
- score < `-20` = negative
- อื่น ๆ = neutral

### 8.5 Tagging Strategy

tag ที่ต้องรองรับใน MVP:

- `earnings`
- `m-and-a`
- `lawsuit`
- `analyst-upgrade`
- `analyst-downgrade`
- `dividend`
- `sec-filing`
- `fda`
- `offering`
- `guidance`
- `partnership`
- `ai`
- `regulation`

ตัวอย่าง mapping:

- มี `10-Q`, `8-K`, `S-1`, `13D`, `13F` -> `sec-filing`
- มี `acquire`, `acquisition`, `merger` -> `m-and-a`
- มี `raises dividend` -> `dividend`
- มี `downgraded by` -> `analyst-downgrade`

### 8.6 Ticker Mapping Strategy

ลำดับความเชื่อมั่น:

1. ticker ที่ provider ส่งมาโดยตรง
2. SEC filing map จาก CIK หรือ issuer reference
3. exact symbol/name match จาก `watchlist + popular tickers`
4. keyword based fallback

ลด false positive ด้วยกฎ:

- symbol ต้อง match ทั้งคำ
- name match ต้อง normalize company suffix เช่น `Inc`, `Corp`, `Ltd`
- หลีกเลี่ยง ticker ที่ซ้อนกับคำธรรมดา เช่น `IT`, `ON`

### 8.7 Alert Evaluation

ข่าวใหม่ทุกชิ้นต้องประเมินกับ `alert_rules` ที่ active:

- ticker match ถ้า rule ระบุ
- keyword match ถ้า rule ระบุ
- sentiment match ถ้า rule ระบุ
- source match ถ้า rule ระบุ
- tag match ถ้า rule ระบุ
- impact score >= threshold ถ้า rule ระบุ

cooldown:

- ถ้ามี `alert_events` ซ้ำ news เดิมแล้ว ห้ามส่งซ้ำ
- ถ้า rule เดียวกันเพิ่งยิงข่าวคล้ายกันภายใน cooldown ให้ skip

---

## 9. Angular UI/UX Design

### 9.1 Layout

ใช้ 3-column dashboard สำหรับ desktop:

- Left Sidebar
  - watchlist
  - quick filters
  - alert shortcut
- Center Feed
  - real-time news feed
  - virtual scroll
  - news cards
- Right Panel
  - selected stock detail
  - related news
  - sentiment/impact summary

Top Bar:

- search
- live connection status
- market status
- source filter
- sentiment filter
- tag filter

### 9.2 Visual Direction

- dark mode เป็นค่าเริ่มต้น
- พื้นหลังใช้ charcoal / navy-black
- ใช้ accent สีเขียวสำหรับ positive, แดงสำหรับ negative, amber สำหรับ warning/high impact
- typography เน้นอ่านเร็วระหว่างเทรด
- card spacing กระชับ ไม่โปร่งเกินไป

### 9.3 News Card Content

แต่ละ card ต้องมี:

- title
- summary
- source badge
- published time
- ticker chips
- sentiment badge
- impact score meter
- tags

### 9.4 Interaction

- ข่าวใหม่ขึ้นบนสุด
- highlight animation 1-2 วินาทีเมื่อมีข่าวเข้าใหม่
- click card แล้วเปิด right panel detail
- filter เปลี่ยนแล้วไม่ reload ทั้งหน้า

### 9.5 States

- loading state
  - skeleton card 6-10 รายการ
- empty state
  - ข้อความเชิญเพิ่ม watchlist หรือเปิด source
- reconnect state
  - แสดง badge `Reconnecting...`

### 9.6 Mobile / Narrow Layout

- left/right panel collapse เป็น drawer
- center feed เป็น column หลัก
- top bar filter เปิดเป็น bottom sheet

---

## 10. Angular Starter Code

### 10.1 Interfaces

```ts
export interface NewsItem {
  id: number;
  title: string;
  summary?: string;
  source: string;
  sourceCode: string;
  url: string;
  publishedAtUtc: string;
  tickers: string[];
  sentiment: 'positive' | 'negative' | 'neutral';
  sentimentScore: number;
  impactScore: number;
  impactLevel: 'low' | 'medium' | 'high' | 'critical';
  tags: string[];
  isBreaking: boolean;
}
```

### 10.2 API Service Example

```ts
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class NewsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = 'http://localhost:5000/api/news';

  getLatest(limit = 30): Observable<NewsItem[]> {
    const params = new HttpParams().set('limit', limit);
    return this.http.get<NewsItem[]>(`${this.baseUrl}/latest`, { params });
  }

  getNews(query: {
    ticker?: string;
    sourceCode?: string;
    sentiment?: string;
    tag?: string;
    minImpactScore?: number;
    page?: number;
    pageSize?: number;
  }): Observable<any> {
    let params = new HttpParams();

    Object.entries(query).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, value);
      }
    });

    return this.http.get<any>(this.baseUrl, { params });
  }
}
```

### 10.3 Watchlist Service Example

```ts
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class WatchlistApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = 'http://localhost:5000/api/watchlist';

  getAll() {
    return this.http.get<any[]>(this.baseUrl);
  }

  add(request: { ticker: string; displayName?: string; market?: string }) {
    return this.http.post<any>(this.baseUrl, request);
  }

  remove(ticker: string) {
    return this.http.delete(`${this.baseUrl}/${ticker}`);
  }
}
```

### 10.4 Dashboard Shell Example

```ts
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'sp-dashboard-shell',
  templateUrl: './dashboard-shell.component.html',
  styleUrl: './dashboard-shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardShellComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly newsApi = inject(NewsApiService);
  private readonly signalR = inject(SignalRService);

  readonly items = signal<NewsItem[]>([]);

  async ngOnInit(): Promise<void> {
    this.newsApi.getLatest(30)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((items) => this.items.set(items));

    await this.signalR.connect();

    this.signalR.newsCreated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ news }) => {
        this.items.update((items) => [news, ...items].slice(0, 300));
      });
  }

  trackByNewsId(_: number, item: NewsItem): number {
    return item.id;
  }
}
```

### 10.5 Component List

- `dashboard-shell`
- `top-bar`
- `news-feed`
- `news-card`
- `watchlist-panel`
- `stock-detail-panel`
- `alert-rule-panel`

### 10.6 Minimal News Card Template

```html
<article class="news-card" [class.news-card--breaking]="item.isBreaking">
  <header class="news-card__header">
    <span class="source">{{ item.source }}</span>
    <span class="time">{{ item.publishedAtUtc | date:'HH:mm:ss' }}</span>
  </header>

  <h3 class="news-card__title">{{ item.title }}</h3>
  <p class="news-card__summary">{{ item.summary }}</p>

  <div class="news-card__meta">
    <span class="chip" *ngFor="let ticker of item.tickers">{{ ticker }}</span>
    <span class="chip chip--sentiment">{{ item.sentiment }}</span>
    <span class="chip chip--impact">{{ item.impactScore }}</span>
  </div>
</article>
```

---

## 11. Performance Design

### 11.1 Frontend Performance

- ใช้ Angular `OnPush`
- ใช้ `trackBy` ใน news list
- ใช้ `cdk-virtual-scroll-viewport`
- จำกัดข่าวใน memory ไม่เกิน `200-300` รายการ
- debounce filter/search `200-300ms`
- batch UI updates เมื่อข่าวเข้าถี่มาก
- แยก component ให้ update เฉพาะส่วนที่เปลี่ยน

### 11.2 Backend Performance

- query เฉพาะ fields ที่ใช้แสดงผล
- ใช้ pagination เสมอ
- repository ต้อง `AsNoTracking()` สำหรับ query read-heavy
- cache metadata ที่ไม่เปลี่ยนบ่อย
- แยก insert batch ใน worker

### 11.3 Database Performance

- มี composite indexes ตาม query pattern
- ticker mapping แยกตารางเพื่อลด repeated JSON scan
- index `published_at_utc`, `source_id`, `impact_score`
- หากข่าวโตมากในอนาคต ค่อย partition ตามเดือน

### 11.4 Worker Throughput

- provider jobs แยกกัน
- จำกัด concurrency ต่อ provider
- save แบบ batch ตามรอบ ingestion
- dedup ก่อน insert ให้มากที่สุด

---

## 12. Security Design

### 12.1 API Key Security

- API key อยู่ใน backend config เท่านั้น
- Angular ไม่เห็น provider key
- ใช้ `appsettings.Development.json` หรือ user secrets ใน local

### 12.2 Network and CORS

- เปิด CORS เฉพาะ origin ของ Angular local dev เช่น `http://localhost:4200`
- ห้ามเปิด `AllowAnyOrigin` ในขั้น production

### 12.3 Input Validation

- validate ticker, page, pageSize, score ranges
- sanitize search/filter input
- normalize string ก่อน query

### 12.4 Rate Limiting

- rate limit endpoint ที่เรียกบ่อย เช่น `/api/news/latest`
- ป้องกัน flood จาก client ซ้ำ ๆ
- worker เองต้อง respect provider quota เสมอ

### 12.5 Logging and Error Handling

- log ด้วย correlation id
- ไม่ log secrets
- provider error ต้องสรุปสั้นและไม่ dump payload เต็มใน info log
- API error response ต้องไม่เปิดเผย internal stack trace

### 12.6 Server-side Authorization Direction

แม้ MVP ยังไม่มี auth แต่ design ต้องพร้อม:

- อย่าฝัง assumption แบบ anonymous-only ใน service layer
- แยก watchlist/alerts service ให้พร้อมรับ `userId` เพิ่มในอนาคต

---

## 13. MVP Scope and Phase Plan

### Phase 0: Local Prototype

เป้าหมาย:

- Angular dashboard ขึ้นได้
- API/Worker/DB เชื่อมต่อกัน
- mock provider feed ใช้งานได้
- watchlist global ใช้งานได้
- SignalR push ข่าวใหม่จาก mock data ได้

Deliverables:

- solution structure
- DB migrations ชุดแรก
- mock provider mode
- dashboard UI dark mode รุ่นแรก

### Phase 1: MVP ใช้เอง

เป้าหมาย:

- เปิดใช้ Alpha Vantage, Finnhub, SEC EDGAR จริง
- normalize + deduplicate + save DB
- real-time dashboard ใช้งานได้จริง
- filter ตาม ticker/source/sentiment/tag
- alert rules พื้นฐานใช้งานได้

Deliverables:

- provider clients จริง
- worker scheduler/retry/rate limit handling
- alert evaluation
- system status endpoint

### Phase 2: Alert + Sentiment + Better Ranking

เป้าหมาย:

- ปรับ scoring ให้แม่นขึ้น
- เพิ่ม ranking/feed quality
- เพิ่ม alert logic ซับซ้อนขึ้น
- เพิ่ม keyword dictionary และ tagging coverage

Deliverables:

- refined sentiment/impact engine
- alert cooldown/aggregation ดีขึ้น
- better stock detail panel

### Phase 3: Production-ready

เป้าหมาย:

- multi-user + auth
- deployment automation
- observability และ resilience สูงขึ้น
- scale-out realtime

Deliverables:

- auth and user isolation
- Redis backplane for SignalR
- containerized deployment
- health checks, metrics, tracing
- background retry storage / queue option

---

## 14. Recommended Build Order

1. สร้าง solution + Angular app + project skeleton
2. วาง PostgreSQL schema และ migration แรก
3. ทำ watchlist API และ UI พื้นฐาน
4. ทำ mock worker -> save -> SignalR -> dashboard flow
5. ทำ news query APIs และ filters
6. ใส่ provider clients จริงทีละเจ้า
7. ใส่ dedup, scoring, tags, alerts
8. ปรับ performance และ hardening

---

## 15. Risks and Mitigations

### Risk: Provider quota ต่ำ

- Likelihood: High
- Mitigation:
  - ใช้ `watchlist + popular tickers`
  - มี mock mode
  - มี per-provider polling config

### Risk: False positive ใน ticker mapping

- Likelihood: Medium
- Mitigation:
  - ใช้ provider ticker ก่อน
  - ใช้ exact token match
  - เก็บ `confidence_score`

### Risk: ข่าวเข้าเร็วทำให้ UI กระตุก

- Likelihood: Medium
- Mitigation:
  - virtual scroll
  - update batching
  - memory cap
  - OnPush

### Risk: duplicate ข่าวจากหลาย provider

- Likelihood: High
- Mitigation:
  - provider key unique
  - dedup hash
  - canonical URL normalization

---

## 16. Implementation Readiness Checklist

- [x] Architecture baseline ชัดเจน
- [x] Project structure พร้อมเริ่ม scaffold
- [x] Database schema พร้อมทำ migration
- [x] DTO/API/SignalR contracts ชัดเจน
- [x] Worker ingestion flow ชัดเจน
- [x] UI layout และ component set ชัดเจน
- [x] MVP phase plan ชัดเจน
- [ ] เริ่มสร้าง implementation plan ราย task

---

## 17. Notes

- เอกสารนี้ตั้งใจให้เป็น design baseline สำหรับเริ่มเขียน implementation plan และ scaffold โปรเจ็กต์ทันที
- เนื่องจาก workspace ปัจจุบันยังไม่ใช่ git repository จึงยังไม่สามารถ commit spec นี้ได้ในตอนนี้
