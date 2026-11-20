# MEMS C# project instructions

Help maintain the COMP4976 Municipal Elections Management System, a .NET 10
multi-tenant application. Read the relevant existing implementation and
[project documentation](../docs/README.md) before changing behavior. Prefer
small, clear changes appropriate for a course project.

## Project boundaries

| Project | Responsibility |
| --- | --- |
| `MEMS.AppHost` | Local Aspire orchestration for SQL Server, API, Admin and Public. |
| `MEMS.ServiceDefaults` | Server logging, tracing, health checks and service discovery. |
| `MEMS.Admin` | Blazor Interactive Server administration at `/admin`, using Bootstrap. |
| `MEMS.Public` | Blazor WebAssembly public site with municipality slug routes. |
| `MEMS.Api` | REST endpoints, authentication, tenant authorization and safe errors. |
| `MEMS.Data` | SQL Server, EF Core Code First, Identity, repositories, migrations and seeding. |
| `MEMS.Shared` | Browser-safe DTOs, validation and shared constants. |

Admin uses `AdminDataService` and the shared repository directly on the server;
Public uses the API. Keep database entities and server dependencies out of Shared
and Public. The iOS SQLite/JWT backend in `../COMP4977-MunicipalElections-iOS`
is independent: do not change it or connect it to MEMS SQL Server for a C# task.

## Implementation conventions

- Match the surrounding C# and Razor style. Use async EF/HTTP operations and
  propagate cancellation tokens where supported.
- Reuse `ElectionRepository`, shared DTO validation and existing services.
  Keep business rules out of Razor components; avoid unnecessary abstractions,
  packages, schema changes or unrelated refactoring.
- Use fresh service scopes/DbContexts for Blazor operations. Preserve revision
  concurrency checks and handle stale edits with a clear user message.
- Preserve Bootstrap responsiveness, accessible form labels, validation,
  loading/error states and confirmation before destructive operations.
- Coordinate changes to the partner-owned Public app. Keep voting selections
  only in browser `sessionStorage`, separated by municipality. Never add ballot
  persistence to SQL Server, Firebase or an API endpoint.

## Authentication and tenant safety

- Super Admin can manage all municipalities; Municipality Administrators belong
  to exactly one. Only Super Admin may create, edit or delete municipalities.
- Derive permissions from current Identity assignments. Route/body municipality
  IDs select data; they do not grant access. Preserve repository ownership checks,
  EF tenant filters/write guards and same-municipality candidate/position constraints.
- Revalidate account status, role, municipality, security stamp and session expiry
  on sensitive Admin operations. Preserve separate Admin UI/API cookies, CSRF
  protection, lockout and HTTPS requirements; do not introduce iOS JWTs here.
- Keep upload size/type/decode/path validation and generated filenames. Return
  safe errors and use structured logs without credentials or sensitive bodies.
- Keep SQL credentials and custom passwords in user secrets/environment settings.
  Preserve the existing Development-only demo password mode. Automatic database
  initialization and demo seeding must remain disabled outside Development.

## Verification and scope

Run checks appropriate to the change. For application/dependency changes, use
`dotnet restore MEMS.sln`, `dotnet build MEMS.sln` and a Release build from the
C# folder. Run relevant tests when available; do not reference removed test
projects. Documentation-only edits need link/content checks rather than builds.

Report what changed, what was actually verified and any remaining limitations.
Distinguish source review from live SQL/Aspire, browser or deployment checks.
Do not invent testing evidence, instructor feedback or previous AI-tool usage.
Do not commit, push or deploy unless explicitly requested.
