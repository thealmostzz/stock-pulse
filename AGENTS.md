# Repository Guidelines

## Project Structure & Module Organization

StockPulse is a local stock-news prototype. `backend/src` follows clean-layer boundaries: `StockPulse.Api` exposes REST, SignalR, and internal endpoints; `Application` contains use cases, DTOs, and abstractions; `Domain` holds entities/enums; `Infrastructure` owns EF Core/PostgreSQL repositories and migrations; and `Worker` ingests mock news and dispatches the realtime outbox. Backend tests live in `backend/tests` by layer. The Angular application is in `frontend/src`: place shared API models and services under `app/core`, and screen-specific components under `app/features`. Local Compose assets are in `docker/`; runbooks and design records are in `docs/`.

## Build, Test, and Development Commands

Run commands from the repository root in PowerShell:

```powershell
docker compose -f docker/docker-compose.yml up -d       # start PostgreSQL
dotnet run --project backend/src/StockPulse.Api --urls http://localhost:5000
dotnet run --project backend/src/StockPulse.Worker
npm.cmd start --prefix frontend                           # Angular at :4200
dotnet test backend/StockPulse.sln --configuration Release
dotnet build backend/StockPulse.sln --configuration Release
npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless
npm.cmd run build --prefix frontend
```

Integration tests require `STOCKPULSE_TEST_CONNECTION`; applying schema changes requires `STOCKPULSE_CONNECTION` and `dotnet ef database update --project backend/src/StockPulse.Infrastructure --startup-project backend/src/StockPulse.Infrastructure`.

## Coding Style & Naming Conventions

Use four-space indentation for C# and two spaces for TypeScript/SCSS. Keep nullable warnings clean: the backend targets .NET 10, enables nullable references, and treats warnings as errors. Prefer `async` APIs with `CancellationToken`, focused services, repository abstractions, and server-side validation. Angular uses standalone, OnPush components and signals; name component files `*.component.ts`, services `*.service.ts`, and selectors `sp-*`. Use PascalCase for C# public types/methods, camelCase for TypeScript members, and kebab-case filenames.

## Testing Guidelines

Add or update an xUnit test in the matching `backend/tests/StockPulse.*.Tests` project for backend behavior, using descriptive `Method_Scenario_ExpectedResult` names. Place Angular Jasmine specs next to their source as `*.spec.ts`. Run all four verification commands above before requesting review; preserve query limits, deduplication, and realtime behavior in relevant tests.

## Commit, Pull Request, and Security Guidelines

Use Conventional Commit-style subjects seen in history, e.g. `feat: add watchlist filter`, `fix: reject invalid tickers`, or `docs: update runbook`. Keep commits narrowly scoped. PRs should explain behavior and test evidence, link the issue/design record where applicable, and include screenshots for dashboard changes. Never commit provider keys, realtime shared keys, or production credentials; set local secrets through environment variables as documented in `docs/local-development.md`.
