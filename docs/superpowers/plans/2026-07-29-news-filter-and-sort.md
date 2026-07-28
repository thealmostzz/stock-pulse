# News Filter and Sort Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Add server-driven ticker, source, sentiment, tag, and watchlist filtering with deterministic latest/impact sorting, paginated virtual scrolling, and URL-backed dashboard state.

**Architecture:** Extend the existing Application DTO, then apply the full query in NewsRepository before projection and pagination. Keep HTTP serialization in NewsApiService, create a focused standalone filter component, and make DashboardComponent the owner of URL state, paging, realtime matching, and list state.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core/Npgsql/PostgreSQL, xUnit, Angular 22 standalone components/signals, RxJS, Angular Router, CDK virtual scrolling, Jasmine/Karma.

## Global Constraints

- Keep GET /api/news backward compatible; absent sort remains latest-first.
- Do not add dependencies, full-text title/summary search, auth, or saved searches.
- watchlistOnly means the existing global active watchlist and uses AND semantics with all other filters.
- Keep query filtering/sorting/pagination server-side, API page size 1–200, and dashboard memory cap 300.
- Latest order is PublishedAtUtc DESC then Id DESC; impact order is ImpactScore DESC, PublishedAtUtc DESC, then Id DESC.
- Preserve Angular standalone/OnPush, typed outputs/signals, virtual scrolling, accessible labels/focus, and SignalR event-ID dedupe.
- Do not touch unrelated files, including backend/tests/StockPulse.Worker.Tests/MockNewsClientTests.cs.

---

## File Structure

| File | Responsibility |
| --- | --- |
| backend/src/StockPulse.Application/DTOs/NewsQueryRequest.cs | Query parameters for sort and global watchlist scope |
| backend/src/StockPulse.Application/Services/NewsQueryService.cs | Input validation and normalization |
| backend/src/StockPulse.Infrastructure/Persistence/Repositories/NewsRepository.cs | Server-side filtering and deterministic ordering |
| backend/tests/StockPulse.Application.Tests/NewsQueryServiceTests.cs | Query contract tests |
| backend/tests/StockPulse.Infrastructure.Tests/NewsRepositoryTests.cs | PostgreSQL query integration tests |
| frontend/src/app/core/models/news-query.ts | Query, sort, and paged response types |
| frontend/src/app/core/services/news-api.service.ts | Paginated query HTTP call |
| frontend/src/app/features/dashboard/news-filter.component.ts | Accessible filter controls |
| frontend/src/app/features/watchlist/watchlist-panel.component.ts | Active ticker output |
| frontend/src/app/features/dashboard/dashboard.component.ts | URL, paging, and realtime state |
| frontend/src/app/features/dashboard/news-feed.component.ts | Filter/paging presentation and scroll signal |

### Task 1: Extend and validate the backend query contract

**Files:**
- Modify: backend/src/StockPulse.Application/DTOs/NewsQueryRequest.cs
- Modify: backend/src/StockPulse.Application/Services/NewsQueryService.cs
- Modify: backend/tests/StockPulse.Application.Tests/NewsQueryServiceTests.cs
- Modify: backend/tests/StockPulse.Application.Tests/NewsControllerTests.cs

**Interfaces:**
- Produces NewsQueryRequest(string? Ticker, string? SourceCode, string? Sentiment, string? Tag, int Page = 1, int PageSize = 50, string? SortBy = null, bool WatchlistOnly = false).
- Produces SortBy normalized to publishedAt or impact; missing/blank values become publishedAt.

- [ ] **Step 1: Write the failing tests**

Add to NewsQueryServiceTests:

~~~csharp
[Fact]
public async Task QueryAsync_DefaultsMissingSortByToPublishedAt()
{
    var repository = new CapturingNewsRepository();
    var service = new NewsQueryService(repository);

    await service.QueryAsync(new NewsQueryRequest(null, null, null, null), CancellationToken.None);

    Assert.Equal("publishedAt", repository.LastRequest!.SortBy);
}

[Fact]
public async Task QueryAsync_NormalizesImpactSortBy()
{
    var repository = new CapturingNewsRepository();
    var service = new NewsQueryService(repository);

    await service.QueryAsync(
        new NewsQueryRequest(null, null, null, null, SortBy: " IMPACT "),
        CancellationToken.None);

    Assert.Equal("impact", repository.LastRequest!.SortBy);
}

