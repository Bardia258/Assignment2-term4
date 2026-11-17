# COMP4977 API compatibility and Firebase handoff

## Backend boundary

The iOS app continues to call MunicipalElections.Web at its existing Azure URL.
That application retains its SQLite database, migrations, Identity users, JWT
configuration, MVC functionality, and deployment settings. MEMS.Api is a separate
SQL Server service for COMP4976 and must not replace the iOS server.

No SwiftUI files were changed. The original iOS API documentation remains intact.

## Election filtering

The existing endpoints are extended, with no duplicate office/election API:

| Existing route | Optional filters |
| --- | --- |
| GET /api/municipalities | municipalityId, city |
| GET /api/municipalities/{id} | municipalityId, city |
| GET /api/positions | municipalityId, city |
| GET /api/positions/{id} | municipalityId, city |
| GET /api/candidates | municipalityId, city, positionId, name |
| GET /api/candidates/search | municipalityId, city, positionId, name |
| GET /api/candidates/{id} | municipalityId, city |

Use the actual IDs returned by the legacy municipality directory. The SQL Server
seed IDs and towns do not identify records in the iOS database.

Examples:

    GET /api/candidates?municipalityId=<selected legacy municipality ID>
    GET /api/positions?city=burnaby
    GET /api/municipalities?city=pitt-meadows
    GET /api/candidates/search?city=burnaby&name=reese

City matching is case-insensitive and accepts display names and the existing
stable sample identifiers (burnaby, pitt-meadows, maple-ridge). Ambiguous names
return 400 and require an ID. Supplied IDs must be positive and exist. Empty or
malformed selectors return 400, unknown municipalities/positions return 404, and
conflicting city/ID selectors return 400. Detail reads also enforce any supplied
municipality filter. Candidate position filters must belong to the selected city.

Filtered candidate responses and their nested municipality/position data belong
to that city only. Filtered positions belong to that city only. The real model
has no separate election resource: electionDate is already part of each
municipality, including nested candidate municipality responses.

Requests with no filters retain the original all-municipality behavior for
compatibility, including fetchAllCandidates(). Existing camelCase JSON fields,
nested responses, nullable image/contact fields, and date representations are
preserved. The existing Swift fetchCandidates(for:) already sends municipalityId
and needs no API contract change.

When the partner implements city switching, clear/reload the selected city's
positions and candidates using that city's ID. Cancel or discard an old city's
in-flight response before displaying data for a newly selected city.

## Authentication retained

POST /api/auth/register still accepts username, email, password, and optional
fullName, and returns 201 with id and username. POST /api/auth/login still accepts
a username/email in username plus password and returns { "token": "<JWT>" }.
JWT signature, issuer, audience, lifetime, security stamp, and lockout validation
remain unchanged. Tokens contain no administrative role claims.

Existing /api/savedcandidates GET/POST/DELETE remain available and retain
user-ownership checks. They are preserved for Assignment 1 compatibility; the
partner's new Firestore flow should stop using them once migration is ready.
The existing MVC voting guide is not removed or migrated.

## Firebase authentication requires a separate sign-in

The ASP.NET HMAC JWT is not a Firebase ID token and cannot directly authenticate
Firestore rules. A client-supplied email is not proof of identity.

Recommended integration, based on
[Firebase custom authentication](https://firebase.google.com/docs/auth/admin/create-custom-tokens):

1. Retain ASP.NET registration/login and the existing bearer JWT.
2. Configure the Firebase project and a server-side Firebase Admin identity using
   Application Default Credentials/workload identity or a securely mounted
   service account. Do not commit credentials or put private keys in iOS code.
3. Add a backend exchange only after this configuration is available and tested.
   It must require the existing JwtBearer scheme, including current-user,
   security-stamp, and lockout validation. Derive the Firebase UID from the
   authenticated database user's immutable Identity ID, e.g.
   municipal-elections-ios:<user ID>, not an email, posted user ID, or username.
4. The backend uses Firebase Admin to mint a custom token for that UID and
   returns it over HTTPS with no-store caching. Never log either token.
5. iOS signs in through Firebase Auth using signIn(withCustomToken:). Firestore
   SDK calls then use Firebase's own authentication session.
6. Store private selections below users/{firebaseUid}/savedCandidates/{recordId}.
   Scope every read/write by the authenticated Firebase UID. Store the original
   legacy candidate and municipality IDs, not MEMS SQL Server IDs.
7. Sign out of Firebase and discard the ASP.NET token when the user logs out.
   Coordinate Firebase session revocation separately: expiration/revocation of
   an ASP.NET JWT does not automatically revoke an existing Firebase session.

An ownership-rule foundation, to extend with field/type validation and test in
the Firebase Emulator Suite, is:

    rules_version = '2';
    service cloud.firestore {
      match /databases/{database}/documents {
        match /users/{uid}/savedCandidates/{recordId} {
          allow read, write: if request.auth != null && request.auth.uid == uid;
        }
      }
    }

All other paths are denied by default. Do not add a blanket allow rule.
Owner checks must apply to direct document access as well as queries. Define
allowed fields/types for candidateId, municipalityId, and timestamps before
deployment. Client-provided candidate/municipality values are untrusted private
selection data; they must never grant administrative authority or become
authoritative election records. See
[Firestore authorization rules](https://firebase.google.com/docs/firestore/security/rules-conditions).

## What remains for the partner/backend integration

No Firebase project ID, Admin signing identity, credentials, or emulator setup
was supplied. Consequently no custom-token exchange endpoint, Firebase dependency,
service account file, or permissive Firestore rule was added. The design above
is a handoff, not a deployed or tested Firebase integration.

The partner must provide/configure the Firebase project, iOS Firebase Auth and
Firestore SDKs, credential/signing setup, UID/path agreement, rule validation,
and emulator tests. The backend exchange must then be implemented and tested
for forged/expired ASP.NET JWTs, changed security stamps, locked-out users,
stable UID binding, anonymous denial, and cross-user Firestore access denial.

The new C# public voting guide must remain separate: no Firestore integration
and no SQL Server persistence for its public voting selections were added.

## Verified scope

Automated tests exercise local HTTP registration/login, JWT security-stamp
revocation, saved-record ownership, city/position filtering, and Swift-compatible
response fields against isolated SQLite storage. No existing SQLite file is
opened by those tests. Azure deployment, Firebase signing, Firestore rules, and
device integration have not been tested or deployed.
