# Task 1 Report — เตรียมเครื่องมือและสร้าง solution skeleton

## สถานะ

เสร็จสมบูรณ์: สร้าง .NET 10 solution, reference graph, Angular 18 standalone app และ common build settings แล้ว

## คำสั่งทดสอบและผลลัพธ์

| คำสั่ง | ผลลัพธ์ |
| --- | --- |
| `dotnet --list-sdks` | พบ SDK 8.0.423 และ 10.0.302 |
| `node --version` | `v24.18.0` |
| `dotnet build backend\\StockPulse.sln --configuration Release` | ผ่าน: 0 warnings, 0 errors |
| `dotnet test backend\\StockPulse.sln --configuration Release --no-build` | ผ่าน: 3 projects, 3 tests ผ่าน |
| `npm.cmd run build --prefix frontend` | ผ่าน: Angular build สร้าง `frontend/dist/frontend` |

## Files ที่สร้างหรือเปลี่ยน

- `.gitignore` — ignore build, IDE, Node และ test artifacts ระดับ repository
- `backend/StockPulse.sln` และ `backend/Directory.Build.props` — .NET 10 solution และ strict common settings
- `backend/src/` — Api, Application, Contracts, Domain, Infrastructure, Worker พร้อม project references ตาม brief
- `backend/tests/` — Application, Infrastructure และ Worker xUnit projects
- `backend/src/StockPulse.Api/StockPulse.Api.csproj` — pin `Microsoft.OpenApi` 2.7.5 เพื่อแทน dependency 2.0.0 ที่มีช่องโหว่ระดับสูงและทำให้ restore ล้มเหลวเมื่อ treat warnings as errors
- `backend/src/StockPulse.Worker/Worker.cs` — ใช้ source-generated `LoggerMessage` แทน template logging เพื่อผ่าน analyzers ที่ตั้งเป็น errors
- `frontend/` — Angular CLI 18 standalone, strict, routing, SCSS พร้อม `@angular/cdk` 18 และ `@microsoft/signalr`

## Self-review

- Performance: ใช้ `LoggerMessage` เพื่อลด allocation จาก structured log template ใน worker
- Security: dependency OpenAPI ที่ vulnerable ถูกแทนด้วย 2.7.5; ไม่มี secrets หรือ config เฉพาะเครื่องถูกเพิ่ม
- Naming: project names และ reference directions ตรงตาม brief; log placeholder เป็น `Time` ตาม .NET convention
- Extensibility: สร้าง layer และ test projects แยกชัดเจน; shared compiler settings อยู่ที่ `Directory.Build.props`
- ตรวจ solution แล้วมีครบ 9 projects และ `git diff --check` ไม่พบ whitespace error

## Concerns

- `npx @angular/cli@18 new` แสดง Node deprecation warning (`DEP0190`) ระหว่างสร้าง แต่ Angular 18 build ผ่านบน Node 24.18.0 แล้ว จึงไม่เป็น blocker สำหรับ skeleton นี้
- คำสั่ง scaffold รวม timeout หลัง 122 วินาที แต่ Angular app และ dependencies ถูกสร้างครบ; รัน build ภายหลังผ่าน
