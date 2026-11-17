# MEMS API v1

This is the COMP4976 SQL Server API. The COMP4977 Azure/iOS backend remains
MunicipalElections.Web with its own SQLite database and JWT authentication.
Neither backend reads the other's connection settings, cookies, or tokens.

## Run and configure

From COMP4976-MunicipalElections-CSharp:

    dotnet user-secrets set --project MEMS.Api "ConnectionStrings:MEMS" "<SQL Server connection string for the MEMS database>"
    dotnet run --project MEMS.Api --launch-profile https

Alternatively supply ConnectionStrings__MEMS securely through the environment.
Apply the Task 1 migrations and optional development seed using the instructions
in [the data README](../MEMS.Data/README.md). EF's design-time factory reads the
Data project's user secrets; runtime reads the API project's user secrets.
An environment connection setting can configure both. No migrations or seed
accounts run automatically when the API starts.

Use HTTPS; the template HTTPS endpoint is https://localhost:7249. Cookie and CSRF
security is not relaxed for HTTP. Production should terminate HTTPS correctly
and persist/protect Data Protection keys, sharing them only among instances of
this API. Do not share keys/cookies with the legacy application.

Optional settings:

- Cors:AllowedOrigins: exact trusted frontend origins (an array), never "*".
  Defaults to no cross-origin access. Set Cors__AllowedOrigins__0, etc. for
  environment configuration.
- Images:BaseUrl: optional trusted asset origin, e.g. https://assets.example.com.
  Otherwise image references are relative /uploads/logos/{filename} and
  /uploads/candidates/{filename}. Hosting existing assets is an operator concern;
  this milestone does not implement uploads or migrate image files.
- OpenApi:Enabled: enables /openapi/v1.json outside Development. The document
  is enabled in Development automatically.

## Authentication

There is no public administrator registration endpoint. Use the Task 1 seeded
accounts with securely configured passwords.

| Method | Path | Behavior |
| --- | --- | --- |
| GET | /api/auth/csrf | Returns requestToken and headerName; sets the secure companion cookie. |
| POST | /api/auth/login | Username or email + password; returns the admin session and sets the Identity cookie. |
| GET | /api/auth/me | Returns the currently authenticated administrator's assignment. |
| POST | /api/auth/logout | Invalidates existing sessions for that user and clears the cookie. |

Browser workflow:

1. GET /api/auth/csrf and retain the companion cookie.
2. POST /api/auth/login with Content-Type: application/json and
   X-CSRF-TOKEN: <requestToken>.
3. GET /api/auth/csrf again after login; the token is tied to the authenticated
   identity. Send the refreshed header and companion cookie for POST/PUT/DELETE.
4. Retain the __Host-MEMS.Admin cookie for authenticated reads and writes.
5. POST /api/auth/logout with the current CSRF token when finished.

Login request:

    { "username": "Burnabyadmin", "password": "<configured development password>" }

Session response (IDs are examples; resolve actual IDs from the directory):

    {
      "userId": "<Identity user ID>",
      "username": "Burnabyadmin",
      "role": "Municipality Administrator",
      "municipalityId": 3
    }

Cookies are Secure, HttpOnly, SameSite=Strict, nonpersistent, and expire after
30 minutes without sliding renewal. Security stamps and current stored role/
municipality assignments are checked every request. Logout rotates the security
stamp, invalidating previously copied cookies and other sessions for that user.
Invalid, conflicting, locked-out, removed, or revoked administrator assignments
cannot authenticate. Accounts requiring two-factor authentication are not signed
in by this password-only flow; a future 2FA flow must complete that challenge.

Login failures are generic. Identity lockout applies, and login requests are
limited to 10 per minute per connection IP. Configure trusted proxy forwarding
before relying on per-client IP limits behind a reverse proxy; never trust
arbitrary forwarded headers.

For Blazor Server integration, prefer same-origin routing/reverse proxy or a BFF.
Browser cookies do not automatically accompany a server-side HttpClient call.
Never share one CookieContainer across different administrators. Same-site,
cross-origin browser calls require explicitly allowed origins and credentials.
SameSite=Strict intentionally blocks cross-site session use even if CORS permits
an origin. No Blazor Admin UI is implemented here.

The legacy iOS bearer token is not accepted by this API.

## Election endpoints

The following methods exist for each resource:

| Resource | GET collection | GET detail | POST | PUT | DELETE |
| --- | --- | --- | --- | --- | --- |
| Municipality | /api/municipalities | /api/municipalities/{id} | /api/municipalities | /api/municipalities/{id} | /api/municipalities/{id}?revision={revision} |
| Position | /api/positions | /api/positions/{id} | /api/positions | /api/positions/{id} | /api/positions/{id}?revision={revision} |
| Candidate | /api/candidates | /api/candidates/{id} | /api/candidates | /api/candidates/{id} | /api/candidates/{id}?revision={revision} |

Additional reads:

- GET /api/municipalities/slug/{slug}
- GET /api/candidates/search, with the same query parameters as the collection.

Filters:

| Parameter | Applies to | Meaning |
| --- | --- | --- |
| municipalityId | Collection/detail reads | Positive municipality ID. |
| municipalitySlug | Collection/detail reads | Lowercase URL slug, e.g. burnaby. |
| positionId | Candidate collection/search | Positive position ID within the effective tenant. |
| name | Candidate collection/search | Case-insensitive name substring, max 100 characters. |

