# MEMS Blazor administration

The .NET 10 Interactive Server application hosts the shared administrative
experience at `/admin`. It uses the MEMS SQL Server database and Identity
accounts. The API-only iOS backend has its own SQLite/JWT configuration and
lives separately under `COMP4977-MunicipalElections-iOS`.

## Run locally

For the assignment demo, start `MEMS.AppHost` with its `https` or `http`
development launch profile. It automatically initializes the database and gives
`Super` (also `super`), `pm`, `Burnabyadmin`, `Surreyadmin`, and `Richmondadmin`
the password `P@$$w0rd`. Existing seeded accounts receive that same password
on startup, and their roles/municipality assignments stay intact. No password
setup commands are required. Sign in at `https://localhost:7052/admin/login`.

For a standalone Admin host instead of Aspire:

From `COMP4976-MunicipalElections-CSharp`:

```powershell
dotnet user-secrets set --project MEMS.Admin "ConnectionStrings:MEMS" "<your MEMS SQL Server connection string>"
dotnet run --project MEMS.Admin --launch-profile https
```

Open `https://localhost:7052/admin`. Alternatively supply
`ConnectionStrings__MEMS` through secure environment configuration. The Admin
host has its own user-secrets store; the Data project's design-time secrets are
not automatically read by this host. Connection strings use external
configuration; the shared assignment password applies only to development seeding.

Apply the existing migration and provision development accounts using
[`MEMS.Data/README.md`](../MEMS.Data/README.md) before signing in. The Admin app
does not create, migrate, or seed the database on startup. Use the same new MEMS
database as `MEMS.Api`, never the original SQLite database.

For the completed local orchestration, use the
[solution setup](../docs/README.md). AppHost supplies
the SQL connection, waits for the API's development migration startup, and sets
the API image origin to this Admin host automatically.

## Pages and behavior

| Route | Function |
| --- | --- |
| `/admin` | Counts for the current administrator's permitted municipalities |
| `/admin/login` | Username/email and password sign-in |
| `/admin/municipalities` | Municipality list |
| `/admin/positions` | Position list |
| `/admin/candidates` | Candidate list, name search, municipality/position filters |
| `/admin/{resource}/create` | Create form |
| `/admin/{resource}/{id}` | Details |
| `/admin/{resource}/{id}/edit` | Edit form |
| `/admin/{resource}/{id}/delete` | Details and explicit deletion confirmation |
| `/admin/access-denied`, `/admin/error`, `/admin/not-found` | Safe error states |

Municipality writes require Super Admin. Municipality Administrators can view
their municipality and manage its positions and candidates. Positions retain
their original municipality when edited. Super Admin can reassign a candidate
to another municipality only with a position belonging to that destination;
municipality administrators cannot transfer candidates. Required fields, seat
counts, lengths, email/HTTP(S) URL syntax, unique slugs, related records, and
revision concurrency are checked on the server as well as in the forms.

## Authentication and data access

Sign-in/out use antiforgery-protected HTTP POSTs at
`/admin/account/sign-in` and `/admin/account/sign-out`. The UI has a separate
secure, HttpOnly, same-site `__Host-MEMS.AdminUI` cookie and a separate data
protection application name. It shares the Identity store and role/municipality
resolver; it does not forward browser cookies to `MEMS.Api` or use
the legacy JWTs. Sessions have an absolute 30-minute lifetime, no persistent
cookie, login throttling, and Identity lockout. Logout rotates the user's
security stamp, invalidating that user's other MEMS sessions too.

`AdminDataService` is the server-side integration boundary. Each operation
opens a fresh DI scope/EF context, revalidates the current security stamp,
absolute expiry, administrator role and municipality assignment, configures
`TenantQueryContext`, and calls the shared `ElectionRepository`. Components do
not issue database queries or construct privileged scopes. EF query filters and
write guards enforce the tenant boundary independently of the visible controls.
Circuit authentication is also revalidated every 30 seconds. A forged record
ID, destination municipality, or position is checked against stored ownership.

Accounts enrolled in two-factor
authentication are denied rather than bypassing their second factor; a 2FA
enrollment/challenge UI is not implemented.

## Uploaded images

The optional `Uploads:RootPath` setting defaults to `App_Data/uploads` beneath
the Admin content root. It may instead point to a dedicated external directory
with write permission for the host. Application source, `wwwroot`, and ancestor
directories are rejected as upload roots. Keep durable storage outside the
deployment directory when publishing; share that volume across replicas if
needed. Persist data-protection keys appropriately for the hosting environment.

Images accept matching PNG/JPEG/WebP extension, MIME type and decoded content;
maximum input size is 2 MB, 16 megapixels and 8000 pixels on either side. The
server counts actual bytes, fully decodes and re-encodes pixels, drops source
metadata, generates filenames, and cleans up failed/replaced/unreferenced files.
Original client filenames never determine storage paths. Cross-tenant image
references are checked before cleanup. Public election images are served at
`/uploads/logos/{generated-name}` and `/uploads/candidates/{generated-name}` with
`nosniff`. Keep backups of the upload directory together with database backups.

The API's `Images:BaseUrl` points at the Admin HTTPS origin when orchestrated by
AppHost, so public API DTOs resolve these image paths. Configure it explicitly
for other hosting arrangements. Existing Assignment 1 image files are not copied
or modified. The public UI uses the API's image URLs.

## Build

From the C# course folder, run `dotnet build MEMS.Admin/MEMS.Admin.csproj` or
`dotnet build MEMS.sln`. The former Admin/Data/API test projects are not present
in the current solution. See the [solution guide](../docs/README.md) for the
scope of previous verification and current limitations.
