# Deficiency Log: iOS App (Assignment 1 → Assignment 2)

Problems found in the Assignment 1 SwiftUI app and how Assignment 2 resolves them.
Paths are relative to `MunicipalElections/MunicipalElections/`.

## Resolved

| # | Issue | Original problem (A1) | Fix (A2) | Phase |
|---|-------|-----------------------|----------|-------|
| 1 | No project structure | All 13 source files sat loose in the Xcode project root, outside the app folder, with views, view models, models and networking mixed together. | Reorganized into `Models/`, `Services/API`, `Services/Auth`, `Services/Firebase`, `ViewModels/`, `Views/` (grouped by feature) and `Views/Components/`. | 1 |
| 2 | Firebase never initialized | The FirebaseAuth/FirebaseFirestore packages were linked, but `FirebaseApp.configure()` was never called, so the first Auth or Firestore call would crash. | An `AppDelegate` (via `@UIApplicationDelegateAdaptor`) calls `FirebaseService.configure()` at launch. Firestore is reached through `FirebaseService.firestore`. | 1 |
| 3 | Views bypassed MVVM | `MunicipalityListView` and `MunicipalityDetailView` were declared inside `ContentView.swift` and called `APIClient` directly from `@State`. | Split into `Views/Municipalities/` and backed by `MunicipalityListViewModel` / `MunicipalityDetailViewModel`. | 1 |
| 4 | Auth requests built in the view model | `AuthViewModel` built the request DTOs and called the API itself, with no auth service layer. | New `Services/Auth/AuthService.swift` owns the `/api/auth` DTOs and calls. `AuthViewModel` only handles UI state. | 1 |
| 5 | Pull-to-refresh broke the directory | Refreshing set `isLoading`, which swapped the `List` for a full-screen spinner. That removed the `.refreshable` view and cancelled the refresh. | The full-screen spinner shows only on the first load (`showsInitialLoading`). Refresh keeps the list on screen. | 1 |
| 6 | Stale rows when changing municipality | After switching the municipality filter, the previous city's candidates stayed visible until the new request finished. | Candidates are cleared when the filter changes, so the loading state shows instead of stale data. | 1 |
| 7 | One error hid the whole screen | A failed request (e.g. a failed "Add to guide") replaced the entire directory or voting guide with an error message, even when data was already loaded. | When data exists, errors appear in an inline `ErrorBanner` or list row with Retry. Full-screen errors are only for empty screens. | 1 |
| 8 | Candidate photos ignored in the directory | Directory rows always showed a generic SF Symbol even though the API returns `photoUrl`. | Rows show the real photo through the reusable `CandidatePhotoView`, with a placeholder when it's missing. | 1 |
| 9 | Detail photo could overflow and spin forever | `scaledToFill()` with only a height frame could make the image wider than the screen. A failed image load showed a `ProgressView` forever. | `CandidatePhotoView` draws the image in an overlay on a sized background, so it can't change the layout, and shows a placeholder on failure. | 1 |
| 10 | Contact info not tappable | The website and email were plain `Text`. Websites stored without a scheme (e.g. `johndoe.ca`) couldn't be opened. | Shown as `Link`s (`https://` added when missing, `mailto:` for email). The section is hidden when both are empty. | 1 |
| 11 | Password hint not enforced | Register said "uppercase, lowercase, number, symbol" but only checked length ≥ 6, so users got a server validation error after submitting. | Client-side check matching ASP.NET Identity's default rules, with live feedback listing what's missing. | 1 |
| 12 | Weak email validation | Any string containing `@` passed as an email. | Pattern check (`name@domain.tld`) and a 100-character limit, with an inline message on Register. | 1 |
| 13 | Raw system error text | Network and decoding failures showed messages like "The data couldn't be read because it is missing." | `APIError` maps `URLError` codes (offline, timeout, host unreachable, TLS), 401/403/404/5xx and decoding failures to plain-language messages. | 1 |
| 14 | Force-unwrapped URL building | `APIClient.send` force-unwrapped `URLComponents` and `components.url`. | Uses a guard that throws a friendly `APIError` instead. | 1 |
| 15 | Login/Register clipped in landscape | Fixed `VStack` + `Spacer` layouts had no scrolling, so fields and buttons were hidden in landscape or behind the keyboard. | Wrapped in `ScrollView` with `.scrollDismissesKeyboard`, keyboard focus chaining (Next → Go) and a readable max width on iPad. | 1 |
| 16 | Deprecated / missing form APIs | Used `.navigationBarHidden`, `.autocapitalization` and had no `textContentType`, so password AutoFill didn't work. | Replaced with `.toolbar(.hidden, for:)` and `.textInputAutocapitalization`, and added `textContentType` (`.username`, `.password`, `.newPassword`). | 1 |
| 17 | Position data dropped | The API returns `positionId` and `position.seatsAvailable`, but the `Candidate` model didn't decode them. | Added a `Position` model and optional `positionId`/`position` on `Candidate` (needed for seat limits). | 1 |
| 18 | Voting guide rows were dead ends | Guide entries couldn't be tapped to see the candidate. | Rows link to `CandidateDetailView`. | 1 |
| 19 | JWT stored in plain text | The token was kept in `UserDefaults` via `@AppStorage("authToken")`, readable in unencrypted app backups. | `KeychainService` stores the JWT and email as device-only Keychain items. Old UserDefaults values are wiped on first launch. `APIClient` attaches `Authorization: Bearer` for authorized requests. | 2 |
| 20 | Token expiry never explained | A 401 silently cleared `UserDefaults`. The "log in again" message was set on a screen that was then replaced by the login prompt, so users never saw it. | `AuthSession` handles expiry in one place: it reads the JWT `exp` claim, runs a timer, re-checks when the app returns to the foreground, checks before each authorized request, and treats a server 401 as expired. All of these log out and show a "Session expired" alert. | 2 |
| 21 | Auth state scattered | Seven views and `VotingGuideManager` read and wrote the `authToken`/`userEmail` keys directly. | One `AuthSession` environment object is the single source of truth. Views read `session.isLoggedIn`/`email`, and `VotingGuideManager` no longer touches UserDefaults. | 2 |
| 25 | No identity for Firestore | Only the API knew who the user was, so Firestore rules had no way to identify them. | After API login/register, `FirebaseAuthService` signs in to Firebase Auth with the same email/password (creating the user the first time). The session is only saved when both succeed. Logout signs out of both. | 2 |
| 26 | Instant logout | One tap logged the user out with no confirmation. | Profile asks for confirmation before logging out. | 2 |

## Open (scheduled for a later phase)

| # | Issue | Original problem (A1) | Planned fix | Phase |
|---|-------|-----------------------|-------------|-------|
| 22 | No home municipality | Users browse every city. There's no saved preference and API calls aren't scoped to a city. | Municipality picked at first login, stored in Firestore `users/{email}`, and every API call scoped to it. | 3 |
| 23 | Voting guide depends on an A1-only endpoint | `/api/savedcandidates` exists only in the A1 web app, not the A2 API. | Replaced by Firestore `CandidateSelections` CRUD. | 4 |
| 24 | Fixed sizes / iPhone-only layout | Hard-coded frames (photo height 300, etc.) and `NavigationStack` everywhere, even on iPad. | Size classes, adaptive grids and `NavigationSplitView` on iPad. | 6 |
