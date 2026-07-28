# StockPulse News Filter and Sort Design

**Date:** 2026-07-29  
**Scope:** Add server-driven filtering, sorting, pagination, and URL-backed view state to the Angular news dashboard.  
**Out of scope:** Free-text title/summary search, saved searches, notifications, authentication, and changes to the realtime delivery contract.

## Goal

Let a user narrow the news feed by ticker, source, sentiment, tag, or the active global watchlist, then order the complete matching result set by latest publication time or impact score. The view must remain reproducible after refresh or when its URL is shared.

## Existing Facts

- `GET /api/news` already supports one `ticker`, `sourceCode`, `sentiment`, `tag`, page, and page size.
- The dashboard currently calls `GET /api/news/latest` for 30 articles and keeps up to 300 articles in memory for SignalR events.
- Watchlist data is global for this Phase 0 prototype; it is not associated with an authenticated user.
- PostgreSQL already indexes publication time, impact score, and ticker membership.

## User Experience

The news-feed header contains a compact filter area:

- Ticker, source, and tag text fields. Inputs are trimmed; ticker is normalized by the existing backend normalization path.
- Sentiment select: all, positive, neutral, or negative.
- `Watchlist only` toggle. When on, return articles related to at least one active watchlist ticker. A manually entered ticker remains a further restriction.
- Sort select: `Latest` (default) or `Highest impact`.
- A clear action restores the unfiltered, latest-first view.

The filter/sort/page state is reflected in query parameters. The initial page begins at one. Reaching the virtual-scroll end requests the next page only when `hasMore` is true and no request is already in flight. Changing any filter or sort cancels/ignores the obsolete response, resets to page one, and scrolls the viewport to the beginning.

Source and tag fields deliberately accept text rather than requiring new metadata endpoints. Users can enter a known source code or tag; no full-text headline/summary search is introduced.

## API Contract

Extend `NewsQueryRequest` with:

- `SortBy`: optional `publishedAt` or `impact`; defaults to `publishedAt`.
- `WatchlistOnly`: optional boolean; defaults to `false`.

`GET /api/news` remains backward compatible. Unsupported `sortBy` values produce the existing 400 validation response. Existing page-size limits remain unchanged.

When `watchlistOnly=true`, the repository filters news through active `WatchlistItem` entries. If no active watchlist entries exist, it returns an empty paginated result. It is combined with all other filters using AND semantics.

Sorting is deterministic:

- `publishedAt`: `PublishedAtUtc DESC`, then `Id DESC`.
- `impact`: `ImpactScore DESC`, then `PublishedAtUtc DESC`, then `Id DESC`.

## Architecture and Data Flow

1. The Dashboard owns a typed query signal, derived from URL query parameters.
2. A focused news-query API service serializes only populated parameters and requests the paginated endpoint.
3. The Dashboard replaces its loaded items for page one and appends unique items for later pages. It preserves the 300-item in-memory cap.
4. The repository applies filters and sorting before projection and pagination; all work stays server-side.
5. Each SignalR article is evaluated against the active filter before it is merged. It is inserted only when it matches. A `watchlistOnly` evaluation uses the dashboard's current active ticker set, matching the global API meaning for the current prototype.

The `/api/news/latest` endpoint stays available for compatibility but is no longer used by the dashboard.

## Errors and Accessibility

- Invalid API query parameters use the existing Problem Details error path.
- The dashboard shows an inline, accessible error state while retaining the last successfully rendered list where safe.
- Filter controls have visible labels, keyboard focus treatment, and clear loading/disabled state during a query.
- The item count and loading-more status are announced without interrupting keyboard scrolling.

## Tests and Verification

- Application/repository tests cover `SortBy` validation, both deterministic ordering modes, combined filters, `watchlistOnly`, and an empty watchlist.
- API/service tests verify serialization of populated query parameters and error handling.
- Dashboard tests verify URL initialization, clear/reset, pagination append/deduplication, stale-query protection, and filtered SignalR insertion.
- Run the repository's backend test/build and Angular test/build commands. Manually check responsive filter controls, keyboard use, URL restore, paging, realtime filtering, and empty/error states.

## Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| Offset pagination can shift while realtime news arrives | Deduplicate by article ID; reset to page one on filter change; retain deterministic tie-breakers. |
| Watchlist changes during an open filtered view | Refresh page one after add/remove; realtime matching uses the current watchlist ticker set. |
| Queries become slower as data grows | Keep existing page limits and server-side filtering; review generated query plans and add a composite index only if measurements show it is needed. |
| Future per-user watchlists change semantics | Keep `watchlistOnly` explicitly documented as global Phase 0 behavior; replace the repository scope when identity is introduced. |
