# C# API reference

`MEMS.Api` is the SQL Server backend for the public Blazor application and
administrative API clients. Default local origin: `https://localhost:7249`.
It is separate from the iOS Azure API and does not accept that API's JWTs.
Run it through [Aspire](../README.md). OpenAPI JSON is available at
`/openapi/v1.json` in Development; there is no bundled Swagger UI.

## Routes

All paths below are relative to the API origin.

| Resource | GET collection | GET detail | POST create | PUT update | DELETE |
| --- | --- | --- | --- | --- | --- |
| Municipalities | `/api/municipalities` | `/api/municipalities/{id}` | `/api/municipalities` | `/api/municipalities/{id}` | `/api/municipalities/{id}?revision={revision}` |
| Positions | `/api/positions` | `/api/positions/{id}` | `/api/positions` | `/api/positions/{id}` | `/api/positions/{id}?revision={revision}` |
| Candidates | `/api/candidates` | `/api/candidates/{id}` | `/api/candidates` | `/api/candidates/{id}` | `/api/candidates/{id}?revision={revision}` |

Additional reads: `GET /api/municipalities/slug/{slug}` and
`GET /api/candidates/search` (same filters as the candidate collection).
Collections return JSON arrays. Details return one DTO.

## Tenant filtering

| Query parameter | Applies to | Meaning |
| --- | --- | --- |
| `municipalityId` | Directory reads | Positive municipality ID. |
| `municipalitySlug` | Directory reads | Lowercase slug such as `burnaby`. |
| `positionId` | Candidate collection/search | Positive position ID within the selected municipality. |
| `name` | Candidate collection/search | Name substring, maximum 100 characters. |

```http
GET /api/municipalities
GET /api/municipalities/slug/burnaby
GET /api/positions?municipalitySlug=burnaby
GET /api/candidates?municipalitySlug=burnaby&name=reese
GET /api/candidates?municipalityId=3&positionId=7
GET /api/candidates/9?municipalitySlug=burnaby
```

IDs above are examples: resolve actual municipality/position IDs first.
Anonymous position/candidate reads, including details, require a municipality
ID or slug. The municipality registry may be listed without a selector.
Conflicting ID/slug selectors return 400; unknown municipalities return 404.

An authenticated Municipality Administrator defaults to their stored municipality
and receives 403 when explicitly selecting another. A resource outside the
effective scope returns 404. Super Admin can select one or read across tenants.
All writes require authentication and CSRF protection. Municipality writes
require Super Admin; position/candidate writes enforce stored tenant ownership.
A candidate's position must belong to its municipality. Positions cannot move
municipalities; only Super Admin may transfer a candidate to a valid destination.

Creating a municipality also provisions its Identity administrator atomically.
The username is the initial slug plus `admin` (for example `testadmin`); the role
and assignment grant access only to that municipality. Local Development uses
the assignment password unless overridden. Other environments require
`Mems:MunicipalityAdministrator:InitialPassword` on the API host. Existing account
collisions return 409 without creating the municipality. The response DTO remains
the municipality contract below. Deleting an empty municipality also removes its
automatically provisioned account; other administrator assignments prevent deletion.

## Authentication

| Method | Path | Behavior |
| --- | --- | --- |
| GET | `/api/auth/csrf` | Return `requestToken` and `headerName`; set CSRF companion cookie. |
| POST | `/api/auth/login` | Accept username/email and password; set administrator cookie and return session details. |
| GET | `/api/auth/me` | Return authenticated administrator ID, username, role and municipality assignment. |
| POST | `/api/auth/logout` | Require authentication/CSRF; invalidate sessions and clear cookie; return 204. |

1. GET `/api/auth/csrf` and retain its cookie and token.
2. POST login as JSON with `X-CSRF-TOKEN: <requestToken>` and that cookie.
3. GET `/api/auth/csrf` again after login to obtain an authenticated CSRF token.
4. Retain `__Host-MEMS.Admin` for reads and writes; send the fresh CSRF header
   and companion cookie for every POST, PUT and DELETE, including logout.

```json
{ "username": "Burnabyadmin", "password": "P@$$w0rd" }
```

