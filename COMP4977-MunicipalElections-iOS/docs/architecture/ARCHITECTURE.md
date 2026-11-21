# iOS architecture

The SwiftUI app runs on iPhone and iPad. It uses an Azure-hosted API for
election information and Firebase for personal profiles and saved selections.

```mermaid
flowchart TD
    Views["SwiftUI views -<br/>phone and iPad<br/>navigation"]
    VM["ViewModels -<br/>auth,<br/>municipality,<br/>directory and<br/>selections"]
    Client["APIClient -<br/>URLSession,<br/>async/await,<br/>Codable"]
    Session["AuthSession -<br/>JWT and email<br/>in Keychain"]
    Api["Azure App<br/>Service -<br/>ASP.NET Core<br/>API and<br/>Identity/JWT"]
    Sqlite[("SQLite -<br/>accounts and<br/>election data")]
    Auth["Firebase<br/>Authentication<br/><br/>email/password"]
    Firestore[("Firestore - user<br/>profiles and<br/>candidate<br/>selections")]

    Views --> VM
    VM --> Client
    VM --> Session
    Session -->|"Bearer JWT for current-<br/>user check"| Client
    Client -->|"HTTPS - login and<br/>municipality-filtered<br/>directory"| Api
    Api -->|EF Core| Sqlite
    VM -->|"matching email/password<br/>sign-in"| Auth
    VM -->|"Firebase SDK - owner-<br/>scoped reads and writes"| Firestore
    Auth -. "authenticated identity for<br/>rules" .-> Firestore
```

## Data flow

1. The user signs in through one login screen. The app establishes an API
   session and a Firebase session using matching email/password credentials.
   The API login token and email are stored in Keychain.
2. The user chooses a municipality. The app retrieves its positions, candidates
   and photograph URLs from the Azure API, which reads SQLite.
3. Firestore stores the user's preferred municipality, saved candidates and
   personal notes. Firebase authentication and security rules restrict access
   to the user's own records.
4. Changing municipality reloads the election information and shows selections
   for that municipality.

The iOS backend uses SQLite and is separate from the C# Aspire/SQL Server solution.

See the [data model](../erd/ERD.md) and [API reference](../api/API-ENDPOINTS.md).
