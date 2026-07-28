# Finnhub Watchlist News Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ingest current Finnhub company news for up to 20 active Watchlist tickers while retaining the mock provider for repeatable local development.

**Architecture:** The Worker will select active Watchlist tickers directly from its existing scoped `StockPulseDbContext`, request Finnhub Company News sequentially through a named `HttpClient`, and map valid response items into the existing provider-neutral `NormalizedNewsDto`. Provider registration and polling cadence will be configuration-driven: mock mode retains its 15-second interval; Finnhub mode uses a fixed 15-minute interval and requires a local-only API key.

**Tech Stack:** .NET 10 Worker Service, EF Core 10/PostgreSQL, `IHttpClientFactory`, xUnit, Angular unchanged.

## Global Constraints

- Target .NET 10; nullable warnings remain errors.
- Do not add a provider API key to source, JSON configuration, test fixtures, logs, Git, or the frontend.
- Query active Watchlist tickers only, in `SortOrder` then ticker order, capped at 20 per Finnhub polling cycle.
- Finnhub requests must be sequential and separated by at least one second; its polling cadence is 15 minutes.
- Preserve the current mock fixture behavior and existing persistence, de-duplication, outbox, and SignalR pipeline.
- Use conventional commits and run the four repository verification commands before handoff.

---

## File Structure

- `backend/src/StockPulse.Worker/Configuration/WorkerOptions.cs` — validates provider-mode settings and exposes fixed mock/Finnhub polling intervals.
- `backend/src/StockPulse.Worker/Configuration/FinnhubOptions.cs` — binds the local-only Finnhub key and endpoint settings.
- `backend/src/StockPulse.Worker/Providers/Finnhub/FinnhubNewsClient.cs` — retrieves up to 20 active Watchlist tickers, calls Finnhub, and maps valid JSON articles.
- `backend/src/StockPulse.Worker/Program.cs` — registers the selected provider and named Finnhub `HttpClient` without logging secrets.
- `backend/src/StockPulse.Worker/HostedServices/NewsIngestionHostedService.cs` — uses the mode-appropriate polling interval.
- `backend/tests/StockPulse.Worker.Tests/FinnhubNewsClientTests.cs` — verifies request shape, mapping, cap, empty Watchlist, and per-ticker error isolation.
- `backend/tests/StockPulse.Worker.Tests/WorkerConfigurationTests.cs` — verifies key validation, provider selection, and polling intervals.
- `docs/local-development.md` — documents local User Secrets setup and a real-news smoke test.

### Task 1: Provider Configuration and Scheduling

**Files:**
- Create: `backend/src/StockPulse.Worker/Configuration/WorkerOptions.cs`
- Create: `backend/src/StockPulse.Worker/Configuration/FinnhubOptions.cs`
- Modify: `backend/src/StockPulse.Worker/Program.cs`
- Modify: `backend/src/StockPulse.Worker/HostedServices/NewsIngestionHostedService.cs`
- Create: `backend/tests/StockPulse.Worker.Tests/WorkerConfigurationTests.cs`

**Interfaces:**
- Produces: `WorkerOptions.UseMockProviders`, `WorkerOptions.GetPollingInterval()`, and `FinnhubOptions.ApiKey` for Worker registration and the Finnhub client.
- Consumes: existing `IProviderNewsClient`, `MockNewsClient`, `NewsIngestionHostedService`, and Worker User Secrets ID `dotnet-StockPulse.Worker-852cc624-d2ad-49c0-8054-a768f8bc0cce`.

- [ ] **Step 1: Write the failing configuration tests**

```csharp
[Fact]
public void GetPollingInterval_FinnhubMode_ReturnsFifteenMinutes() =>
    Assert.Equal(TimeSpan.FromMinutes(15), new WorkerOptions { UseMockProviders = false }.GetPollingInterval());

[Fact]
public void GetPollingInterval_MockMode_ReturnsFifteenSeconds() =>
    Assert.Equal(TimeSpan.FromSeconds(15), new WorkerOptions { UseMockProviders = true }.GetPollingInterval());

[Fact]
public void Validate_FinnhubModeWithoutApiKey_Throws() =>
    Assert.Throws<InvalidOperationException>(() => WorkerOptions.Validate(new WorkerOptions { UseMockProviders = false }, new FinnhubOptions()));
```

