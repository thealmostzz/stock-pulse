# StockPulse

> **MVP status / สถานะ MVP:** The backend local prototype is available. A production dashboard and external news-provider ingestion are planned work.
>
> **สถานะ MVP:** มี backend prototype สำหรับใช้งานบนเครื่องแล้ว ส่วน dashboard ที่พร้อมใช้จริงและการดึงข่าวจากผู้ให้บริการภายนอกยังอยู่ในแผนพัฒนา

## Overview / ภาพรวม

**English**

StockPulse is a real-time stock-news monitoring application for traders and investors. Its long-term goal is to collect market news, link it to relevant tickers, assess potential impact, and surface timely updates in a focused dashboard.

The repository currently contains the foundation of that product: a local PostgreSQL database, an ASP.NET Core API for a global watchlist and persisted news queries, a SignalR hub for realtime delivery, and an Angular frontend scaffold.

**ไทย**

StockPulse คือแอปพลิเคชันสำหรับติดตามข่าวหุ้นแบบเรียลไทม์สำหรับนักลงทุนและเทรดเดอร์ เป้าหมายระยะยาวคือรวบรวมข่าวตลาด เชื่อมโยงข่าวกับ ticker ที่เกี่ยวข้อง ประเมินผลกระทบที่อาจเกิดขึ้น และแสดงข้อมูลที่ทันเวลาใน dashboard ที่ใช้งานง่าย

ปัจจุบัน repository นี้มีรากฐานของผลิตภัณฑ์แล้ว ได้แก่ PostgreSQL สำหรับใช้งานบนเครื่อง, ASP.NET Core API สำหรับ watchlist กลางและการค้นหาข่าวที่บันทึกไว้, SignalR hub สำหรับการส่งข้อมูลแบบเรียลไทม์ และ Angular frontend scaffold

## MVP status / สถานะ MVP

**English**

This is an early local-first prototype, not a production-ready trading platform. The API, persistence layer, tests, and realtime hub are in place. The Angular application is still the default scaffold, and the Worker currently runs as a placeholder service. No external news provider is connected yet.

**ไทย**

นี่คือ prototype ระยะแรกที่เน้นการรันบนเครื่อง ไม่ใช่แพลตฟอร์มสำหรับเทรดที่พร้อมใช้งานใน production ปัจจุบันมี API, persistence layer, tests และ realtime hub แล้ว แต่ Angular application ยังเป็น scaffold เริ่มต้น ส่วน Worker ยังเป็นบริการ placeholder และยังไม่ได้เชื่อมต่อผู้ให้บริการข่าวภายนอก

## Current capabilities / ความสามารถปัจจุบัน

**English**

- Create, list, and remove items from one global watchlist.
- Persist stock-news records and query them with pagination and filters.
- Retrieve the most recent persisted news items.
- Broadcast newly created news through SignalR, including per-ticker subscriptions.
- Run PostgreSQL locally with Docker Compose and run backend tests.

**ไทย**

- เพิ่ม ดูรายการ และลบรายการจาก global watchlist เดียวได้
- บันทึกข้อมูลข่าวหุ้น และค้นหาพร้อมแบ่งหน้าและกรองข้อมูลได้
- ดึงรายการข่าวที่บันทึกล่าสุดได้
- ส่งข่าวที่สร้างใหม่ผ่าน SignalR รวมถึงการ subscribe ตาม ticker
- รัน PostgreSQL บนเครื่องด้วย Docker Compose และรัน backend tests ได้

## Roadmap / แผนงาน

**English**

The following are product goals and are **not implemented yet**:

- A finished Angular trading-focused dashboard.
- Scheduled ingestion from external financial-news providers.
- News normalization, deduplication, sentiment and market-impact scoring.
- Alert-rule evaluation, end-user notifications, authentication, multi-user access, and production deployment.

**ไทย**

รายการต่อไปนี้เป็นเป้าหมายของผลิตภัณฑ์และ **ยังไม่ได้พัฒนา**:

- Angular dashboard สำหรับการติดตามข่าวหุ้นที่เสร็จสมบูรณ์
- การดึงข่าวตามกำหนดเวลาจากผู้ให้บริการข่าวการเงินภายนอก
- การปรับรูปแบบข่าว การกำจัดข่าวซ้ำ การประเมิน sentiment และคะแนนผลกระทบต่อตลาด
- การประเมินกฎแจ้งเตือน การแจ้งเตือนผู้ใช้ การยืนยันตัวตน การรองรับผู้ใช้หลายคน และการ deploy production

