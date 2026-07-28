# StockPulse

StockPulse is a Phase 0 local prototype for monitoring stock news. It provides an Angular dashboard, an ASP.NET Core API and SignalR hub, a PostgreSQL persistence layer, and a mock-news worker for repeatable local development.

## Local development

Follow the reproducible setup, environment-variable configuration, smoke test, and manual end-to-end checklist in [docs/local-development.md](docs/local-development.md).

The local stack uses these endpoints:

| Service | Address |
| --- | --- |
| Angular dashboard | http://localhost:4200 |
| API, OpenAPI, and SignalR | http://localhost:5000 |
| PostgreSQL | localhost:5432 |

The API CORS policy permits only `http://localhost:4200` for this prototype. Do not commit provider API keys, internal realtime shared keys, or production credentials. Configure secrets in the current shell or an approved local secret store.

## Phase 0 capabilities

- Global watchlist with normalized ticker symbols.
- Persisted, paginated news queries and a latest-news endpoint.
- Mock news ingestion every 15 seconds, normalization, database deduplication, and retryable realtime outbox delivery.
- SignalR `news:new` events with event-id deduplication and ticker subscriptions.
- Angular dark dashboard with virtual scrolling, loading/empty states, and new-news highlights.

External provider integrations, alert rules, production authentication, and deployment remain out of scope for Phase 0.

## Prerequisites

- .NET SDK 10
- Node.js with npm
- Docker Desktop with Docker Compose

## Verification

From the repository root, run the checks documented in the runbook:

```powershell
dotnet test backend\StockPulse.sln --configuration Release
dotnet build backend\StockPulse.sln --configuration Release
npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless
npm.cmd run build --prefix frontend
```

Backend integration tests need `STOCKPULSE_TEST_CONNECTION` to point to the local `stockpulse_test` database; the runbook shows the complete setup.

## Project structure

```text
backend/
  src/StockPulse.Api/            # REST API, SignalR, internal realtime endpoint
  src/StockPulse.Application/    # Use cases, DTOs, abstractions
  src/StockPulse.Contracts/      # Provider-neutral contracts
  src/StockPulse.Domain/         # Entities and enums
  src/StockPulse.Infrastructure/ # EF Core, PostgreSQL, repositories, migrations
  src/StockPulse.Worker/         # Mock ingestion and realtime outbox dispatcher
frontend/                        # Angular dashboard
docker/                          # PostgreSQL Compose configuration
docs/                            # Development documentation
```

## Disclaimer

StockPulse is under development and does not provide investment, financial, legal, or tax advice. News and market data can be delayed, incomplete, or inaccurate.
