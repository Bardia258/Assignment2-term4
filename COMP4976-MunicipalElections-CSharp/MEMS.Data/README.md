# MEMS SQL Server data layer

Run the commands below from `COMP4976-MunicipalElections-CSharp` using the .NET 10
SDK. These commands target the new MEMS database only. The Assignment 1 SQLite
application, migrations, JWT configuration, and iOS app remain independent.

## Configure and migrate

```powershell
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet restore MEMS.sln
dotnet user-secrets set --project MEMS.Data "ConnectionStrings:MEMS" "<your SQL Server connection string>"
dotnet ef database update --project MEMS.Data --startup-project MEMS.Data
```

Alternatively set `ConnectionStrings__MEMS` securely in the environment. No
connection string fallback or credentials are embedded in the application.
Use a separate MEMS database and a trusted SQL Server certificate; configure
authentication appropriately for your local SQL Server or Azure SQL instance.

The design-time factory reads this project's user secrets, then environment
variables, then CLI configuration arguments. A runtime host must supply
its own configuration and `IHostEnvironment`, and register
`services.AddMemsData(configuration)` from `MEMS.Data.Configuration`. This adds
the SQL Server context, Identity user/role stores, `ElectionRepository`, and
`AdministratorScopeResolver`. It adds no login endpoints, cookies, or JWT scheme.

Generate a reviewable idempotent schema script without connecting to SQL Server:

```powershell
dotnet ef migrations script --idempotent --project MEMS.Data --startup-project MEMS.Data --output MEMS.Data/obj/InitialSqlServer.sql
dotnet ef migrations has-pending-model-changes --project MEMS.Data --startup-project MEMS.Data
```

SQL scripts contain schema changes only. They do not invoke the development
seeder. Applying a migration through `database update` or `Database.Migrate`
invokes EF's sync/async seeding hooks when explicitly enabled below. Disable
development seeding before rolling the database back to migration `0`.

## Seed development data

The AppHost development launch profiles enable seeding automatically with
`Mems:Seed:UseDefaultPasswords=true`. All five seeded administrators use
`P@$$w0rd`; existing seeded passwords are updated on startup. No password
configuration commands are needed for that assignment workflow. This mode is
rejected outside Development, and its seeder accepts the eight-character demo password.

For standalone tools and custom-password mode, seeding defaults to disabled.
Enabling it outside Development throws an error. Production passwords require
at least 12 characters; Development accepts the eight-character assignment
password. Both require uppercase, lowercase, a number and a non-alphanumeric
character. Migration files contain no password hashes.

Configure these five user-secret keys with separate development passwords using
`dotnet user-secrets set --project MEMS.Data "<key>" "<value>"`, or supply their
equivalent environment variables with `__` replacing `:`:

- `Mems:Seed:Passwords:Super`
- `Mems:Seed:Passwords:Burnabyadmin`
- `Mems:Seed:Passwords:Surreyadmin`
- `Mems:Seed:Passwords:Richmondadmin`
- `Mems:Seed:Passwords:pm`