- [ ] **Step 2: Run the focused tests to verify they fail**

Run: `dotnet test backend/tests/StockPulse.Worker.Tests --configuration Release --filter FullyQualifiedName~WorkerConfigurationTests`

Expected: FAIL because the options types and validation method do not exist.

- [ ] **Step 3: Add validated options and conditional provider registration**

```csharp
public sealed class WorkerOptions
{
    public bool UseMockProviders { get; init; } = true;

    public TimeSpan GetPollingInterval() =>
        UseMockProviders ? TimeSpan.FromSeconds(15) : TimeSpan.FromMinutes(15);

    public static void Validate(WorkerOptions worker, FinnhubOptions finnhub)
    {
        if (!worker.UseMockProviders && string.IsNullOrWhiteSpace(finnhub.ApiKey))
        {
            throw new InvalidOperationException("Finnhub:ApiKey must be configured when mock providers are disabled.");
        }
    }
}
```

Bind `Worker` and `Finnhub` in `Program.cs`, call validation before building the host, and retain `MockNewsClient` registration only when mock mode is enabled. In Finnhub mode register a named `HttpClient` named `Finnhub` with base address `https://finnhub.io/api/v1/` and register `FinnhubNewsClient` as scoped `IProviderNewsClient`. Inject `IOptions<WorkerOptions>` into `NewsIngestionHostedService` and construct its `PeriodicTimer` from `GetPollingInterval()` instead of the hard-coded 15 seconds. Do not add an API key to either appsettings file.

- [ ] **Step 4: Run the focused tests to verify they pass**

Run: `dotnet test backend/tests/StockPulse.Worker.Tests --configuration Release --filter FullyQualifiedName~WorkerConfigurationTests`

Expected: PASS.

- [ ] **Step 5: Commit the configuration task**

```powershell
git add backend/src/StockPulse.Worker/Configuration backend/src/StockPulse.Worker/Program.cs backend/src/StockPulse.Worker/HostedServices/NewsIngestionHostedService.cs backend/tests/StockPulse.Worker.Tests/WorkerConfigurationTests.cs
git commit -m "feat: configure Finnhub news provider"
```

### Task 2: Finnhub Client and Provider Contract Mapping

**Files:**
- Create: `backend/src/StockPulse.Worker/Providers/Finnhub/FinnhubNewsClient.cs`
- Create: `backend/tests/StockPulse.Worker.Tests/FinnhubNewsClientTests.cs`

**Interfaces:**
- Consumes: `IProviderNewsClient.FetchNewsAsync(CancellationToken)`, `StockPulseDbContext`, `IHttpClientFactory`, `FinnhubOptions`, and `WorkerOptions` from Task 1.
- Produces: an `IProviderNewsClient` implementation with `SourceCode == "finnhub"` that returns `NormalizedNewsDto` values.

- [ ] **Step 1: Write the failing Finnhub client tests**