[Fact]
public async Task QueryAsync_RejectsUnsupportedSortBy()
{
    var service = NewsQueryService.CreateForTest();

    await Assert.ThrowsAsync<ArgumentException>(() => service.QueryAsync(
        new NewsQueryRequest(null, null, null, null, SortBy: "title"),
        CancellationToken.None));
}
~~~

Add new NewsQueryRequest(null, null, null, null, SortBy: "title") to InvalidQueryRequests in NewsControllerTests.

- [ ] **Step 2: Run test to verify it fails**

Run:

~~~powershell
dotnet test backend/StockPulse.sln --configuration Release --filter "FullyQualifiedName~NewsQueryServiceTests"
~~~

Expected: FAIL because SortBy does not exist.

- [ ] **Step 3: Write minimal implementation**

Change the request record:

~~~csharp
public sealed record NewsQueryRequest(
    string? Ticker,
    string? SourceCode,
    string? Sentiment,
    string? Tag,
    int Page = 1,
    int PageSize = 50,
    string? SortBy = null,
    bool WatchlistOnly = false);
~~~

In NewsQueryService.QueryAsync, before the normalized with expression:

~~~csharp
var sortBy = string.IsNullOrWhiteSpace(request.SortBy)
    ? "publishedAt"
    : request.SortBy.Trim().ToLowerInvariant();

if (sortBy is not ("publishedAt" or "impact"))
{
    throw new ArgumentException("SortBy is invalid.", nameof(request));
}
~~~

Add SortBy = sortBy and WatchlistOnly = request.WatchlistOnly to that with expression. Leave existing ticker, source, sentiment, page, and page-size validation intact.

- [ ] **Step 4: Run test to verify it passes**

~~~powershell
dotnet test backend/StockPulse.sln --configuration Release --filter "FullyQualifiedName~NewsQueryServiceTests|FullyQualifiedName~NewsControllerTests"
~~~

Expected: PASS; invalid sort returns the existing 400 validation response.

- [ ] **Step 5: Commit**

~~~powershell
git add backend/src/StockPulse.Application/DTOs/NewsQueryRequest.cs backend/src/StockPulse.Application/Services/NewsQueryService.cs backend/tests/StockPulse.Application.Tests/NewsQueryServiceTests.cs backend/tests/StockPulse.Application.Tests/NewsControllerTests.cs
git commit -m "feat: validate news sort query"
~~~

### Task 2: Apply watchlist scope and deterministic sorting in PostgreSQL

**Files:**
- Modify: backend/src/StockPulse.Infrastructure/Persistence/Repositories/NewsRepository.cs
- Create: backend/tests/StockPulse.Infrastructure.Tests/NewsRepositoryTests.cs

**Interfaces:**
- Consumes validated SortBy and WatchlistOnly.
- Produces filter predicates before CountAsync and ordering before Skip/Take.

- [ ] **Step 1: Write the failing repository integration tests**

Create NewsRepositoryTests.cs. Reuse TestDatabaseConnection.GetConnectionString, random schema setup, GenerateCreateScript, and finally cleanup from StockPulseDbContextTests.

Seed three news rows:
- newsA: AAPL, impact 0.20, published 10:00.
- newsB: NVDA, impact 0.90, published 09:00.
- newsC: MSFT, impact 0.90, published 11:00.

Seed active AAPL and inactive NVDA watchlist items. Assert:

~~~csharp
var impactResult = await repository.QueryAsync(
    new NewsQueryRequest(null, null, null, null, SortBy: "impact"),
    CancellationToken.None);

Assert.Equal([newsC.Id, newsB.Id, newsA.Id], impactResult.Items.Select(item => item.Id));

var watchlistResult = await repository.QueryAsync(
    new NewsQueryRequest(null, null, null, null, WatchlistOnly: true),
    CancellationToken.None);

Assert.Equal([newsA.Id], watchlistResult.Items.Select(item => item.Id));
Assert.Equal(1, watchlistResult.TotalCount);
~~~

Add a no-active-watchlist case that expects empty Items and TotalCount == 0.

- [ ] **Step 2: Run test to verify it fails**

~~~powershell
dotnet test backend/StockPulse.sln --configuration Release --filter "FullyQualifiedName~NewsRepositoryTests"
~~~

Expected: FAIL because global-watchlist scope and impact ordering do not exist. Set STOCKPULSE_TEST_CONNECTION as documented in docs/local-development.md if required.