Then run:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
$env:Mems__Seed__Enabled = 'true'
dotnet ef database update --project MEMS.Data --startup-project MEMS.Data
Remove-Item Env:Mems__Seed__Enabled
Remove-Item Env:DOTNET_ENVIRONMENT
```

The seeder creates five municipalities and 15 positions (Mayor, Councillor,
School Trustee per municipality), 32 fictional candidates, two Identity roles,
and five administrative users. Burnaby, Surrey, and Richmond each have eight
candidates. The original Pitt Meadows/Maple Ridge samples and their stable seed
keys are retained; `pm` is assigned to Pitt Meadows. Identity normalizes username
casing, so `Super` also accommodates the Assignment 1 login spelling `super`.
This does not copy or change the original SQLite accounts or their passwords.

Re-running seeding adds missing seed records without overwriting existing names,
profiles, renamed accounts, or administrator assignments. Custom-password mode
preserves existing passwords; assignment-default mode restores `P@$$w0rd` for
the five seeded accounts and clears their login lockouts. Stable seed
markers and unique indexes prevent duplication. A matching unmarked username,
email, municipality, or position causes a conflict instead of adopting existing
records or elevating privileges. Initial passwords are needed only for accounts
that have not been seeded yet. EF migration locks serialize seeding; domain and
Identity changes share the same transaction. AppHost supplies the
SQL connection and optional seed secrets to the API's Development-only startup
initializer; see the [architecture](../docs/architecture/ARCHITECTURE.md).

## Automatically created municipality administrators

`ElectionRepository.AddMunicipalityAsync` creates the municipality and an Identity
administrator in one transaction. `MunicipalityAdministratorProvisioner` assigns
only the Municipality Administrator role and that municipality's ID. The username
is the initial slug followed by `admin`; the placeholder email is
`{slug}-admin@example.com`. Existing usernames/emails cause a conflict and roll
back creation rather than reusing or elevating another account.

Development defaults to `P@$$w0rd`. To override it, or before creating
municipalities outside Development, configure
`Mems:MunicipalityAdministrator:InitialPassword` securely on the runtime host
(environment key `Mems__MunicipalityAdministrator__InitialPassword`). Production
has no default password. Name/slug edits preserve the existing login/password.
Automatic account creation applies when a new municipality is created; existing
municipalities and their assignments are not changed by startup.

Deleting an otherwise empty municipality also deletes its automatically created
administrator. Seeded/manually assigned/elevated accounts continue to restrict
deletion and must be reassigned separately. Both deletions share a transaction;
stale revisions or account conflicts roll back all changes.

## Integrity and tenant access

The model retains Assignment 1 entity fields, validation attributes, saved
candidates, and optimistic `Revision` concurrency tokens. The initial SQL Server
migration adds unique municipality slugs, unique municipality name/province and
position name/municipality indexes, filtered unique seed/email indexes, positive
seat and nonblank required-text checks, and restrictive parent deletion.
Candidate `(PositionId, MunicipalityId)` must reference a position with that same
municipality, including for writes outside EF. Saved-candidate rows cascade when
their user or candidate is deleted.

`ApplicationUser.AdministratorType` distinguishes ordinary users, Super Admins,
and municipality administrators. A database check requires exactly one valid
municipality for municipality administrators and no municipality for the other
two types. Ordinary users and the original-style saved-candidate schema are
retained but unused by the new public website. Its voting selections must use
browser `sessionStorage` only; there are no MEMS voting-selection API endpoints.
The former per-municipality Identity group table is replaced in the new schema
by the shared `Municipality Administrator` role plus the user's municipality.
The legacy group table remains in the separate iOS SQLite schema.

Every repository query/mutation requires an explicit `MunicipalityScope`.
Cross-tenant lookups return no record, writes check the stored owner and the
destination position, and only Super Admin scopes may manage municipalities.
Posted navigation graphs, seed keys, and creation timestamps are not attached
or adopted by repository writes. Updates/deletes require the original revision.

The API derives scopes server-side using `AdministratorScopeResolver` and the
authenticated user's ID, and must keep `AdministratorType`, municipality assignment,
and Identity role membership consistent when provisioning/changing users. The
resolver reads current database assignments and rejects missing/conflicting
administrator roles, mismatched types, and locked-out users. Never construct a
Super Admin scope from client input or rely solely on cached role claims. The
API registers request-scoped `TenantQueryContext`, which defaults to no tenant
access and adds EF query filters plus write guards. Trusted API resolution
enables one tenant or Super Admin access. Offline tooling and development
seeding omit that request context and remain privileged. Do not bypass the
filters in request handlers; see `MEMS.Api/README.md` for the implemented flow.

## Build and verification

Run `dotnet build MEMS.sln` from the C# course folder. The former Data/API/Admin
test projects are no longer present, so their previous `dotnet test` commands
do not apply. EF migration commands above inspect/update the actual SQL Server
model; a successful build alone does not verify database constraints or runtime
connectivity. See [solution setup](../docs/README.md) for previous verification.