```csharp
[Fact]
public async Task FetchNewsAsync_ActiveWatchlistTicker_MapsFinnhubArticleAndUsesCompanyNewsUrl()
{
    await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
    dbContext.WatchlistItems.AddRange(
        new WatchlistItem { Ticker = "AAPL", IsActive = true, SortOrder = 1 },
        new WatchlistItem { Ticker = "NVDA", IsActive = false, SortOrder = 2 });
    await dbContext.SaveChangesAsync();
    var handler = new StubHttpMessageHandler(_ => JsonResponse("[{\"id\":12345,\"url\":\"https://example.com/article\",\"headline\":\"Apple result\",\"summary\":\"Summary\",\"datetime\":1784505600}]"));
    var client = CreateClient(dbContext, handler);

    var articles = await client.FetchNewsAsync(CancellationToken.None);

    Assert.Single(articles);
    Assert.Equal("finnhub", articles[0].SourceCode);
    Assert.Equal("12345", articles[0].ProviderNewsKey);
    Assert.Equal(["AAPL"], articles[0].Tickers);
    Assert.Single(handler.RequestUris);
    Assert.Contains("symbol=AAPL", handler.RequestUris[0].Query, StringComparison.Ordinal);
}

[Fact]
public async Task FetchNewsAsync_MoreThanTwentyActiveTickers_RequestsOnlyFirstTwentyInWatchlistOrder()
{
    await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
    dbContext.WatchlistItems.AddRange(Enumerable.Range(1, 21)
        .Select(index => new WatchlistItem { Ticker = $"T{index:D2}", IsActive = true, SortOrder = index }));
    await dbContext.SaveChangesAsync();

    var handler = new StubHttpMessageHandler(_ => JsonResponse("[]"));
    var client = CreateClient(dbContext, handler);

    await client.FetchNewsAsync(CancellationToken.None);

    Assert.Equal(20, handler.RequestUris.Count);
    Assert.Contains("symbol=T01", handler.RequestUris[0].Query, StringComparison.Ordinal);
    Assert.Contains("symbol=T20", handler.RequestUris[^1].Query, StringComparison.Ordinal);
}

[Fact]
public async Task FetchNewsAsync_EmptyWatchlist_MakesNoHttpRequest()
{
    await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
    var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("No request is expected."));

    var articles = await CreateClient(dbContext, handler).FetchNewsAsync(CancellationToken.None);

    Assert.Empty(articles);
    Assert.Empty(handler.RequestUris);
}

[Fact]
public async Task FetchNewsAsync_OneTickerFails_ContinuesWithNextTicker()
{
    await using var dbContext = await CreateSchemaDbContextAsync(schemaName);
    dbContext.WatchlistItems.AddRange(
        new WatchlistItem { Ticker = "AAPL", IsActive = true, SortOrder = 1 },
        new WatchlistItem { Ticker = "MSFT", IsActive = true, SortOrder = 2 });
    await dbContext.SaveChangesAsync();

    var handler = new StubHttpMessageHandler(request => request.RequestUri!.Query.Contains("symbol=AAPL", StringComparison.Ordinal)
        ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        : JsonResponse("[{\"id\":12345,\"url\":\"https://example.com/article\",\"headline\":\"Microsoft result\",\"summary\":\"Summary\",\"datetime\":1784505600}]"));

    var articles = await CreateClient(dbContext, handler).FetchNewsAsync(CancellationToken.None);

    var article = Assert.Single(articles);
    Assert.Equal(["MSFT"], article.Tickers);
    Assert.Equal(2, handler.RequestUris.Count);
}
```

Use the existing PostgreSQL test-connection and per-test schema helpers from `NewsIngestionPipelineTests` for the required EF Core relationships. The stub `HttpMessageHandler` records requests and returns response bodies without performing network I/O.

- [ ] **Step 2: Run the focused tests to verify they fail**

Run: `dotnet test backend/tests/StockPulse.Worker.Tests --configuration Release --filter FullyQualifiedName~FinnhubNewsClientTests`

Expected: FAIL because `FinnhubNewsClient` does not exist.

- [ ] **Step 3: Implement the scoped Finnhub provider**

```csharp
public sealed partial class FinnhubNewsClient(
    StockPulseDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IOptions<FinnhubOptions> options,
    ILogger<FinnhubNewsClient> logger) : IProviderNewsClient
{
    public string SourceCode => "finnhub";

    public async Task<IReadOnlyList<NormalizedNewsDto>> FetchNewsAsync(CancellationToken cancellationToken)
    {
        var tickers = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Ticker)
            .Select(item => item.Ticker)
            .Take(20)
            .ToArrayAsync(cancellationToken);

        return await FetchTickersSequentiallyAsync(tickers, cancellationToken);
    }
}
```