Anonymous position/candidate reads, including detail reads, require municipalityId
or municipalitySlug. The public municipality directory is the selector exception:
it lists municipalities, their metadata/election dates, and no nested election
records. A municipality detail route itself supplies its tenant ID/slug.

Municipality administrators default to their stored assignment when no filter
is supplied. They cannot select another municipality. Super Admin reads may
span all municipalities or select one. If ID and slug disagree, return 400;
unknown tenants return 404. Cross-tenant admin selectors return 403; resource IDs
outside an effective tenant return 404. Public responses contain only records
belonging to the requested tenant, including nested position/municipality data.

Examples:

    GET /api/positions?municipalitySlug=burnaby
    GET /api/candidates?municipalitySlug=burnaby&name=reese
    GET /api/candidates?municipalityId=3&positionId=7
    GET /api/candidates/9?municipalitySlug=burnaby

All POST/PUT/DELETE requests require an active administrator cookie and CSRF.
Only Super Admin can create, change, or delete municipalities. Municipality
administrators may manage positions/candidates only inside their assignment.
Body municipality IDs select a destination; they never grant authorization.
Records cannot be moved between municipalities by PUT.

## Contracts and examples

Reusable browser-compatible DTOs live in MEMS.Shared/Contracts. Responses use
camelCase JSON. Entity navigation graphs, Identity password hashes/security
stamps, internal seed markers, and client-supplied creation timestamps are never
bound or returned.

Municipality POST:

    {
      "name": "Example City", "slug": "example-city", "province": "BC",
      "electionDate": "2026-10-17", "description": "Election information",
      "logoFileName": null
    }

Position POST:

    { "name": "Mayor", "seatsAvailable": 1, "municipalityId": 3 }

Candidate POST:

    {
      "name": "Example Candidate", "profile": "Candidate biography",
      "municipalityId": 3, "positionId": 7,
      "website": "https://example.com/candidate",
      "email": "candidate@example.com", "photoFileName": "example.png"
    }

Candidate response:

    {
      "id": 33, "name": "Example Candidate", "profile": "Candidate biography",
      "photoUrl": "/uploads/candidates/example.png",
      "website": "https://example.com/candidate", "email": "candidate@example.com",
      "dateCreated": "2026-11-16T00:00:00Z",
      "municipalityId": 3, "municipalityName": "Burnaby",
      "positionId": 7, "positionTitle": "Mayor",
      "municipality": {
        "id": 3, "name": "Burnaby", "slug": "burnaby", "province": "BC",
        "electionDate": "2026-10-17T00:00:00",
        "description": "Election information", "logoUrl": null,
        "revision": "<32 lowercase hexadecimal characters>"
      },
      "position": {
        "id": 7, "name": "Mayor", "seatsAvailable": 1,
        "municipalityId": 3, "municipalityName": "Burnaby",
        "revision": "<32 lowercase hexadecimal characters>"
      },
      "revision": "<32 lowercase hexadecimal characters>"
    }

PUT uses the corresponding POST fields plus the original revision from GET.
DELETE requires that revision in its query string. Revisions contain 32 lowercase
hexadecimal characters; stale revisions return 409. Read again before retrying.
Successful POST returns 201, the response DTO, and a Location header.
PUT returns 200 with the updated DTO; DELETE returns 204.

## Errors and isolation

| Status | Meaning |
| --- | --- |
| 400 | Invalid/missing fields, invalid related position, missing tenant selector, conflicting selectors, or invalid CSRF. |
| 401 | Missing/expired/revoked authentication, or invalid login. |
| 403 | Administrator tenant mismatch or insufficient municipality-management permission. |
| 404 | Requested tenant/resource/position not found within the effective scope. |
| 409 | Duplicate unique values, dependent records preventing deletion, or stale revision. |
| 429 | Login rate limit exceeded. |
| 500 | Unexpected server failure; no exception details returned. |
| 503 | SQL Server temporarily unavailable. |

Errors use application/problem+json. Validation errors also have an errors
dictionary. Example:

    {
      "title": "One or more validation errors occurred.",
      "status": 400,
      "errors": { "Email": [ "The Email field is not a valid e-mail address." ] },
      "traceId": "<request trace ID>"
    }

Other errors have title/status/detail/traceId. Logs record structured categories,
status, method, path, and trace ID without logging credentials or request bodies.

Authorization derives from current Identity data. The repository independently
checks trusted scopes, stored owners, related positions, and uniqueness.
Request-scoped TenantQueryContext adds EF query filters and SaveChanges guards
for domain records. An API context starts with no tenant access. Only trusted
resolution enables one municipality or Super Admin access. Schema foreign keys
remain the final guard against cross-municipality position assignments.
No schema migration is needed for the additional EF query filters.

## Documentation and tests

/openapi/v1.json describes all routes, DTO validation, cookie/CSRF security
schemes, success responses, and error responses. MEMS.Api.http provides request
examples without real credentials.

    dotnet test MEMS.Api.Tests/MEMS.Api.Tests.csproj
    dotnet test MEMS.Data.Tests/MEMS.Data.Tests.csproj
    dotnet build MEMS.sln

HTTP tests use real ASP.NET authentication, CSRF, routing, validation, and
middleware. MEMS storage is replaced only in tests with EF InMemory. Legacy
compatibility tests use a separate, isolated in-memory SQLite database.
The optional Task 1 SQL Server test still requires MEMS_TEST_SQLSERVER.
These tests do not verify Azure deployment or live Firebase access.

No new voting-guide persistence endpoint is added. Public Blazor selections
remain a future client-side concern and are not stored in SQL Server or Firebase.
