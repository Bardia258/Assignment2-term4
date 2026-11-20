# MEMS C# codebase

COMP4976 Assignment 2 is a .NET 10 municipal elections application with a shared
Blazor Admin site, a municipality-based public Blazor WebAssembly site, a REST
API and SQL Server. It runs locally through Aspire; Azure deployment is not
required for this C# solution.

| Reference | Contents |
| --- | --- |
| [Architecture](architecture/ARCHITECTURE.md) | Seven projects, request flow, tenant isolation and Aspire. |
| [ERD](erd/ERD.md) | Core SQL Server entities, Identity assignments and integrity rules. |
| [API](api/API-ENDPOINTS.md) | Routes, filters, authentication, JSON examples and errors. |
| [Data project](../MEMS.Data/README.md) | Standalone SQL configuration, migrations and seeding. |
| [Admin project](../MEMS.Admin/README.md) | Admin routes, server authorization and image storage. |

## Run locally

Install the .NET 10 SDK and Docker Desktop. Start Docker using Linux containers.
Docker runs SQL Server for this Aspire configuration; the .NET applications run
on the host. The assignment requires SQL Server/Aspire, rather than Docker by
name. Using an existing SQL Server instead would require different AppHost
configuration; it is not the default setup here.

In Visual Studio with .NET 10/Aspire support, open `MEMS.sln`, set `MEMS.AppHost`
as the startup project and run the `https` profile. Or from this course folder:

```powershell
dotnet restore MEMS.sln
dotnet build MEMS.sln
dotnet run --project MEMS.AppHost --launch-profile https
```

If HTTPS is not trusted yet, run `dotnet dev-certs https --trust` once. Allow
the initial SQL Server image download/startup to finish. The API applies
migrations and seeds sample data before Admin and Public become ready.
No manual password setup commands are required for this development profile.

| Application | Default local address |
| --- | --- |
| Admin | `https://localhost:7052/admin` |
| Admin login | `https://localhost:7052/admin/login` |
| Public | `https://localhost:7158` |
| Tenant sites | `https://localhost:7158/burnaby`, `/surrey`, `/richmond` |
| API | `https://localhost:7249` |
| OpenAPI JSON | `https://localhost:7249/openapi/v1.json` |
| Aspire dashboard | `https://localhost:17275` (use the login link printed at startup) |

Use the resource links in Aspire if endpoints change. Also update
`MEMS.Public/wwwroot/appsettings.json` when changing the API URL; browser
WebAssembly cannot read Aspire's server environment variables.

## Development accounts and sample data

All five seeded local accounts use **`P@$$w0rd`**.

| Username | Role / scope |
| --- | --- |
| `Super` (also `super`) | Super Admin, all municipalities. |
| `Burnabyadmin` | Municipality Administrator, Burnaby. |
| `Surreyadmin` | Municipality Administrator, Surrey. |
| `Richmondadmin` | Municipality Administrator, Richmond. |
| `pm` | Municipality Administrator, Pitt Meadows. |

These passwords belong to the Development-only assignment seed mode.
Restarting AppHost restores them for recognized seeded accounts and clears
their login lockouts. It preserves existing role/municipality assignments and
edited election records. This does not change the separate iOS SQLite users.

Creating a municipality as Super Admin also creates an Identity administrator
assigned only to it. The username is the initial slug plus `admin`: a `test`
slug creates `testadmin` (login is case-insensitive). Its local Development
password is `P@$$w0rd`. The municipality details page shows the actual username;
later name/slug edits do not rename the account or reset its password.

A fresh seed creates Burnaby, Surrey, Richmond, Pitt Meadows and Maple Ridge;
15 positions; 32 fictional candidates; two Identity roles; and five admins.
Re-running the seed adds missing records without duplicating the dataset.
SQL data survives a normal stop/restart in Docker's data volume. Admin image
files are stored separately, under `MEMS.Admin/App_Data/uploads` by default.

## Main behavior

- One protected `/admin` site provides dashboard counts, municipality/position/
  candidate lists, details, forms, delete confirmation, logo and photo uploads.
- Super Admin manages all municipalities. Municipal admins manage only their
  own positions/candidates and see counts for their assigned municipality.
- Public routes resolve municipality slugs, load the real API directory,
  search/filter candidates, display details and support a printable voting guide.
- Voting selections stay in browser `sessionStorage`, separated by municipality;
  they are not sent to SQL Server, Firebase or an API write endpoint.
- Repository checks, EF query filters/write guards and database relationships
  protect tenant boundaries in addition to UI controls.

## Maintenance notes

The original SQLite/JWT backend now lives under
[`COMP4977-MunicipalElections-iOS`](../../COMP4977-MunicipalElections-iOS/docs/README.md)
and serves only the iOS application. It is not part of `MEMS.sln`.

The seven-project solution previously restored and built in Release with zero
warnings/errors. Earlier local SQL/Aspire and public integration checks were
performed, but the removed test projects are not a runnable current test suite.
No build or runtime checks were repeated for this documentation-only update.

The SQL schema still includes an unused legacy `SavedCandidates` mapping; the
public voting guide never uses it. Instructor feedback from Assignment 1 is
not available in the workspace, so no instructor-specific resolution claims
are made here.

Developed by **Bardia Bashiri** (backend, database and Admin) and
**Omar Mohamad** (public C# website and SwiftUI frontend).
