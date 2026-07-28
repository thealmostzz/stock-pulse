# StockPulse Local Development

## Prerequisites

Install .NET SDK 10, Node.js with npm, and Docker Desktop with Docker Compose. Run all commands from the repository root in PowerShell.

The PostgreSQL credential in `docker/docker-compose.yml` is deliberately local-only. Do not reuse it outside this repository. Provider API keys, the internal realtime shared key, and production credentials must not be committed. Set the local internal key only in the active shell (or an approved local secret store):

```powershell
$realtimeSharedKey = [Guid]::NewGuid().ToString('N')
$env:InternalRealtime__SharedKey = $realtimeSharedKey
$env:RealtimeApi__SharedKey = $realtimeSharedKey
```

Each terminal that starts the API or Worker must receive the same two environment variables. The examples below assume they are started from the same PowerShell session or that the values have been set in each terminal.

## Start dependencies and apply migrations

Start PostgreSQL:

```powershell
docker compose -f docker/docker-compose.yml up -d
```

Set the design-time and test connection strings for the current shell, then apply migrations. EF Core uses the Infrastructure project for both the migrations project and startup project because it contains `StockPulseDbContextFactory`.

```powershell
$env:STOCKPULSE_CONNECTION = 'Host=localhost;Port=5432;Database=stockpulse;Username=stockpulse;Password=stockpulse_local_only'
$env:STOCKPULSE_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=stockpulse_test;Username=stockpulse;Password=stockpulse_local_only'
dotnet ef database update --project backend/src/StockPulse.Infrastructure --startup-project backend/src/StockPulse.Infrastructure
```

## Start applications

Open three terminals at the repository root. Before starting the API and Worker, set the same non-placeholder realtime shared key in each terminal as described above.

```powershell
dotnet run --project backend/src/StockPulse.Api --urls http://localhost:5000
dotnet run --project backend/src/StockPulse.Worker
npm.cmd start --prefix frontend
```

Open http://localhost:4200. Add `AAPL` or `NVDA` in Watchlist; the mock provider inserts news every 15 seconds. The API accepts browser requests only from `http://localhost:4200` through its local CORS policy.

## Automated verification

With PostgreSQL running and `STOCKPULSE_TEST_CONNECTION` set, run:

```powershell
dotnet test backend\StockPulse.sln --configuration Release
dotnet build backend\StockPulse.sln --configuration Release
npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless
npm.cmd run build --prefix frontend
```

All four commands must exit with code 0.

## Manual end-to-end checklist

1. Start PostgreSQL, API, Worker, and Angular as above.
2. Add `AAPL` through the UI and confirm `GET http://localhost:5000/api/watchlist` contains uppercase `AAPL`.
3. Wait at least 15 seconds and confirm `GET http://localhost:5000/api/news/latest?limit=30` contains a mock article exactly once after several worker cycles.
4. Open two browser tabs and confirm both receive the new-news highlight without a refresh.
5. Request `GET http://localhost:5000/api/news?pageSize=201` and confirm the response is `400 Bad Request` rather than an unbounded query.
6. Stop Worker and confirm the API and the existing dashboard remain usable.

## Quality review checklist

- Performance: news queries use `AsNoTracking`, server-side pagination, indexes for publication time, ticker, and impact score, Angular virtual scroll, outbox batching, and a 200-item API limit (the dashboard requests 30).
- Security: no provider key is exposed to the frontend; CORS allows only `http://localhost:4200`; the internal endpoint compares its shared key in constant time; API inputs are validated; development error responses use a generic problem title instead of a stack trace.
- Naming: `StockNews`, `WatchlistItem`, `NewsHubService`, `NewsIngestionPipeline`, and the documented API routes match the Phase 0 contracts.
- Extensibility: `IProviderNewsClient`, `IRealtimePublisher`, repository abstractions, and `NormalizedNewsDto` keep providers and transport details decoupled.

## Stop dependencies

```powershell
docker compose -f docker/docker-compose.yml down
```

Use `down -v` only when intentionally deleting local PostgreSQL data.
