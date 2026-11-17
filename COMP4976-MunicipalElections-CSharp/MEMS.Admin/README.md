# MEMS Blazor administration

The .NET 10 Interactive Server application hosts the shared administrative
experience at `/admin`. It uses the Task 1 SQL Server database and Identity
accounts. Assignment 1's MVC/SQLite/JWT application and the iOS application
remain separate and unchanged.

## Run locally

From `COMP4976-MunicipalElections-CSharp`:

```powershell
dotnet user-secrets set --project MEMS.Admin "ConnectionStrings:MEMS" "<your MEMS SQL Server connection string>"
dotnet run --project MEMS.Admin --launch-profile https
```

Open `https://localhost:7052/admin`. Alternatively supply
`ConnectionStrings__MEMS` through secure environment configuration. The Admin
host has its own user-secrets store; the Data project's design-time secrets are
not automatically read by this host. No connection strings or initial passwords
are embedded in source.

Apply the existing migration and provision development accounts using
[`MEMS.Data/README.md`](../MEMS.Data/README.md) before signing in. The Admin app
does not create, migrate, or seed the database on startup. Use the same new MEMS
database as `MEMS.Api`, never the original SQLite database.

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
resolver from Task 2; it does not forward browser cookies to `MEMS.Api` or use
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

The password-based flow follows Task 2. Accounts enrolled in two-factor
authentication are denied rather than bypassing their second factor; a 2FA
enrollment/challenge UI is not included in this milestone.

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

The API's existing `Images:BaseUrl` can later point at the externally
reachable Admin origin so public API DTOs resolve these same image paths. The
Aspire/public hosting configuration is left for Task 4. Existing Assignment 1
image files are not copied or modified.

## Verification

```powershell
dotnet build MEMS.Admin
dotnet build MEMS.Admin.Tests
pwsh MEMS.Admin.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test MEMS.Admin.Tests
```

Tests use real ASP.NET Identity/cookies/antiforgery and EF's InMemory provider;
they do not substitute an always-authorized test authentication scheme. The
browser test runs real Chromium against a temporary HTTPS Kestrel host with a
test-only certificate. It checks CRUD, uploads, validation, destructive-action
confirmation, tenant access, and desktop/mobile rendering. Browser screenshots
are written under the test output's ignored `TestResults/browser` directory.
For service/HTTP tests without a browser, use
`dotnet test MEMS.Admin.Tests --filter 'FullyQualifiedName!~BrowserTests'`.

InMemory tests verify application behavior, not SQL Server transactions or SQL
constraints. The existing SQL Server integration check in `MEMS.Data.Tests`
requires an explicitly configured disposable database as described in its
README. A live SQL Server/Azure deployment and the final Aspire integration
must be verified separately; this milestone does not change the AppHost.