The example password is the local Aspire development default only. Session
responses contain `userId`, `username`, `role` and nullable `municipalityId`.
Cookies are Secure, HttpOnly and SameSite=Strict with a fixed 30-minute lifetime.
Current account status, role, assignment and security stamp are checked.
Login is rate limited and uses Identity lockout. No public admin registration or
two-factor challenge UI is provided; accounts requiring 2FA are refused.

Admin UI login uses a separate `__Host-MEMS.AdminUI` cookie. Signing into
`MEMS.Admin` does not sign an API client in. For browser API clients retain
cookies explicitly and configure an exact trusted CORS origin; do not share
a cookie container between users. Public Blazor reads need no login.

## Requests and responses

DTOs and validation live in
[ElectionContracts.cs](../../MEMS.Shared/Contracts/ElectionContracts.cs).
JSON property names use camelCase. POST bodies:

```json
{
  "name": "Example City", "slug": "example-city", "province": "BC",
  "electionDate": "2026-10-17", "description": "Election information",
  "logoFileName": null
}
```

```json
{ "name": "Mayor", "seatsAvailable": 1, "municipalityId": 3 }
```

```json
{
  "name": "Example Candidate", "profile": "Candidate biography",
  "municipalityId": 3, "positionId": 7,
  "website": "https://example.com/candidate",
  "email": "candidate@example.com", "photoFileName": null
}
```

A candidate response includes its related municipality and position:

```json
{
  "id": 33, "name": "Example Candidate", "profile": "Candidate biography",
  "photoUrl": null, "website": "https://example.com/candidate",
  "email": "candidate@example.com", "dateCreated": "2026-11-16T00:00:00Z",
  "municipalityId": 3, "municipalityName": "Burnaby",
  "positionId": 7, "positionTitle": "Mayor",
  "municipality": {
    "id": 3, "name": "Burnaby", "slug": "burnaby", "province": "BC",
    "electionDate": "2026-10-17T00:00:00", "description": "Election information",
    "logoUrl": null, "revision": "0123456789abcdef0123456789abcdef"
  },
  "position": {
    "id": 7, "name": "Mayor", "seatsAvailable": 1,
    "municipalityId": 3, "municipalityName": "Burnaby",
    "revision": "0123456789abcdef0123456789abcdef"
  },
  "revision": "0123456789abcdef0123456789abcdef"
}
```

POST returns 201, a response DTO and `Location`. PUT uses the corresponding POST
fields plus the latest `revision` and returns 200 with an updated DTO. DELETE
uses that revision in the query string and returns 204. Revisions are 32
lowercase hexadecimal characters. A stale revision returns 409; reload first.

Names/slugs/province are limited to 100 characters; candidate biography is
required and limited to 4000, municipality description to 2000, email to 254,
URL to 2048. Seats and IDs must be positive; dates must be in 2000–2100.
Optional image fields accept safe stored filenames, not arbitrary paths.
Uploads are handled by Admin, not an API upload endpoint. Aspire configures
image URLs to point to Admin's public `/uploads` origin.

## Errors

| Status | Meaning |
| --- | --- |
| 400 | Invalid fields/relationships, missing tenant, conflicting selectors or invalid CSRF. |
| 401 | Missing, expired or revoked login; invalid credentials. |
| 403 | Insufficient role or administrator municipality mismatch. |
| 404 | Tenant/resource not found in the effective scope. |
| 409 | Duplicate unique value, dependent records or stale revision. |
| 429 | Login rate limit reached. |
| 500 / 503 | Safe unexpected error / temporarily unavailable SQL Server. |

Errors use `application/problem+json`; validation adds an `errors` dictionary.
Do not depend on error wording for client behavior.

```json
{
  "title": "One or more validation errors occurred.", "status": 400,
  "errors": { "Email": ["The Email field is not a valid e-mail address."] },
  "traceId": "<request trace ID>"
}
```

The public voting guide has no persistence endpoints. Keep its selections only
in browser `sessionStorage`. See [architecture](../architecture/ARCHITECTURE.md)
and [ERD](../erd/ERD.md) for the service and database boundaries.