- [ ] **Step 3: Write minimal implementation**

After the tag predicate and before CountAsync:

~~~csharp
if (request.WatchlistOnly)
{
    query = query.Where(news => news.Tickers.Any(ticker =>
        dbContext.WatchlistItems.Any(item => item.IsActive && item.Ticker == ticker.Ticker)));
}
~~~

Add and use before Skip/Take:

~~~csharp
private static IOrderedQueryable<StockNews> ApplyOrdering(
    IQueryable<StockNews> query,
    string sortBy) => sortBy == "impact"
        ? query.OrderByDescending(news => news.ImpactScore)
            .ThenByDescending(news => news.PublishedAtUtc)
            .ThenByDescending(news => news.Id)
        : query.OrderByDescending(news => news.PublishedAtUtc)
            .ThenByDescending(news => news.Id);
~~~

Use ApplyOrdering(query, request.SortBy ?? "publishedAt") only in QueryAsync. Do not change GetLatestAsync, AsNoTracking, or counting before ordering/pagination.

- [ ] **Step 4: Run test to verify it passes**

~~~powershell
dotnet test backend/StockPulse.sln --configuration Release --filter "FullyQualifiedName~NewsRepositoryTests|FullyQualifiedName~StockPulseDbContextTests"
~~~

Expected: PASS and inactive NVDA is excluded.

- [ ] **Step 5: Commit**

~~~powershell
git add backend/src/StockPulse.Infrastructure/Persistence/Repositories/NewsRepository.cs backend/tests/StockPulse.Infrastructure.Tests/NewsRepositoryTests.cs
git commit -m "feat: filter and sort paged news"
~~~

### Task 3: Add typed frontend query serialization

**Files:**
- Create: frontend/src/app/core/models/news-query.ts
- Modify: frontend/src/app/core/services/news-api.service.ts
- Modify: frontend/src/app/core/services/news-api.service.spec.ts

**Interfaces:**
- Produces NewsSortBy = 'publishedAt' | 'impact'.
- Produces NewsQuery and PagedNewsResponse.
- Produces NewsApiService.query(query: NewsQuery): Observable<PagedNewsResponse>.

- [ ] **Step 1: Write the failing HTTP test**

Add:

~~~ts
it('serializes populated filters, impact sort, and watchlist scope', () => {
  const service = TestBed.inject(NewsApiService);
  const httpTesting = TestBed.inject(HttpTestingController);

  service.query({
    ticker: 'NVDA', sourceCode: 'mock', sentiment: 'Positive', tag: 'earnings',
    page: 2, pageSize: 30, sortBy: 'impact', watchlistOnly: true,
  }).subscribe();

  const request = httpTesting.expectOne((candidate) =>
    candidate.url === environment.apiBaseUrl + '/api/news');
  expect(request.request.params.get('sortBy')).toBe('impact');
  expect(request.request.params.get('watchlistOnly')).toBe('true');
  expect(request.request.params.get('page')).toBe('2');
  request.flush({ items: [], page: 2, pageSize: 30, totalCount: 0, hasMore: false });
  httpTesting.verify();
});
~~~

- [ ] **Step 2: Run test to verify it fails**

~~~powershell
npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/core/services/news-api.service.spec.ts'
~~~

Expected: FAIL because query and its types do not exist.

- [ ] **Step 3: Write minimal implementation**

Create news-query.ts:

~~~ts
import { NewsItem } from './news-item';

export type NewsSortBy = 'publishedAt' | 'impact';
export type NewsSentimentFilter = NewsItem['sentiment'];

export interface NewsQuery {
  ticker: string | null;
  sourceCode: string | null;
  sentiment: NewsSentimentFilter | null;
  tag: string | null;
  page: number;
  pageSize: number;
  sortBy: NewsSortBy;
  watchlistOnly: boolean;
}

export interface PagedNewsResponse {
  items: NewsItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  hasMore: boolean;
}
~~~

Implement query in NewsApiService with HttpParams: emit text fields only when non-empty and always send page, pageSize, sortBy, and watchlistOnly. Reuse rethrowApiError<PagedNewsResponse>.

- [ ] **Step 4: Run test to verify it passes**

Run the Step 2 command.

Expected: PASS; existing getLatest tests remain green.

- [ ] **Step 5: Commit**