## Architecture / สถาปัตยกรรม

**English**

The current local architecture separates browser access, realtime delivery, persistence, and background processing:

```text
Angular frontend (http://localhost:4200)
             │ REST + SignalR
             ▼
ASP.NET Core API (http://localhost:5179)
   ├── REST endpoints: news and watchlist
   ├── SignalR hub: /hubs/news
   └── EF Core / Npgsql
             │
             ▼
PostgreSQL 16 (localhost:5432)

.NET Worker ──► Placeholder background service
External news providers ──► Planned; not connected
```

**ไทย**

สถาปัตยกรรมสำหรับใช้งานบนเครื่องแยกหน้าที่ของ browser, การส่งข้อมูลแบบเรียลไทม์, persistence และการประมวลผลเบื้องหลังออกจากกัน โดย Worker ยังเป็น placeholder และผู้ให้บริการข่าวภายนอกยังไม่ได้เชื่อมต่อ

## Technology stack / เทคโนโลยีที่ใช้

| Area | Technology |
| --- | --- |
| Frontend | Angular 22, TypeScript 6, SCSS |
| API | ASP.NET Core Web API on .NET 10 |
| Realtime | ASP.NET Core SignalR |
| Data access | Entity Framework Core and Npgsql |
| Database | PostgreSQL 16 |
| Local infrastructure | Docker Compose |
| Tests | xUnit, Angular Karma/Jasmine setup |

**ไทย**

Frontend ใช้ Angular 22 และ TypeScript 6 ส่วน backend ใช้ ASP.NET Core บน .NET 10, PostgreSQL 16 เป็นฐานข้อมูลหลักสำหรับ local development และ SignalR สำหรับ realtime updates

## Prerequisites / สิ่งที่ต้องเตรียม

**English**

