# iOS API reference

The iOS API is hosted at [the Azure server](https://assignment2-term4-dbe7c6evbpajetbf.westus3-01.azurewebsites.net/). Every path below is relative to that address.

This is the SQLite-backed iOS API in `MunicipalElections.Web`. It uses JWT
authentication and is separate from the COMP4976 SQL Server API. The routes
below describe the current source. Live directory/filter reads have been checked;
authenticated login and Firebase integration still need verification on this deployment.
Requests and responses use JSON.

## Endpoint list

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/api/auth/register` | Create an account. |
| POST | `/api/auth/login` | Sign in with a username or email and password. |
| GET | `/api/auth/me` | Validate the bearer JWT and return the signed-in account. |
| GET | `/api/municipalities` | Load all municipalities. |
| GET | `/api/municipalities/{id}` | Load one municipality by ID. |
| GET | `/api/positions` | Load positions, filtered by `municipalityId` or `city`. |
| GET | `/api/positions/{id}` | Load one position by ID. |
| GET | `/api/candidates` | Load candidates; supports municipality, position, and name filters. |
| GET | `/api/candidates?municipalityId={id}` | Load candidates for one municipality. |
| GET | `/api/candidates/{id}` | Load one candidate by ID. |
| GET | `/api/candidates/search?name={name}` | Search candidates by name. |

All directory collection/detail routes accept `municipalityId` or `city`.
When both are supplied they must identify the same municipality. Candidate
collections also accept `positionId` and `name`; the position must belong to
the requested municipality. Invalid selectors return 400 and unknown records
return 404. Requests without municipality filters retain the existing directory
contract; the SwiftUI app always supplies its selected municipality.

Examples (IDs are illustrative; load actual municipality/position IDs first):

```http
GET /api/municipalities
GET /api/positions?municipalityId=3&city=Burnaby
GET /api/candidates?municipalityId=3&positionId=7
GET /api/candidates/search?city=Burnaby&name=reese
GET /api/candidates/9?municipalityId=3
```

URL-encode city/search values, for example `city=Pitt%20Meadows`. `city` is a
municipality name, not the new C# API's `municipalitySlug` parameter. A position
or detail ID outside the selected municipality returns 404. Collections return
arrays (possibly empty); detail routes return one object. There is no separate
`/api/elections` or `/api/offices` resource: election dates are municipality fields
and offices are represented by `/api/positions`.

## Authentication examples

`POST /api/auth/register` accepts:

```json
{
  "username": "student", "email": "student@example.com",
  "password": "<chosen password>", "fullName": "Example Student"
}
```

Successful registration returns 201 with `{ "id": "<user ID>", "username": "student" }`.
`fullName` is optional. Username/email/password validation and Identity password
policy apply; registration does not return a JWT.

`POST /api/auth/login` accepts a username or email in `username`:

```json
{ "username": "student@example.com", "password": "<chosen password>" }
```

Login returns `{ "token": "<JWT>" }`. Send that token as
`Authorization: Bearer <JWT>` for `/api/auth/me`. Directory reads remain public.
Signature, issuer, audience, expiry, account lockout, and security stamp are validated.

`GET /api/auth/me` returns:

```json
{ "id": "<user ID>", "email": "student@example.com", "username": "student" }
```

The SwiftUI app stores the JWT/email in Keychain. There is no server logout or
refresh endpoint; the app clears its local session and signs out of Firebase.
Firebase uses its own email/password login; this ASP.NET JWT is not a Firebase
credential and cannot authorize Firestore rules.

## Directory response example

JSON field names remain compatible with the SwiftUI Codable models. For example,
`GET /api/candidates/9?municipalityId=3` returns an object shaped as follows:

```json
{
  "id": 9, "name": "Example Candidate", "profile": "Candidate biography",
  "photoUrl": null, "website": "https://example.com/candidate",
  "email": "candidate@example.com", "dateCreated": "2026-10-15T00:00:00Z",
  "municipalityId": 3, "municipalityName": "Burnaby",
  "positionId": 7, "positionTitle": "Mayor",
  "municipality": {
    "id": 3, "name": "Burnaby", "province": "BC",
    "electionDate": "2026-10-17T00:00:00", "description": "Election information",
    "logoUrl": null
  },
  "position": {
    "id": 7, "name": "Mayor", "seatsAvailable": 1,
    "municipalityId": 3, "municipalityName": "Burnaby"
  }
}
```

Municipality and position endpoints return the corresponding nested DTO shapes
above. Unlike `MEMS.Api`, these DTOs have no `slug` or `revision` fields. Image
URLs point to existing `/uploads/logos/...` or `/uploads/candidates/...` assets;
this API has no image-upload endpoint.

## Errors

| Status | Meaning |
| --- | --- |
| 400 | Invalid/missing fields, invalid/ambiguous city, conflicting selectors or duplicate registration. |
| 401 | Invalid credentials, unavailable/2FA account or missing/invalid/expired JWT. |
| 404 | Unknown municipality/resource, position outside selected city or removed route. |
| 500 | Safe server error, including an unexpected database failure. |

Validation and framework errors use Problem Details, optionally with an `errors`
dictionary and `traceId`. Login failures preserve the existing
`{ "message": "Invalid credentials." }`-style response. Do not assume every
error has the same JSON shape or expose raw server exceptions in the UI.

```json
{
  "title": "One or more validation errors occurred.", "status": 400,
  "errors": { "Email": ["The Email field is not a valid e-mail address."] },
  "traceId": "<request trace ID>"
}
```

## Firebase and removed routes

Saved selections and municipality profiles use Firebase in the iOS app.
The old `/api/savedcandidates`, MVC/Admin pages, Identity UI, and browser voting
guide have been removed. Historical SQLite table mappings/migrations remain
compatible with the existing database; the API adds no saved-selection records.

## Configuration and deployment

Publish `MunicipalElections.Web/MunicipalElections.Web.csproj` to the existing
Linux Azure Web App `assignment2-term4`. Docker Desktop and SQL Server are not
required for this backend. Configure the following Azure settings:

- `ConnectionStrings:DefaultConnection`: SQLite connection string, using a persistent database path.
- `Jwt:SigningKey`: the existing server-side signing key (at least 32 UTF-8 bytes).
- `Jwt:Issuer` and `Jwt:Audience`: retain their existing values.
- `Uploads:RootPath`: persistent storage for existing photographs and logos, served at `/uploads`.

The current database path is `/home/data/mems-ios/app.db`; upload storage is
`/home/data/mems-ios/uploads`. Both are outside the deployed code directory.

Use environment/App Service configuration or local user secrets. Environment
keys use double underscores, for example `ConnectionStrings__DefaultConnection`
and `Jwt__SigningKey`. Never put a server signing key in the iOS app. Keep
`Jwt:LifetimeMinutes` consistent with the existing deployment (default 60).
Development can generate a temporary signing key if none is supplied; tokens
then become invalid after a restart. Production requires a configured key.

Production does not migrate or seed on HTTP startup. The separate maintenance
commands `--migrate-database` and `--seed-demo-data` remain available; MVC
administrator-provisioning commands were removed. Development startup migrates
and adds missing sample election data. iOS users register through the API.
The service root returns JSON metadata and errors use safe Problem Details.

## Backend source

- [Directory endpoints and response fields](../../MunicipalElections.Web/Controllers/Api/DirectoryController.cs)
- [Registration and login](../../MunicipalElections.Web/Controllers/Api/AuthController.cs)
- [Current-user validation](../../MunicipalElections.Web/Controllers/Api/AuthSessionController.cs)

See [setup](../README.md), [architecture](../architecture/ARCHITECTURE.md) and
[SQLite/Firestore data model](../erd/ERD.md). This API has no bundled Swagger UI;
the route table and source contracts are its reference.
