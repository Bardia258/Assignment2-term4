# MEMS.Api

The .NET 10 SQL Server API for COMP4976. The separate SQLite/JWT iOS API lives
in `COMP4977-MunicipalElections-iOS/MunicipalElections.Web`.

- [Run the complete solution with Aspire](../docs/README.md)
- [API routes, authentication, request/response examples and errors](../docs/api/API-ENDPOINTS.md)
- [Architecture and tenant isolation](../docs/architecture/ARCHITECTURE.md)
- [SQL migrations and development seeding](../MEMS.Data/README.md)

## Standalone configuration

Prefer AppHost for normal local use: it supplies the SQL connection, initializes
the Development database and configures CORS/image origins. Standalone API
startup does not automatically migrate or seed the database.

From `COMP4976-MunicipalElections-CSharp`, after preparing the MEMS SQL database:

```powershell
dotnet user-secrets set --project MEMS.Api "ConnectionStrings:MEMS" "<MEMS SQL Server connection string>"
dotnet run --project MEMS.Api --launch-profile https
```

Alternatively supply `ConnectionStrings__MEMS` in the environment. API runtime
secrets and Data design-time secrets belong to different project stores.

| Setting | Purpose |
| --- | --- |
| `Cors:AllowedOrigins` | Exact trusted browser origins; defaults to no cross-origin access. |
| `Images:BaseUrl` | Admin's public HTTPS origin for uploaded election images. |
| `OpenApi:Enabled` | Enable `/openapi/v1.json` outside Development if required. |

The default HTTPS origin is `https://localhost:7249`. Cookie/CSRF security
requires HTTPS. API cookies are separate from Admin UI cookies and iOS JWTs.
Public read clients do not need a login. There are no public voting-selection
persistence endpoints.