Install [.NET SDK 10](https://dotnet.microsoft.com/download), [Node.js](https://nodejs.org/) with npm, and [Docker Desktop](https://www.docker.com/products/docker-desktop/) with Docker Compose. The examples below use PowerShell on Windows.

**ไทย**

ติดตั้ง [.NET SDK 10](https://dotnet.microsoft.com/download), [Node.js](https://nodejs.org/) พร้อม npm และ [Docker Desktop](https://www.docker.com/products/docker-desktop/) พร้อม Docker Compose โดยตัวอย่างคำสั่งด้านล่างใช้ PowerShell บน Windows

## Quick start / เริ่มต้นใช้งานอย่างรวดเร็ว

### 1. Start PostgreSQL / เริ่ม PostgreSQL

**English**

From the repository root, start the local database. The Compose file also creates the `stockpulse_test` database for integration tests.

**ไทย**

จาก root ของ repository ให้เริ่มฐานข้อมูลบนเครื่อง โดย Compose file จะสร้างฐานข้อมูล `stockpulse_test` สำหรับ integration tests ด้วย

```powershell
docker compose -f docker/docker-compose.yml up -d
docker compose -f docker/docker-compose.yml ps
```

> The database password in the Compose file is for local development only. Do not reuse it outside this repository.
>
> รหัสผ่านฐานข้อมูลใน Compose file ใช้สำหรับ local development เท่านั้น ห้ามนำไปใช้กับระบบอื่น

### 2. Start the API / เริ่ม API

**English**

Set the connection string for the current PowerShell session, then run the API. This environment variable is not persisted and avoids placing credentials in source-controlled configuration.

**ไทย**

กำหนด connection string สำหรับ PowerShell session ปัจจุบัน แล้วจึงรัน API ตัวแปร environment นี้จะไม่ถูกบันทึกถาวร จึงช่วยหลีกเลี่ยงการเก็บ credentials ไว้ใน configuration ที่อยู่ใน source control

```powershell
$env:ConnectionStrings__StockPulse = 'Host=localhost;Port=5432;Database=stockpulse;Username=stockpulse;Password=stockpulse_local_only'
dotnet run --project backend/src/StockPulse.Api
```

The development profile listens on [http://localhost:5179](http://localhost:5179). Swagger UI and OpenAPI are available only when `ASPNETCORE_ENVIRONMENT=Development`.

Development profile จะรับคำขอที่ [http://localhost:5179](http://localhost:5179) และ Swagger UI กับ OpenAPI จะเปิดใช้เฉพาะเมื่อ `ASPNETCORE_ENVIRONMENT=Development`

### 3. Start the frontend / เริ่ม frontend

**English**

Open a second PowerShell window and run:

**ไทย**

เปิด PowerShell อีกหน้าต่าง แล้วรันคำสั่งต่อไปนี้:

```powershell
Set-Location frontend
npm ci
npm start
```

Then open [http://localhost:4200](http://localhost:4200). The current Angular application is a scaffold; it does not yet render the StockPulse dashboard.

จากนั้นเปิด [http://localhost:4200](http://localhost:4200) ปัจจุบัน Angular application ยังเป็น scaffold และยังไม่ได้แสดง StockPulse dashboard

### 4. Stop local services / หยุดบริการบนเครื่อง

```powershell
docker compose -f docker/docker-compose.yml down
```

Use `down -v` only when you intentionally want to delete local PostgreSQL data.

ใช้ `down -v` เฉพาะเมื่อคุณต้องการลบข้อมูล PostgreSQL บนเครื่องโดยเจตนา

## API reference / เอกสารอ้างอิง API

**English**

The API base URL in the Development profile is `http://localhost:5179`. The following endpoints are implemented.

**ไทย**

API base URL ของ Development profile คือ `http://localhost:5179` โดย endpoint ต่อไปนี้พัฒนาแล้ว

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/news` | Query persisted news with pagination and optional filters |
| `GET` | `/api/news/latest?limit=20` | Get the newest persisted news records |
| `GET` | `/api/watchlist` | Get the global watchlist |
| `POST` | `/api/watchlist` | Add a ticker to the global watchlist |
| `DELETE` | `/api/watchlist/{ticker}` | Remove a ticker from the global watchlist |

### Query latest news / ค้นหาข่าวล่าสุด

```powershell
Invoke-RestMethod 'http://localhost:5179/api/news/latest?limit=20'
```

The `GET /api/news` endpoint supports `ticker`, `sourceCode`, `sentiment`, `tag`, `minImpactScore`, `publishedFromUtc`, `publishedToUtc`, `page`, and `pageSize` query parameters.

Endpoint `GET /api/news` รองรับ query parameter ได้แก่ `ticker`, `sourceCode`, `sentiment`, `tag`, `minImpactScore`, `publishedFromUtc`, `publishedToUtc`, `page` และ `pageSize`

### Add a ticker / เพิ่ม ticker

```powershell
$body = @{ ticker = 'NVDA'; displayName = 'NVIDIA Corporation'; market = 'NASDAQ' } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri 'http://localhost:5179/api/watchlist' -ContentType 'application/json' -Body $body
```

### Remove a ticker / ลบ ticker

```powershell
Invoke-RestMethod -Method Delete -Uri 'http://localhost:5179/api/watchlist/NVDA'
```

**English**

The API normalizes tickers, returns `400 Bad Request` for invalid values, `409 Conflict` for a duplicate ticker, and `404 Not Found` when deleting a ticker that is absent.

**ไทย**

API จะปรับรูปแบบ ticker ให้เป็นมาตรฐาน โดยคืนค่า `400 Bad Request` เมื่อข้อมูลไม่ถูกต้อง, `409 Conflict` เมื่อ ticker ซ้ำ และ `404 Not Found` เมื่อลบ ticker ที่ไม่มีอยู่

## Realtime updates / การอัปเดตแบบเรียลไทม์

**English**

The SignalR hub is available at `/hubs/news`, for example `http://localhost:5179/hubs/news`. A client can call `SubscribeTicker(ticker)` and `UnsubscribeTicker(ticker)` to join or leave a ticker-specific group. When the backend publishes a newly created news item, subscribed clients receive the `news:new` event.

There is not yet an end-to-end ingestion pipeline or frontend client implementation that produces and displays these events.

**ไทย**

SignalR hub อยู่ที่ `/hubs/news` เช่น `http://localhost:5179/hubs/news` client สามารถเรียก `SubscribeTicker(ticker)` และ `UnsubscribeTicker(ticker)` เพื่อเข้าหรือออกจากกลุ่มเฉพาะ ticker ได้ เมื่อ backend publish ข่าวที่สร้างใหม่ client ที่ subscribe จะได้รับ event `news:new`

ขณะนี้ยังไม่มี ingestion pipeline แบบครบวงจร หรือ frontend client ที่สร้างและแสดง event เหล่านี้

## Testing / การทดสอบ

**English**

Run backend tests from the repository root after PostgreSQL is available. Set the test connection string for the current PowerShell session first:

**ไทย**

หลังจาก PostgreSQL พร้อมใช้งาน ให้กำหนด test connection string สำหรับ PowerShell session ปัจจุบัน แล้วรัน backend tests จาก root ของ repository:

```powershell
$env:STOCKPULSE_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=stockpulse_test;Username=stockpulse;Password=stockpulse_local_only'
dotnet test backend/StockPulse.sln
```

Run frontend tests or build the frontend from the frontend directory:

```powershell
Set-Location frontend
npm test
npm run build
```

รัน frontend tests หรือ build frontend จากโฟลเดอร์ frontend ด้วยคำสั่งข้างต้น

## Project structure / โครงสร้างโปรเจกต์

```text
stock-pulse/
├── backend/
│   ├── src/
│   │   ├── StockPulse.Api/            # REST API, SignalR hub, realtime publisher
│   │   ├── StockPulse.Application/    # Use cases, DTOs, repository abstractions
│   │   ├── StockPulse.Contracts/      # Shared contracts
│   │   ├── StockPulse.Domain/         # Entities and enums
│   │   ├── StockPulse.Infrastructure/ # EF Core, PostgreSQL, repositories, migrations
│   │   └── StockPulse.Worker/         # Placeholder hosted background service
│   └── tests/                         # Application, infrastructure, and worker tests
├── docker/                            # Local PostgreSQL and initialization scripts
├── docs/superpowers/                  # Design and implementation records
├── frontend/                          # Angular application
└── README.md
```

**English**

Each backend project has a focused responsibility, keeping HTTP delivery, application use cases, domain concepts, infrastructure, and background work separate.

**ไทย**

แต่ละ backend project มีความรับผิดชอบที่ชัดเจน โดยแยก HTTP delivery, application use cases, domain concepts, infrastructure และงานเบื้องหลังออกจากกัน

## Development notes / ข้อควรทราบสำหรับการพัฒนา

**English**

- The API CORS policy currently permits only `http://localhost:4200`.
- Migrations are included in the infrastructure project. Set `STOCKPULSE_CONNECTION` before using EF Core design-time commands.
- Keep connection strings, provider API keys, and production credentials out of source control.
- The Compose configuration is for local development; review passwords, network rules, backups, and TLS requirements before production use.
- The repository may contain work in progress. Stage and commit only files that belong to your change.

**ไทย**

- CORS policy ของ API อนุญาตเฉพาะ `http://localhost:4200` ในปัจจุบัน
- มี migrations อยู่ใน infrastructure project ให้กำหนด `STOCKPULSE_CONNECTION` ก่อนใช้คำสั่ง EF Core design-time
- ห้ามเก็บ connection strings, provider API keys และ production credentials ไว้ใน source control
- Compose configuration มีไว้สำหรับ local development ควรทบทวนรหัสผ่าน network rules backups และ TLS ก่อนนำไปใช้ใน production
- Repository อาจมีงานที่กำลังพัฒนาอยู่ ให้ stage และ commit เฉพาะไฟล์ที่เป็นส่วนหนึ่งของงานคุณ

## Disclaimer / ข้อจำกัดความรับผิดชอบ

**English**

StockPulse is software under development and does not provide investment, financial, legal, or tax advice. Market data and news can be delayed, incomplete, or inaccurate. Make investment decisions only after independent research and consultation with qualified professionals.

**ไทย**

StockPulse เป็นซอฟต์แวร์ที่อยู่ระหว่างการพัฒนา และไม่ได้ให้คำแนะนำด้านการลงทุน การเงิน กฎหมาย หรือภาษี ข้อมูลตลาดและข่าวอาจล่าช้า ไม่ครบถ้วน หรือไม่ถูกต้อง ควรตัดสินใจลงทุนจากการศึกษาด้วยตนเองและปรึกษาผู้เชี่ยวชาญที่มีคุณสมบัติเหมาะสม

## Contributing / การมีส่วนร่วม

**English**

Contributions are welcome. Please open an issue or discussion before starting a substantial feature, keep changes focused, add or update relevant tests, and avoid committing secrets or generated artifacts.

**ไทย**

ยินดีรับการมีส่วนร่วม กรุณาเปิด issue หรือ discussion ก่อนเริ่มฟีเจอร์ขนาดใหญ่ ทำการเปลี่ยนแปลงให้มีขอบเขตชัดเจน เพิ่มหรือปรับปรุง tests ที่เกี่ยวข้อง และหลีกเลี่ยงการ commit secrets หรือ generated artifacts