For every ticker, calculate a UTC `from` date from that ticker's most recently persisted `StockNewsTicker.News.PublishedAtUtc`; if none exists, use seven days before the current UTC date. Create `company-news` requests with URL-escaped `symbol`, `from`, `to`, and the configured token. Parse the JSON array defensively and map only items having a non-empty `id`, HTTPS `url`, non-empty `headline`, and positive Unix `datetime`; use the requested ticker as the sole ticker in each normalized article. Preserve the complete JSON item as `RawPayload`. Delay one second after each request except the last. Catch request, status, and payload failures per ticker, log ticker plus status/error only, and continue. Never log the request URI because it contains the token.

- [ ] **Step 4: Run the focused tests to verify they pass**

Run: `dotnet test backend/tests/StockPulse.Worker.Tests --configuration Release --filter FullyQualifiedName~FinnhubNewsClientTests`

Expected: PASS, with no outbound network request.

- [ ] **Step 5: Run Worker regression tests and commit the provider task**

Run: `dotnet test backend/tests/StockPulse.Worker.Tests --configuration Release`

Expected: PASS.

```powershell
git add backend/src/StockPulse.Worker/Providers/Finnhub backend/tests/StockPulse.Worker.Tests/FinnhubNewsClientTests.cs
git commit -m "feat: ingest Finnhub watchlist news"
```

### Task 3: Local Setup Documentation and End-to-End Verification

**Files:**
- Modify: `docs/local-development.md`

**Interfaces:**
- Consumes: `Worker:UseMockProviders`, `Finnhub:ApiKey`, and the Worker project User Secrets ID configured in Task 1.
- Produces: a reproducible, secret-safe local setup and smoke-test procedure for real Watchlist news.

- [ ] **Step 1: Update the failing manual checklist expectation**

Replace the mock-only Worker instructions with two explicit modes: the existing default mock mode and a Finnhub mode that requires a User Secret. State that a free Finnhub key is obtained from the provider's dashboard and must not be pasted into the repository.

- [ ] **Step 2: Add exact local setup commands**

```powershell
dotnet user-secrets set "Finnhub:ApiKey" "<your-key>" --project backend/src/StockPulse.Worker
$env:Worker__UseMockProviders = 'false'
dotnet run --project backend/src/StockPulse.Worker
```

Document that the Worker reads up to 20 active Watchlist tickers in their configured order every 15 minutes, waits one second between provider calls, and leaves the mock mode enabled when `Worker__UseMockProviders` is unset or `true`.

- [ ] **Step 3: Add the real-provider smoke test**

Document these steps: add a supported US ticker such as `AAPL`; start API, dashboard, and Worker in Finnhub mode; wait for the first cycle; request `GET http://localhost:5000/api/news/latest?limit=30`; confirm at least one item has `sourceCode` `finnhub`; and confirm its article link opens. State that no live smoke test is possible until the user supplies a valid personal key.

- [ ] **Step 4: Run complete verification**

Run:

```powershell
dotnet test backend/StockPulse.sln --configuration Release
dotnet build backend/StockPulse.sln --configuration Release
npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless
npm.cmd run build --prefix frontend
dotnet format backend/StockPulse.sln --verify-no-changes
```

Expected: every command exits 0. Also run the real-provider smoke test after the user has set a valid API key; do not print the key in terminal output or reports.

- [ ] **Step 5: Commit documentation and verification task**

```powershell
git add docs/local-development.md
git commit -m "docs: add Finnhub local setup"
```

## Plan Self-Review

- **Spec coverage:** Task 1 covers provider selection, key validation, and polling cadence. Task 2 covers active Watchlist selection, 20-ticker cap, request pacing, mapping, dedup-compatible output, and isolated failures. Task 3 covers secret-safe local setup and all required verification.
- **Placeholder scan:** No TBD, TODO, deferred implementation, or unspecified error handling remains.
- **Type consistency:** `IProviderNewsClient.FetchNewsAsync(CancellationToken)` and `NormalizedNewsDto` remain the provider boundary; `WorkerOptions` and `FinnhubOptions` are defined by Task 1 and consumed by Task 2.
