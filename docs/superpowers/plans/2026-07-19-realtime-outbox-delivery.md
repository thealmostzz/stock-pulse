# Realtime Outbox Delivery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ทำให้ข่าวที่บันทึกสำเร็จมีงาน realtime ที่ retry ได้และไม่ broadcast ซ้ำเมื่อ request ถูกส่งซ้ำ

**Architecture:** Worker บันทึก `StockNews` และ outbox event ใน transaction เดียวกัน. Dispatcher ส่ง event ไป internal API แบบ at-least-once; API เก็บ receipt ของ `eventId` ก่อน publish SignalR เพื่อ idempotency.

**Tech Stack:** .NET 10, EF Core 10, Npgsql 10, PostgreSQL 16, xUnit.

## Global Constraints

- ใช้ UTC `DateTimeOffset` และ PostgreSQL `TIMESTAMPTZ`; payload ใช้ `JSONB`.
- ห้าม commit secret; internal key ต้องมาจาก User Secrets หรือ environment variable และห้ามมี usable default.
- ทุก integration test ต้อง guard database เป็น `stockpulse_test` ก่อน DDL หรือ delete.
- duplicate news/source/outbox ต้องมี database unique constraint เป็น final guard.
- internal API และ frontend ต้อง idempotent ด้วย `eventId`/`newsId`; delivery เป็น at-least-once.

---

### Task 1: Outbox persistence และ database-safe worker tests

**Files:**
- Create: `backend/src/StockPulse.Domain/Entities/NewsOutboxEvent.cs`
- Create: `backend/src/StockPulse.Infrastructure/Persistence/Configurations/NewsOutboxEventConfiguration.cs`
- Modify: `backend/src/StockPulse.Infrastructure/Persistence/StockPulseDbContext.cs`
- Modify: `backend/src/StockPulse.Infrastructure/Persistence/Configurations/StockNewsConfiguration.cs`
- Modify: `backend/src/StockPulse.Infrastructure/Persistence/Migrations/*`
- Modify: `backend/tests/StockPulse.Worker.Tests/NewsIngestionPipelineTests.cs`

**Interfaces:**
- Produces `NewsOutboxEvent` with `Guid EventId`, `long NewsId`, `JsonDocument Payload`, `int AttemptCount`, `DateTimeOffset NextAttemptAtUtc`, nullable delivery/error fields.

- [ ] **Step 1: Write failing model tests**

```csharp
[Fact]
public void Model_HasUniqueOutboxNewsIdAndUniqueSourceCode()
{
    using var db = CreateDbContext();
    Assert.True(db.Model.FindEntityType(typeof(NewsOutboxEvent))!
        .GetIndexes().Single(index => index.Properties.Single().Name == nameof(NewsOutboxEvent.NewsId)).IsUnique);
}
```

- [ ] **Step 2: Run RED test**

Run: `dotnet test backend\tests\StockPulse.Worker.Tests --filter FullyQualifiedName~NewsIngestionPipelineTests`

Expected: fail because `NewsOutboxEvent` does not exist.

- [ ] **Step 3: Add entity, mapping, DbSet and migration**

