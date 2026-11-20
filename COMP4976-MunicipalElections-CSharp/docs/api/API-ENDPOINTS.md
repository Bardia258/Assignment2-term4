# C# API reference

Base URL: `https://localhost:7249`

## Routes

| Method | Route |
| --- | --- |
| GET | `/api/municipalities` |
| GET | `/api/municipalities/{id}` |
| GET | `/api/municipalities/slug/{slug}` |
| POST | `/api/municipalities` |
| PUT | `/api/municipalities/{id}` |
| DELETE | `/api/municipalities/{id}?revision={revision}` |
| GET | `/api/positions` |
| GET | `/api/positions/{id}` |
| POST | `/api/positions` |
| PUT | `/api/positions/{id}` |
| DELETE | `/api/positions/{id}?revision={revision}` |
| GET | `/api/candidates` |
| GET | `/api/candidates/{id}` |
| GET | `/api/candidates/search` |
| POST | `/api/candidates` |
| PUT | `/api/candidates/{id}` |
| DELETE | `/api/candidates/{id}?revision={revision}` |

## Tenant filtering

| Parameter | Purpose |
| --- | --- |
| `municipalityId` | Select a municipality by ID. |
| `municipalitySlug` | Select a municipality by slug, such as `burnaby`. |
| `positionId` | Filter candidates by a position in the selected municipality. |
| `name` | Search candidates by part of their name. |

Public position/candidate reads require a municipality ID or slug. The
municipality list can be read without a filter. If both ID and slug are supplied,
they must identify the same municipality. Related positions and candidates
must belong to that municipality.

## Examples

Read and filter data:

```http
GET /api/municipalities
GET /api/municipalities/slug/burnaby
GET /api/positions?municipalitySlug=burnaby
GET /api/candidates?municipalitySlug=burnaby&positionId=7
GET /api/candidates/search?municipalitySlug=burnaby&name=reese
GET /api/candidates/9?municipalitySlug=burnaby
```

IDs below are examples; use the IDs returned by your API.

Create a municipality — `POST /api/municipalities`:

```json
{
  "name": "Example City", "slug": "example-city", "province": "BC",
  "electionDate": "2026-10-17", "description": "Election information"
}
```

Create a position — `POST /api/positions`:

```json
{ "name": "Mayor", "seatsAvailable": 1, "municipalityId": 3 }
```

Example position response:

```json
{
  "id": 7, "name": "Mayor", "seatsAvailable": 1,
  "municipalityId": 3, "municipalityName": "Burnaby",
  "revision": "0123456789abcdef0123456789abcdef"
}
```

Create a candidate — `POST /api/candidates`:

```json
{
  "name": "Example Candidate", "profile": "Candidate biography",
  "municipalityId": 3, "positionId": 7,
  "website": "https://example.com", "email": "candidate@example.com"
}
```

For PUT, send the record's fields plus its current `revision` from GET.
For DELETE, pass that same revision in the query string:

```http
DELETE /api/positions/7?revision=0123456789abcdef0123456789abcdef
```
