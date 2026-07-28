# Mock News Link Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (\`- [ ]\`) syntax for tracking.

**Goal:** Replace unusable Phase 0 mock-news links with official Apple and NVIDIA articles and refresh the approved local mock data.

**Architecture:** Keep the mock provider and dashboard behavior unchanged. A Worker test loads the JSON fixture through \`MockNewsClient\`, so source-backed HTTPS URLs are enforced without test-time HTTP. The local Compose PostgreSQL volume is reset after the code commit to remove obsolete mock rows.

**Tech Stack:** .NET 10, xUnit, JSON, Docker Compose, PostgreSQL 16.

## Global Constraints

- Use only the official Apple and NVIDIA URLs in the approved design.
- Do not add a live provider, API key, external fetch, migration, or dashboard behavior change.
- Preserve IDs \`mock-001\`/\`mock-002\` and ticker arrays \`NVDA\`/\`AAPL\`.
- The reset is limited to \`stockpulse_postgres\`; it intentionally deletes only local StockPulse news and watchlist data.

---

### Task 1: Replace the fixture and lock the URL contract with a test

**Files:**
- Create: \`backend/tests/StockPulse.Worker.Tests/MockNewsClientTests.cs\`
- Modify: \`backend/src/StockPulse.Worker/mock-data/news.json\`

**Interfaces:**
- Consumes: \`MockNewsClient.FetchNewsAsync(CancellationToken)\`.
- Produces: two normalized mock records with source-backed HTTPS URLs.

- [ ] **Step 1: Add the failing fixture-contract test**

Create \`MockNewsClientTests.cs\` with a test named \`FetchNewsAsync_ReturnsOfficialSourceUrlsInsteadOfExampleTest\`. Create \`MockNewsClient\` with a test \`IHostEnvironment\` whose \`ContentRootPath\` is found by walking upward from \`AppContext.BaseDirectory\` until \`src/StockPulse.Worker/mock-data/news.json\` exists. Assert the returned collection in this order:

~~~csharp
Assert.Collection(
    articles,
    nvda =>
    {
        Assert.Equal("https://nvidianews.nvidia.com/news/nvidia-announces-financial-results-for-first-quarter-fiscal-2025", nvda.ExternalUrl);
        Assert.Equal("NVIDIA announces financial results for first quarter fiscal 2025", nvda.Title);
        Assert.Equal(new DateTimeOffset(2024, 5, 22, 0, 0, 0, TimeSpan.Zero), nvda.PublishedAtUtc);
        Assert.Equal(["NVDA"], nvda.Tickers);
    },
    aapl =>
    {
        Assert.Equal("https://www.apple.com/newsroom/2024/05/apple-reports-second-quarter-results/", aapl.ExternalUrl);
        Assert.Equal("Apple reports second quarter results", aapl.Title);
        Assert.Equal(new DateTimeOffset(2024, 5, 2, 0, 0, 0, TimeSpan.Zero), aapl.PublishedAtUtc);
        Assert.Equal(["AAPL"], aapl.Tickers);
    });
Assert.All(articles, article =>
{
    var uri = new Uri(article.ExternalUrl);
    Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
    Assert.NotEqual("example.test", uri.Host);
});
~~~

The local \`TestHostEnvironment\` must implement \`IHostEnvironment\` and set \`EnvironmentName = Environments.Development\`, \`ApplicationName = "StockPulse.Worker.Tests"\`, the resolved \`ContentRootPath\`, and a null-forgiven \`ContentRootFileProvider\`.

- [ ] **Step 2: Run the RED test**

Run:

~~~powershell
dotnet test backend/tests/StockPulse.Worker.Tests --filter FullyQualifiedName~MockNewsClientTests
~~~

Expected: FAIL because the fixture still returns \`example.test\` URLs.

- [ ] **Step 3: Replace only the two fixture records**

Set \`backend/src/StockPulse.Worker/mock-data/news.json\` to:

~~~json
[
  { "id": "mock-001", "url": "https://nvidianews.nvidia.com/news/nvidia-announces-financial-results-for-first-quarter-fiscal-2025", "title": "NVIDIA announces financial results for first quarter fiscal 2025", "summary": "NVIDIA reports first-quarter fiscal 2025 results, including strong data-center revenue driven by generative AI demand.", "publishedAtUtc": "2024-05-22T00:00:00Z", "tickers": ["NVDA"] },
  { "id": "mock-002", "url": "https://www.apple.com/newsroom/2024/05/apple-reports-second-quarter-results/", "title": "Apple reports second quarter results", "summary": "Apple reports an all-time Services revenue record and raises its quarterly dividend for the twelfth consecutive year.", "publishedAtUtc": "2024-05-02T00:00:00Z", "tickers": ["AAPL"] }
]
~~~

- [ ] **Step 4: Run GREEN and the affected suites**

Run:

~~~powershell
dotnet test backend/tests/StockPulse.Worker.Tests --filter FullyQualifiedName~MockNewsClientTests
dotnet test backend/StockPulse.sln --configuration Release
dotnet format backend/StockPulse.sln --verify-no-changes
git diff --check
~~~

Expected: the new test and full backend suite pass; formatting and whitespace checks are clean.

- [ ] **Step 5: Commit Task 1**

~~~powershell
git add backend/src/StockPulse.Worker/mock-data/news.json backend/tests/StockPulse.Worker.Tests/MockNewsClientTests.cs
git commit -m "fix: replace mock news links"
~~~

### Task 2: Refresh approved local data and verify the running stack

**Files:**
- No source-file changes.

**Interfaces:**
- Consumes: Task 1 fixture and the \`stockpulse-postgres\` Compose service.
- Produces: a recreated local database containing only the two new mock articles after ingestion.

- [ ] **Step 1: Stop API and Worker, but keep Angular running**

Stop the local \`StockPulse.Api\` and \`StockPulse.Worker\` processes before deleting the database volume.

- [ ] **Step 2: Reset only the StockPulse Compose data**

Run:

~~~powershell
docker compose -f docker/docker-compose.yml down -v
docker compose -f docker/docker-compose.yml up -d
~~~

Expected: only the local \`stockpulse_postgres\` volume is recreated.

- [ ] **Step 3: Apply migrations and restart the services**

Run:

~~~powershell
$env:STOCKPULSE_CONNECTION = 'Host=localhost;Port=5432;Database=stockpulse;Username=stockpulse;Password=stockpulse_local_only'
dotnet ef database update --project backend/src/StockPulse.Infrastructure --startup-project backend/src/StockPulse.Infrastructure
dotnet run --project backend/src/StockPulse.Api --urls http://localhost:5000
dotnet run --project backend/src/StockPulse.Worker
~~~

Run API and Worker in separate terminals. Expected: the Worker ingests exactly two records on its first cycle.

- [ ] **Step 4: Verify API data and real URLs**

Run:

~~~powershell
$news = Invoke-RestMethod -Uri 'http://localhost:5000/api/news/latest?limit=30'
$news | Select-Object id, title, url, tickers
Invoke-WebRequest -Uri 'https://www.apple.com/newsroom/2024/05/apple-reports-second-quarter-results/' -UseBasicParsing
Invoke-WebRequest -Uri 'https://nvidianews.nvidia.com/news/nvidia-announces-financial-results-for-first-quarter-fiscal-2025' -UseBasicParsing
~~~

Expected: the API returns exactly the two official URLs and both external requests succeed. Refresh \`http://localhost:4200\`; each card opens its official article.

## Plan Self-Review

### Spec coverage

Task 1 updates both official URLs and aligned metadata, blocks \`example.test\` with a no-network regression test, and avoids provider/UI changes. Task 2 performs the approved local-only data reset, migration, restart, and live verification.

### Placeholder scan

No deferred markers or ambiguous paths remain. Every source file, command, test name, URL, and deletion target is explicit.

### Type consistency

\`MockNewsClient.FetchNewsAsync\` already returns \`IReadOnlyList<NormalizedNewsDto>\`; the plan only asserts existing \`ExternalUrl\`, \`Title\`, \`PublishedAtUtc\`, and \`Tickers\` properties.
