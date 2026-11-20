# iOS API reference

The iOS API is hosted at [the Azure server](https://assignment2-term4-dbe7c6evbpajetbf.westus3-01.azurewebsites.net/). Every path below is relative to that address.

This SQLite-backed API uses JWT authentication and JSON requests/responses.
It is separate from the C# assignment's SQL Server API.

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
| GET | `/api/candidates/{id}` | Load one candidate by ID. |
| GET | `/api/candidates/search?name={name}` | Search candidates by name. |

## Municipality filtering

- Candidate lists also support `positionId` and `name`. The position must belong
  to the selected municipality.
- Unfiltered requests can return all municipalities' records. The iOS app always
  sends its selected municipality.

Examples (IDs are illustrative; load actual municipality/position IDs first):

```http
GET /api/municipalities
GET /api/positions?municipalityId=3&city=Burnaby
GET /api/candidates?municipalityId=3&positionId=7
GET /api/candidates/search?city=Burnaby&name=reese
GET /api/candidates/9?municipalityId=3
```

Encode spaces in city names, for example `city=Pitt%20Meadows`.
Lists return arrays; detail routes return one object. Election dates are part
of municipalities, and offices are represented by positions.

## Authentication examples

`POST /api/auth/register` accepts:

```json
{
  "username": "student", "email": "student@example.com",
  "password": "<chosen password>", "fullName": "Example Student"
}
```

Registration returns 201 with `{ "id": "<user ID>", "username": "student" }`.
`fullName` is optional.

`POST /api/auth/login` accepts a username or email in `username`:

```json
{ "username": "student@example.com", "password": "<chosen password>" }
```

Login returns `{ "token": "<JWT>" }`. Send that token as
`Authorization: Bearer <JWT>` for `/api/auth/me`. Directory reads remain public.

`GET /api/auth/me` returns:

```json
{ "id": "<user ID>", "email": "student@example.com", "username": "student" }
```

The app stores its API session in Keychain. Firebase sign-in is separate and
controls access to personal profiles and saved selections.

## Directory response example

Example response for `GET /api/candidates/9?municipalityId=3`:

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

Municipality and position endpoints return the corresponding objects shown
above. Image URLs use `/uploads/logos/...` or `/uploads/candidates/...`.

## Errors

| Status | Meaning |
| --- | --- |
| 400 | Invalid fields, conflicting city filters or duplicate registration. |
| 401 | Invalid login or missing/expired token. |
| 404 | Record not found in the selected municipality. |
| 500 | Server error. |

Validation errors include field messages. Login failures return a `message`,
such as `"Invalid credentials."`.