```csharp
public sealed class NewsOutboxEvent
{
    public Guid EventId { get; set; }
    public long NewsId { get; set; }
    public JsonDocument Payload { get; set; } = JsonDocument.Parse("{}");
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

Map `news_outbox_events`, `event_id` primary key, unique `news_id`, JSONB payload, and index `(delivered_at_utc, next_attempt_at_utc)`. Add a unique index on `NewsSource.SourceCode`.

- [ ] **Step 4: Make worker tests database-safe**

Use `STOCKPULSE_TEST_CONNECTION`; parse it with `NpgsqlConnectionStringBuilder` and throw before setup unless `Database == "stockpulse_test"`. Do not create schemas or DDL in `stockpulse`.

- [ ] **Step 5: Generate migration and verify GREEN**

Run:

```powershell
$env:STOCKPULSE_CONNECTION='Host=localhost;Port=5432;Database=stockpulse;Username=stockpulse;Password=stockpulse_local_only'
dotnet ef migrations add AddRealtimeOutbox --project backend\src\StockPulse.Infrastructure --startup-project backend\src\StockPulse.Infrastructure --output-dir Persistence\Migrations
$env:STOCKPULSE_TEST_CONNECTION='Host=localhost;Port=5432;Database=stockpulse_test;Username=stockpulse;Password=stockpulse_local_only'
dotnet test backend\tests\StockPulse.Worker.Tests --filter FullyQualifiedName~NewsIngestionPipelineTests
```

- [ ] **Step 6: Commit**

Run: `git add backend && git commit -m "feat: add realtime outbox persistence"`

### Task 2: Transactional dispatch, retry และ API idempotency

**Files:**
- Modify: `backend/src/StockPulse.Worker/Pipelines/NewsIngestionPipeline.cs`
- Create: `backend/src/StockPulse.Worker/Services/OutboxDispatcher.cs`
- Modify: `backend/src/StockPulse.Worker/HostedServices/NewsIngestionHostedService.cs`
- Modify: `backend/src/StockPulse.Worker/Services/ApiRealtimeNotifier.cs`
- Modify: `backend/src/StockPulse.Api/Controllers/InternalRealtimeController.cs`
- Create: `backend/src/StockPulse.Domain/Entities/RealtimeDeliveryReceipt.cs`
- Create: `backend/src/StockPulse.Infrastructure/Persistence/Configurations/RealtimeDeliveryReceiptConfiguration.cs`
- Test: `backend/tests/StockPulse.Worker.Tests/NewsIngestionPipelineTests.cs`

**Interfaces:**
- `ApiRealtimeNotifier.NotifyAsync(Guid eventId, NewsCreatedEvent message, CancellationToken cancellationToken)` sends `X-StockPulse-Event-Id`.
- `OutboxDispatcher.DispatchPendingAsync(CancellationToken cancellationToken)` sends due events and records outcome.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact]
public async Task IngestAsync_CreatesOutboxEvent_WhenNewsIsInserted();
[Fact]
public async Task DispatchPendingAsync_RetriesFailedEvent_AndMarksItDelivered();
[Fact]
public async Task InternalEndpoint_DoesNotPublishTwice_ForSameEventId();
```

- [ ] **Step 2: Run RED tests**

Run: `dotnet test backend\tests\StockPulse.Worker.Tests --filter FullyQualifiedName~Outbox`

Expected: fail because dispatcher and idempotency receipt do not exist.

- [ ] **Step 3: Implement transactional outbox and dispatcher**

In `IngestAsync`, add a `NewsOutboxEvent` for every inserted news before the single `SaveChangesAsync`. Dispatcher selects undelivered due events, sends each event, sets `DeliveredAtUtc` on success, or increments `AttemptCount`, stores a truncated error, and sets `NextAttemptAtUtc = UtcNow + TimeSpan.FromSeconds(Math.Min(300, 1 << Math.Min(AttemptCount, 8)))` on failure.

- [ ] **Step 4: Implement idempotent API receipt**

Create `realtime_delivery_receipts` with unique `event_id`. Internal controller inserts receipt and invokes `IRealtimePublisher` only when insert succeeds; a duplicate event returns `204 NoContent` without publishing. Require a configured internal key; startup must throw when key is empty or equals `change-me`.

- [ ] **Step 5: Run GREEN tests and end-to-end check**

Run:

```powershell
dotnet test backend\StockPulse.sln
dotnet build backend\StockPulse.sln --configuration Release
```

Expected: all tests pass, zero warnings/errors. Run API and Worker against Docker PostgreSQL; stop API temporarily, ingest fixture, start API, then confirm event is delivered once and outbox has `delivered_at_utc`.

- [ ] **Step 6: Commit**

Run: `git add backend && git commit -m "feat: retry realtime outbox delivery"`
