# Task 1 Report: Provider Configuration and Scheduling

## Scope delivered

- Added `WorkerOptions` with mock mode as the default, a 15-second mock interval, a 15-minute Finnhub interval, and Finnhub API-key validation.
- Added `FinnhubOptions` with an `ApiKey` binding target.
- Bound `Worker` and `Finnhub` configuration in Worker startup, validated the selected provider before host construction, and registered only the selected provider.
- Added the named `Finnhub` HTTP client with base address `https://finnhub.io/api/v1/`.
- Updated the ingestion hosted service to obtain its timer interval from `IOptions<WorkerOptions>`.
- Added configuration behavior tests covering both polling intervals and the missing-key error contract.

## Changed files

- `backend/src/StockPulse.Worker/Configuration/WorkerOptions.cs` (new)
- `backend/src/StockPulse.Worker/Configuration/FinnhubOptions.cs` (new)
- `backend/src/StockPulse.Worker/Program.cs`
- `backend/src/StockPulse.Worker/HostedServices/NewsIngestionHostedService.cs`
- `backend/src/StockPulse.Worker/Providers/Finnhub/FinnhubNewsClient.cs` (new temporary registration stub)
- `backend/tests/StockPulse.Worker.Tests/WorkerConfigurationTests.cs` (new)

## TDD evidence

### RED

Exact command:

```powershell
dotnet test backend/tests/StockPulse.Worker.Tests/StockPulse.Worker.Tests.csproj --configuration Release --filter FullyQualifiedName~WorkerConfigurationTests
```

Outcome: failed during test-project compilation with `CS0234`, because `StockPulse.Worker.Configuration` and the requested option types did not yet exist. This is the intended failure before production implementation.

### GREEN

Exact command:

```powershell
dotnet test backend/tests/StockPulse.Worker.Tests/StockPulse.Worker.Tests.csproj --configuration Release --filter FullyQualifiedName~WorkerConfigurationTests
```

Outcome: passed — Failed: 0, Passed: 3, Skipped: 0, Total: 3.

## Additional verification

- `dotnet build backend/StockPulse.sln --configuration Release`: passed with 0 warnings and 0 errors.
- `dotnet test backend/StockPulse.sln --configuration Release`: could not complete integration tests because `STOCKPULSE_TEST_CONNECTION` is not configured. The unrelated integration suites reported 12 failures total from that missing required environment variable; 28 non-integration tests passed.
- `npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless` and `npm.cmd run build --prefix frontend`: could not run because the local frontend dependencies are absent (`ng` is not recognized).
- `git diff --check` completed with no whitespace errors before the implementation commit.

## Commit

Implementation commit: `7ddc85cab54cfeebae92947db06fb2c335087e91` (`feat: configure Finnhub news provider`)

## Self-review

| Pillar | Review |
| --- | --- |
| Performance | The timer interval is computed once when the hosted service starts. Provider registration remains conditional, so mock and Finnhub clients are not allocated together. |
| Security | No API key is committed or logged. Finnhub mode fails before host construction if the API key is null, empty, or whitespace. |
| Naming | `WorkerOptions`, `FinnhubOptions`, `UseMockProviders`, and `GetPollingInterval` are explicit and follow existing .NET naming conventions. |
| Extensibility | Provider selection and its scheduling policy are isolated in options. The named HTTP client and scoped Finnhub registration leave a narrow replacement point for the real client implementation. |

## Concerns

- `FinnhubNewsClient` is an intentional, minimal stub needed for the required Finnhub DI registration to compile; it throws `NotImplementedException` if Finnhub mode is run before the subsequent client-ingestion task replaces it.
- Full backend integration verification is blocked by the absent `STOCKPULSE_TEST_CONNECTION`; frontend verification is blocked by absent local Node dependencies.
