# iOS codebase

COMP4977 Assignment 2 uses a SwiftUI application, the existing Azure-hosted
ASP.NET Core API with SQLite/JWT, and Firebase Authentication/Firestore for
user profiles and saved candidate selections. It does not use `MEMS.Api` or
the C# assignment's SQL Server database.

| Reference | Contents |
| --- | --- |
| [Architecture](architecture/ARCHITECTURE.md) | SwiftUI/MVVM, API, Azure and Firebase request paths. |
| [Data model](erd/ERD.md) | SQLite election ERD and Firestore document fields/ownership. |
| [API](api/API-ENDPOINTS.md) | Routes, filtering, JSON examples, JWT and backend configuration. |

## Open the app

On a Mac, open `MunicipalElections/MunicipalElections.xcodeproj` in Xcode and
resolve Swift packages. The project targets iOS 17.5, uses Swift 5 and pins
Firebase iOS SDK 10.29.0. Select a simulator or configure signing for a device.
The SwiftUI sources are organized under the inner `MunicipalElections` folder
into Models, Services, ViewModels and Views.

The existing `GoogleService-Info.plist` must match the Firebase project and app
bundle ID `BCIT.MunicipalElections`. In that Firebase project, enable Email/
Password Authentication and Firestore, and publish the repository's
[`firestore.rules`](../firestore.rules). Mobile Firebase configuration is not an
Admin service-account credential; never add a private service-account key here.

The API client currently targets:

`https://assignment2-term4-dbe7c6evbpajetbf.westus3-01.azurewebsites.net/`

The current backend was deployed to the Linux Web App `assignment2-term4`.
Register/login with the API account; the app also signs
into or creates the corresponding Firebase email/password account. Existing
Firebase accounts must use the matching password. The two account systems do
not automatically synchronize password changes or resets.

On first login choose a municipality. The choice is saved in Firestore and can
be changed in Profile; changing it reloads directory/selection data. Candidate
notes and selections are saved in Firestore, not through the old Voting Guide API.

## Run or host the API

Open `MunicipalElections.slnx` with the .NET 10 SDK. Only
`MunicipalElections.Web` is deployed for iOS. Docker, Aspire and SQL Server are
not required. Development startup migrates and adds sample election data.
From this course folder:

```powershell
dotnet restore MunicipalElections.slnx
dotnet build MunicipalElections.slnx
dotnet run --project MunicipalElections.Web --launch-profile https
```

Local HTTPS origin: `https://localhost:7105`. A physical iPhone cannot use the
Windows host's `localhost`; the normal app configuration uses Azure. See the
[API configuration](api/API-ENDPOINTS.md#configuration-and-deployment) before
running or publishing, including persistent SQLite/image paths and JWT settings.

The current app was deployed manually from a `linux-x64`, framework-dependent
.NET 10 publish ZIP. The repository's existing GitHub workflow still targets the
previous Windows app; it has not been changed for this new deployment.
Production does not migrate or seed on HTTP startup. Its SQLite connection uses
`Data Source=/home/data/mems-ios/app.db`, with images under
`/home/data/mems-ios/uploads`, configured in Azure environment variables.
After backing up any existing database/images, run migrations separately in
the app's SSH terminal:

```bash
cd /home/site/wwwroot
dotnet MunicipalElections.Web.dll --migrate-database
```

Optional `--seed-demo-data` runs separately and adds missing fictional sample
data. Run maintenance commands in the deployed app directory with its production
configuration available; do not replace the existing SQLite database on publish.

## Current behavior and limitations

- Keychain stores the API session; API login remains JWT-based. Firebase Auth
  authenticates Firestore independently. `/api/auth/me` checks the current API
  account; it is not a Firebase token exchange.
- Directory requests include the selected municipality. Profile changes reload
  scoped candidates/positions and hide selections from the previous municipality.
- Firestore selections support create/read/edit notes/delete, duplicate checks,
  500-character notes and position-seat checks. Rules enforce authenticated email
  ownership and document structure. Seat capacity comes from the API and is
  checked by the app; Firestore rules do not independently verify that capacity.
- Phone navigation, iPad split navigation and adaptive layouts are implemented
  in source. Windows cannot verify the Xcode build or the required phone/tablet
  portrait/landscape behavior. The old native test sources need updating.

The moved API previously passed restore, Debug/Release builds and isolated local
HTTP checks for registration/login, JWT sessions, filtering, validation and image
responses. The new Azure deployment has since returned HTTP 200 for the service
root/directory: three municipalities, 12 candidates including three mayors, and
correct Burnaby candidate/position filtering. `/api/auth/me` returns 401 without
a token, as expected. Authenticated login, live Firebase and the native app have
not been verified against this deployment. Rebuild the iOS app to use the new URL.

One known seed mismatch remains: Pitt Meadows currently seeds **6 Councillor
seats**, while the iOS PDF specifies **5**. Existing seed rows are preserved, so
updating the source alone would not change an already deployed value. No code or
database values were changed in this documentation task.

Repository: [Assignment2-term4](https://github.com/Bardia258/Assignment2-term4).