~~~powershell
git add frontend/src/app/core/models/news-query.ts frontend/src/app/core/services/news-api.service.ts frontend/src/app/core/services/news-api.service.spec.ts
git commit -m "feat: query paged news from dashboard"
~~~

### Task 4: Build accessible controls and propagate active watchlist tickers

**Files:**
- Create: frontend/src/app/features/dashboard/news-filter.component.ts
- Create: frontend/src/app/features/dashboard/news-filter.component.spec.ts
- Modify: frontend/src/app/features/watchlist/watchlist-panel.component.ts
- Modify: frontend/src/app/features/watchlist/watchlist-panel.component.spec.ts

**Interfaces:**
- Produces NewsFilterComponent.query = input.required<NewsQuery>(), queryChanged = output<Partial<NewsQuery>>(), clearRequested = output<void>().
- Produces WatchlistPanelComponent.activeTickersChanged = output<readonly string[]>().

- [ ] **Step 1: Write the failing component tests**

Create NewsFilterComponent test:

~~~ts
it('emits an impact sort change', () => {
  const fixture = TestBed.createComponent(NewsFilterComponent);
  fixture.componentRef.setInput('query', createQuery());
  let change: Partial<NewsQuery> | undefined;
  fixture.componentInstance.queryChanged.subscribe((value) => change = value);
  fixture.detectChanges();

  const select = fixture.nativeElement.querySelector('#news-sort') as HTMLSelectElement;
  select.value = 'impact';
  select.dispatchEvent(new Event('change'));

  expect(change).toEqual({ sortBy: 'impact' });
});
~~~

Add clear-button plus visible-label assertions for ticker, source, sentiment, tag, watchlist, and sort. Add WatchlistPanel tests that exercise successful initial load/add/remove and verify output includes active tickers only.

- [ ] **Step 2: Run test to verify it fails**

~~~powershell
npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/news-filter.component.spec.ts'
~~~

Expected: FAIL because NewsFilterComponent does not exist.

- [ ] **Step 3: Write minimal implementation**

Create an OnPush standalone component importing FormsModule. Use controls with IDs news-ticker, news-source, news-sentiment, news-tag, news-watchlist-only, and news-sort. Each calls:

~~~ts
update(change: Partial<NewsQuery>): void {
  this.queryChanged.emit(change);
}
~~~

Use a type=button clear control; do not trim while typing.

Add the following to WatchlistPanelComponent and call it after successful fetch, add, and remove updates:

~~~ts
readonly activeTickersChanged = output<readonly string[]>();

private publishActiveTickers(items: readonly WatchlistItem[]): void {
  this.activeTickersChanged.emit(items.filter((item) => item.isActive).map((item) => item.ticker));
}
~~~

- [ ] **Step 4: Run test to verify it passes**

~~~powershell
npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/news-filter.component.spec.ts|src/app/features/watchlist/watchlist-panel.component.spec.ts'
~~~

Expected: PASS; controls remain keyboard-accessible and watchlist CRUD semantics stay unchanged.

- [ ] **Step 5: Commit**

~~~powershell
git add frontend/src/app/features/dashboard/news-filter.component.ts frontend/src/app/features/dashboard/news-filter.component.spec.ts frontend/src/app/features/watchlist/watchlist-panel.component.ts frontend/src/app/features/watchlist/watchlist-panel.component.spec.ts
git commit -m "feat: add news filter controls"
~~~

### Task 5: Connect URL-backed query, paging, and filtered realtime updates

**Files:**
- Modify: frontend/src/app/features/dashboard/dashboard.component.ts
- Modify: frontend/src/app/features/dashboard/dashboard.component.html
- Modify: frontend/src/app/features/dashboard/dashboard.component.spec.ts
- Modify: frontend/src/app/features/dashboard/news-feed.component.ts
- Modify: frontend/src/app/features/dashboard/news-feed.component.spec.ts

**Interfaces:**
- Produces updateQuery(change: Partial<NewsQuery>), clearQuery(), loadNextPage(), and setActiveWatchlistTickers(tickers: readonly string[]).
- Produces feed loadMore = output<void>(), totalCount = input(0), isLoadingMore = input(false), errorMessage = input('').

- [ ] **Step 1: Write failing dashboard/feed tests**

Add:

