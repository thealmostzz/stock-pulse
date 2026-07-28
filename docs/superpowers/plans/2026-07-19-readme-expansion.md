# README Expansion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the minimal root README with accurate bilingual product and developer documentation for the current StockPulse MVP.

**Architecture:** Keep all public onboarding content in the root `README.md`. Present equivalent English and Thai content within every section, and draw a strict boundary between the implemented local prototype and future product capabilities. Use only routes, tooling versions, and service configuration verified in the repository.

**Tech Stack:** Markdown, .NET 10, ASP.NET Core Web API, Angular 18, PostgreSQL 16, Docker Compose, SignalR.

## Global Constraints

- Modify only `README.md`; do not alter application code, configuration, or the user's existing working-tree changes.
- Every primary README heading must include English and Thai, with English copy first and equivalent Thai copy immediately after it.
- Call implemented features “available” only when supported by source code; label the dashboard UI, provider ingestion, alerting, and production deployment as planned work.
- Show the committed Docker PostgreSQL password only as a local-development credential and never encourage committing API keys or production secrets.
- Use PowerShell commands for the local setup instructions because the repository is maintained from Windows.

---

### Task 1: Replace the root README with bilingual, accurate onboarding documentation

**Files:**
- Modify: `README.md`
- Reference: `backend/Directory.Build.props`
- Reference: `frontend/package.json`
- Reference: `docker/docker-compose.yml`
- Reference: `backend/src/StockPulse.Api/Program.cs`
- Reference: `backend/src/StockPulse.Api/Controllers/NewsController.cs`
- Reference: `backend/src/StockPulse.Api/Controllers/WatchlistController.cs`
- Reference: `backend/src/StockPulse.Api/Hubs/NewsHub.cs`

**Interfaces:**
- Consumes: current service paths and configuration from the referenced files.
- Produces: a self-contained root README for product discovery and local developer onboarding.

- [ ] **Step 1: Capture the verified facts that the README may state as current functionality**

Confirm the following source-backed facts before authoring: the solution targets `.NET 10`; the frontend uses Angular `18.2`; Docker Compose starts PostgreSQL `16-alpine` at `localhost:5432`; the API development profile uses `http://localhost:5179`; the frontend development server uses `http://localhost:4200`; REST routes are `GET /api/news`, `GET /api/news/latest`, `GET|POST /api/watchlist`, and `DELETE /api/watchlist/{ticker}`; and the SignalR hub is `/hubs/news`.

Run:

```powershell
rg -n "TargetFramework|@angular/core|postgres:16|applicationUrl|Route|Http(Get|Post|Delete)|MapHub" backend frontend docker
```

Expected: matching configuration and controller declarations for every stated fact.

- [ ] **Step 2: Replace `README.md` with the following section structure and content requirements**

Write a complete Markdown document with these exact heading pairs, preserving the English-first bilingual order:

```markdown
# StockPulse

## Overview / ภาพรวม
## MVP status / สถานะ MVP
## Current capabilities / ความสามารถปัจจุบัน
## Roadmap / แผนงาน
## Architecture / สถาปัตยกรรม
## Technology stack / เทคโนโลยีที่ใช้
## Prerequisites / สิ่งที่ต้องเตรียม
## Quick start / เริ่มต้นใช้งานอย่างรวดเร็ว
## API reference / เอกสารอ้างอิง API
## Realtime updates / การอัปเดตแบบเรียลไทม์
## Testing / การทดสอบ
## Project structure / โครงสร้างโปรเจกต์
## Development notes / ข้อควรทราบสำหรับการพัฒนา
## Disclaimer / ข้อจำกัดความรับผิดชอบ
## Contributing / การมีส่วนร่วม
```

The Quick start section must include these runnable PowerShell commands, keeping the database password explicitly marked local-only:

```powershell
docker compose -f docker/docker-compose.yml up -d

$env:ConnectionStrings__StockPulse = 'Host=localhost;Port=5432;Database=stockpulse;Username=stockpulse;Password=stockpulse_local_only'
dotnet run --project backend/src/StockPulse.Api

Set-Location frontend
npm ci
npm start
```

For API examples, include a `GET /api/news/latest?limit=20` request, a `POST /api/watchlist` request with ticker `NVDA`, and `DELETE /api/watchlist/NVDA`. State that Swagger/OpenAPI is exposed only in the Development environment. For SignalR, document the hub route and `SubscribeTicker` / `UnsubscribeTicker` methods without claiming a complete client UI exists. Include an architecture flow showing Angular, API, SignalR, PostgreSQL, and the current placeholder Worker; label external provider ingestion as planned.

- [ ] **Step 3: Inspect the Markdown and check documentation scope**

Run:

```powershell
git diff --check -- README.md
rg -n "Alpha Vantage|Finnhub|SEC EDGAR|dashboard|alert|production|implemented|available" README.md
```

Expected: no whitespace errors, and each future capability is explicitly labelled as planned or not yet implemented.

- [ ] **Step 4: Commit the documentation change separately**

Run:

```powershell
git add -- README.md
git commit -m "docs: expand bilingual project README"
```

Expected: one commit containing only the root README change.

### Task 2: Verify all documented commands and references

**Files:**
- Verify: `README.md`
- Reference: `backend/StockPulse.sln`
- Reference: `frontend/package.json`

**Interfaces:**
- Consumes: README instructions written in Task 1.
- Produces: evidence that every documented local command and source path is valid without modifying application state.

- [ ] **Step 1: Verify referenced source paths and command entrypoints exist**

Run:

```powershell
$paths = @(
  'backend/StockPulse.sln',
  'backend/src/StockPulse.Api/StockPulse.Api.csproj',
  'frontend/package.json',
  'docker/docker-compose.yml'
)
$paths | ForEach-Object { "$_ : $(Test-Path $_)" }
dotnet sln backend/StockPulse.sln list
npm --prefix frontend run
```

Expected: every `Test-Path` value is `True`; the solution list succeeds; npm lists the `start`, `build`, `watch`, and `test` scripts.

- [ ] **Step 2: Check the final documentation diff and working tree isolation**

Run:

```powershell
git show --check --stat HEAD
git status --short
```

Expected: the README commit has no whitespace errors; no user-owned application files are staged or committed by this documentation task.
