# EDM 2.0 backend

ASP.NET Core on .NET 10, a modular monolith (ADR-0001). Each module has Domain, Application, Infrastructure and Api layers. No module reads another module's tables. PostgreSQL is the database (ADR-0004).

Specification: [docs/EDM2-Master-Prompt.md](../docs/EDM2-Master-Prompt.md). Phase 0 design: [docs/phase-0/design-note.md](../docs/phase-0/design-note.md).

## What is built (Phase 0 slice)

| Part | Where |
|---|---|
| Shared kernel: error catalogue (Part 21.3), permission catalogue (Part 20.1, 49 strings), paging, context | `src/Shared/Edm.SharedKernel` |
| Web adapters: deny-by-default authentication, `[RequiresPermission]`, ProblemDetails, correlation and actor context | `src/Shared/Edm.Web` |
| Host: pipeline, Serilog, health checks, OpenAPI (development), optional startup migration | `src/Host/Edm.Host` |
| Audit module: append-only store, `AuditWriter`, viewer API | `src/Modules/Audit/*` |
| Endpoints | `GET /api/v1/audit-events` (permission `audit.view`), `GET /health/live`, `GET /health/ready` (anonymous) |

Sign-in is **not** implemented yet. Authentication denies every request until the SSO provider is chosen (Q25). Tests use a test-only header scheme.

## Prerequisites

- .NET 10 SDK. `backend/global.json` pins 10.0.100 and allows later 10.0 feature bands.
- PostgreSQL 18 (any 14 or later should work; the CI uses 17).
- Docker is needed only if you run integration tests without `EDM_TEST_POSTGRES`.

## Check your machine (Windows, PowerShell)

`psql` is not on PATH on this machine, so use the full path to `psql.exe`.

```powershell
dotnet --list-sdks                      # expect a line starting 10.0.
cd backend
dotnet --version                        # expect 10.0.x (global.json pins the SDK)

# PostgreSQL connection check. psql asks for the password; do not paste it into chat.
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -p 5432 -U postgres -c "select version();"
```

If `dotnet --version` prints 9.x, the shell is picking up an older SDK. Run `dotnet --list-sdks` and check that a 10.0 SDK is installed and that `backend/global.json` is found from the folder you are in.

## Run the tests

Integration tests need a PostgreSQL server where the test account can create and drop databases and roles. Give the connection string through an environment variable for your PowerShell session only. Do not write the password into a file in the repository.

```powershell
cd backend
$env:EDM_TEST_POSTGRES = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=<your password>"
dotnet test EDM.sln -c Release
```

Each integration test class creates a database named `edm_test_<guid>` and drops it afterwards. If `EDM_TEST_POSTGRES` is not set, the tests start a PostgreSQL container with Testcontainers, which needs Docker.

## Run the API locally

```powershell
cd backend
$env:ConnectionStrings__Edm = "Host=localhost;Port=5432;Database=edm;Username=postgres;Password=<your password>"
$env:Database__MigrateOnStartup = "true"   # applies the audit schema on start; default is off
dotnet run --project src/Host/Edm.Host
```

Then open `http://localhost:5000/health/ready` (the port is shown in the console output). `GET /api/v1/audit-events` returns 401 until sign-in exists. That is the intended deny-by-default behaviour.

A one-command start that also starts PostgreSQL (`deploy/docker-compose.yml`) is planned and not yet written. It needs Docker, which has not been verified.

## Rules that apply to every change

- Every endpoint declares exactly one permission with `.RequiresPermission("...")`, or is an explicit anonymous health probe. A test fails the build otherwise.
- Every state change writes one audit event through `IAuditWriter`.
- Money is `numeric(18,2)` and rates are `numeric(18,4)`. Use `decimal`, never `float`.
- Timestamps are stored in UTC. The web app displays them in Asia/Karachi.
- Business rules live in C# (application or domain layers), never in stored procedures or triggers. The only trigger is the audit append-only guard, which is integrity, not business logic.
- The audit table is append-only. It has no UPDATE or DELETE path in the application, and the database rejects both even for the table owner.

## Layout

```
backend/
├── global.json                 SDK pin
├── Directory.Build.props       shared MSBuild settings (warnings as errors)
├── Directory.Packages.props    central NuGet versions
├── EDM.sln
├── src/
│   ├── Shared/Edm.SharedKernel
│   ├── Shared/Edm.Web
│   ├── Host/Edm.Host
│   └── Modules/Audit/{Domain, Application, Infrastructure, Api}
└── tests/
    ├── Edm.UnitTests
    ├── Edm.ArchitectureTests
    └── Edm.IntegrationTests
```

## CI

`.github/workflows/backend-ci.yml` runs restore, build (`-c Release`, warnings are errors), and the three test projects on every push that touches `backend/`. Test results are uploaded as an artefact.
