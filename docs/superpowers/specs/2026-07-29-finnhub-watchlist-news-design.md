# Finnhub Watchlist News Design

## Goal

Replace the development-only mock news source with a Finnhub-backed provider that ingests current company news for the user's Watchlist without exposing the API key or exceeding the free-tier rate limit.

## Scope

- Query company news only for ticker symbols present in the global Watchlist.
- Process at most 20 symbols per polling cycle.
- Poll every 15 minutes in Finnhub mode.
- Retain the mock provider as an explicit local-development option.
- Keep the existing persistence, de-duplication, outbox, and SignalR delivery pipeline unchanged.

No authentication, market-wide news, historical backfill beyond the provider request window, or frontend changes are included.

## Architecture

The Worker will select the enabled Watchlist ticker symbols before requesting provider data.  A scoped `FinnhubNewsClient` will receive that symbol list, call Finnhub's Company News endpoint once per symbol, and convert every valid response item into the existing `NormalizedNewsDto` contract.  The existing `NewsIngestionPipeline` will persist and publish the normalized articles exactly as it does for mock data.

Provider selection is configuration-driven.  `Worker:UseMockProviders` remains available for repeatable development and tests.  When mock mode is disabled, the Worker registers Finnhub instead and requires `Finnhub:ApiKey`.  The key is supplied through .NET User Secrets or an environment variable; it must never be added to JSON configuration, source code, logs, tests, or Git.

## Request and Rate-Limit Policy

The Worker uses a 15-minute timer in Finnhub mode.  Each cycle reads up to 20 normalized Watchlist tickers in stable order.  It issues requests sequentially with a minimum one-second gap, yielding at most 20 requests in a minute and leaving margin below Finnhub's free-tier 60-requests-per-minute limit.

Each request uses the company-news endpoint for its ticker with a UTC date range.  The start date is the latest successfully persisted news timestamp for that ticker, with a bounded recent fallback when no stored news exists; the end date is the current UTC date.  The existing de-duplication hash remains the authority for duplicate safety when provider date windows overlap.

## Failure Handling

- A malformed provider item is skipped without aborting the ticker request.
- A failed request, non-success HTTP status, invalid payload, or rate-limit response is logged without including the API key and does not stop remaining ticker requests.
- A cancellation request stops the current cycle promptly.
- If the Watchlist is empty, no Finnhub request is made.

## Data Mapping

Finnhub response fields are mapped into `NormalizedNewsDto` as follows:

| Finnhub field | Destination |
| --- | --- |
| `id` | `ProviderNewsKey` |
| `url` | `ExternalUrl` |
| `headline` | `Title` |
| `summary` | `Summary` |
| `datetime` (Unix seconds) | `PublishedAtUtc` |
| requested Watchlist ticker | `Tickers` |
| full response item | `RawPayload` |

The provider source code is `finnhub`.

## Validation and Tests

Unit tests will use a stubbed `HttpMessageHandler` and a test database where repository access is required.  They will cover URL construction, mapping, UTC conversion, empty Watchlist behavior, the 20-ticker cap, per-ticker failure isolation, missing key validation, and provider registration.  Existing mock-provider tests must continue to pass.

The local-development guide will describe obtaining a free Finnhub key, configuring User Secrets, starting the Worker in Finnhub mode, and confirming a Watchlist news item appears in the API and dashboard.

## Acceptance Criteria

With a valid local Finnhub API key and at least one Watchlist ticker, the Worker ingests real Finnhub company news, labels it `finnhub`, persists it through the existing pipeline, and publishes it to the dashboard.  With mock mode enabled, the existing fixture-based behavior still works.  No API key is committed or emitted in diagnostic output.
