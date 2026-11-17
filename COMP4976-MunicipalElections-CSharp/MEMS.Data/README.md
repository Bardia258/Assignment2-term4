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

Seeding defaults to disabled. Enabling it outside Development throws an error.
Passwords must be supplied securely and meet Identity's policy: at least 12
characters with uppercase, lowercase, a number, and a non-alphanumeric character.
There are no default passwords and no password hashes in migrations.

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
profiles, passwords, renamed accounts, or administrator assignments. Stable seed
markers and unique indexes prevent duplication. A matching unmarked username,
email, municipality, or position causes a conflict instead of adopting existing
records or elevating privileges. Initial passwords are needed only for accounts
that have not been seeded yet. EF migration locks serialize seeding; domain and
Identity changes share the same transaction. Task 4's AppHost supplies the
SQL connection and optional seed secrets to the API's Development-only startup
initializer; see the [backend report](../docs/ASSIGNMENT2-BACKEND.md).

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
The legacy group table remains unchanged in Assignment 1.

Every repository query/mutation requires an explicit `MunicipalityScope`.
Cross-tenant lookups return no record, writes check the stored owner and the
destination position, and only Super Admin scopes may manage municipalities.
Posted navigation graphs, seed keys, and creation timestamps are not attached
or adopted by repository writes. Updates/deletes require the original revision.

The API derives scopes server-side using `AdministratorScopeResolver` and the
authenticated user's ID, and keep `AdministratorType`, municipality assignment,
and Identity role membership consistent when provisioning/changing users. The
resolver reads current database assignments and rejects missing/conflicting
administrator roles, mismatched types, and locked-out users. Never construct a
Super Admin scope from client input or rely solely on cached role claims. The
API registers request-scoped `TenantQueryContext`, which defaults to no tenant
access and adds EF query filters plus write guards. Trusted API resolution
enables one tenant or Super Admin access. Offline tooling and development
seeding omit that request context and remain privileged. Do not bypass the
filters in request handlers; see `MEMS.Api/README.md` for the implemented flow.

## Tests

```powershell
dotnet test MEMS.Data.Tests/MEMS.Data.Tests.csproj
dotnet build MEMS.sln
```

Offline tests use EF's InMemory provider for service/seeding behavior, and the
actual SQL Server provider for model, migration, and SQL-script inspection.
InMemory does not verify SQL Server constraints or transactions.

To run the live integration test, securely set `MEMS_TEST_SQLSERVER` to a SQL
Server connection string with permission to create/drop test databases, then
run the tests again. The test creates a fresh `MEMS_Task1Tests_<random ID>`
database, ignoring the supplied connection's database name, and deletes only
that database in its cleanup. It tests migration up/down/reapply, both seeding
hooks, tenant queries, raw-SQL constraint rejection, uniqueness, restrictive
deletion, and optimistic concurrency. Without that configuration it is explicitly
reported as skipped. Use the same supported SQL Server/Azure SQL version you
intend to deploy.