~~~ts
it('resets to page one and requests impact order after a filter change', () => {
  const requested: NewsQuery[] = [];
  const component = createQueryComponent(requested);

  component.updateQuery({ sortBy: 'impact', sentiment: 'Negative' });

  expect(requested.at(-1)).toEqual(jasmine.objectContaining({
    page: 1, sortBy: 'impact', sentiment: 'Negative',
  }));
});
~~~

Add a buffered SignalR test that selects ticker AAPL, emits NVDA, advances 250ms, and asserts no item is inserted. Add impact sort tie test for newest then highest Id. In feed tests, subscribe to loadMore and simulate scroll within five rows of the end; assert it emits only when hasMore is true and isLoadingMore is false.

- [ ] **Step 2: Run test to verify it fails**

~~~powershell
npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/dashboard.component.spec.ts|src/app/features/dashboard/news-feed.component.spec.ts'
~~~

Expected: FAIL because the dashboard uses getLatest and the feed has no filter/paging interface.

- [ ] **Step 3: Write minimal implementation**

Define:

~~~ts
const defaultNewsQuery: NewsQuery = {
  ticker: null, sourceCode: null, sentiment: null, tag: null,
  page: 1, pageSize: 30, sortBy: 'publishedAt', watchlistOnly: false,
};

readonly query = signal<NewsQuery>(defaultNewsQuery);
readonly totalCount = signal(0);
readonly hasMore = signal(false);
readonly isLoadingMore = signal(false);
readonly errorMessage = signal('');
readonly activeWatchlistTickers = signal<readonly string[]>([]);
~~~

Read valid ticker, sourceCode, sentiment, tag, sortBy, watchlistOnly, and page from the URL at initialization. Use monotonically increasing request IDs so only the current HTTP response updates state. Page one replaces items; later pages append through the existing unique merge/cap helper.

updateQuery trims text, resets page to one, writes only non-default query params, and loads. loadNextPage returns unless hasMore is true and both loading signals are false; otherwise request page+1, append unique items, then update URL page. clearQuery restores defaults/removes all query parameters.

matchesActiveQuery must check every active condition: normalized ticker member, lowercased source code, sentiment equality, exact trimmed tag, and at least one active watchlist ticker. Merge matching realtime data with a sortBy comparator before enforcing cap.

Bind filter changes/clear, activeTickersChanged, and feed loadMore in dashboard HTML. In NewsFeedComponent import NewsFilterComponent, forward output, render accessible count/error/loading-more messages, and emit loadMore from scrolledIndexChange at items length minus five. Keep itemSize=154.

- [ ] **Step 4: Run tests and production build**

~~~powershell
npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/dashboard.component.spec.ts|src/app/features/dashboard/news-feed.component.spec.ts|src/app/features/dashboard/news-filter.component.spec.ts'
npm.cmd run build --prefix frontend
~~~

Expected: PASS without type or style budget errors.

- [ ] **Step 5: Commit**

~~~powershell
git add frontend/src/app/features/dashboard/dashboard.component.ts frontend/src/app/features/dashboard/dashboard.component.html frontend/src/app/features/dashboard/dashboard.component.spec.ts frontend/src/app/features/dashboard/news-feed.component.ts frontend/src/app/features/dashboard/news-feed.component.spec.ts
git commit -m "feat: filter realtime news dashboard"
~~~

### Task 6: Complete verification

- [ ] **Step 1: Run all automated verification**

~~~powershell
dotnet test backend/StockPulse.sln --configuration Release
dotnet build backend/StockPulse.sln --configuration Release
npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless
npm.cmd run build --prefix frontend
~~~

Expected: all commands exit 0.

- [ ] **Step 2: Manually verify behavior**

1. Default state is latest-first paginated news.
2. Ticker, source, sentiment, and tag constrain results individually.
3. Watchlist-only reacts after add/remove and filters realtime events.
4. Impact sort and ties are deterministic.
5. End-of-list scrolling makes one next-page request with no duplicates.
6. Refresh restores filter/sort/page URL state; clear returns defaults.
7. Keyboard focus, labels, empty/error states, 375px, and 1440px layouts remain usable.

- [ ] **Step 3: Inspect final state**

~~~powershell
git diff --check
git status --short
git log -5 --oneline
~~~

Expected: no whitespace errors and only related changes.

- [ ] **Step 4: Commit a correction only if verification required one**

Stage only the exact feature files changed by the correction and commit with the Conventional Commit subject `fix: complete news filter verification`.
