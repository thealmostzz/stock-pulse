# Mock News Link Remediation Design

## Goal

Make every Phase 0 mock-news card open a real, source-backed article rather than the reserved `example.test` domain.

## Scope

- Replace the two records in `backend/src/StockPulse.Worker/mock-data/news.json` with official Apple and NVIDIA newsroom URLs.
- Align each record's title, summary, and published UTC timestamp with the selected article.
- Add regression coverage that reads the fixture through `MockNewsClient` and rejects `example.test` URLs while requiring HTTPS URLs from the expected official hosts.
- Reset only the local Docker Compose PostgreSQL volume after the change, then reapply migrations and restart the local Worker so old mock rows cannot remain in the dashboard.

## Data

| Ticker | Official source | Fixture title | Published UTC |
| --- | --- | --- | --- |
| AAPL | `www.apple.com/newsroom/2024/05/apple-reports-second-quarter-results/` | Apple reports second quarter results | 2024-05-02T00:00:00Z |
| NVDA | `nvidianews.nvidia.com/news/nvidia-announces-financial-results-for-first-quarter-fiscal-2025` | NVIDIA announces financial results for first quarter fiscal 2025 | 2024-05-22T00:00:00Z |

The fixture remains mock data: no live provider is added, no external HTTP request runs during ingestion or tests, and the URLs are only opened when a user selects a news card.

## Data Reset Safety

`docker compose -f docker/docker-compose.yml down -v` deletes the `stockpulse_postgres` Docker volume only. It deletes local news and watchlist data, not source files, Git history, Docker images, or any non-StockPulse database. The user explicitly approved this reset.

After the reset: start Compose, apply the existing EF migrations, restart API and Worker, and verify that the two returned API news URLs do not use `example.test`.

## Testing and Verification

1. Add a failing Worker test for the mock fixture's official HTTPS hosts and absence of `example.test`.
2. Run the targeted test to observe the expected failure.
3. Update only the fixture, then rerun the targeted Worker test and full backend/frontend test suites.
4. Use `Invoke-WebRequest` against both fixture URLs and `GET /api/news/latest?limit=30` after reset to verify reachable links and persisted data.

## Non-Goals

- Adding paid/free live news providers, API keys, scraping, or provider scheduling.
- Changing the dashboard link behavior or adding a local article reader.
- Migrating or retaining the disposable local mock data.
