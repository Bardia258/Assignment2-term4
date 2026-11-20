# Agent Plan: COMP4977 Assignment 2 (iOS)

Resume point for future sessions. Read fully before doing anything.

## Context
This is BCIT COMP4977 Assignment 2: our Assignment 1 SwiftUI app (municipal elections),
upgraded. It lives at COMP4977-MunicipalElections-iOS/. Due Friday Nov 20, 11:59 pm.
I work with a partner. He owns the ASP.NET API/auth (A1 app deployed on Azure, source at
COMP4976-MunicipalElections-CSharp/MunicipalElections.Web). I own the iOS app: Firebase,
screens and UI/UX.

Done and committed on feature/ios-a2:
- Phase 1: MVVM folders (Models, Services/{API,Auth,Firebase}, ViewModels, Views), A1 bug
  fixes, Firebase configured at launch, docs/DEFICIENCY-LOG.md.
- Phase 2: Keychain JWT storage, AuthSession, Firebase Auth sign-in mirrored from API
  login/register, token expiry handling (timer, foreground, pre-request, 401).
- Phase 3: first-login city picker, city saved in Firestore users/{email}, city shown
  app-wide, API calls scoped to the city, reload on city change.
- Phase 4 (d04c66a): Firestore CandidateSelections CRUD with {email}_{candidateId} IDs,
  My Candidate Selections screen, firestore.rules (owner-only, notes <= 500).

Firestore rules are published by the user in the console. Give them the full rules file
whenever it changes.

## Git rules (strict)
- Branch feature/ios-a2 only. Never commit to main. Never push.
- Do NOT commit until the user says so. After each phase, stop with: a summary, files
  changed, simulator test steps and a suggested commit message.
- When a commit is approved: check Package.resolved still pins firebase-ios-sdk at exactly
  10.29.0 (restore the committed copy if Xcode deleted it), stage the new files, and confirm
  nothing from COMP4976-MunicipalElections-CSharp/ or any build folder is staged.
- Never modify anything under COMP4976-MunicipalElections-CSharp/.

## Environment (do not change)
- Xcode 15.4, Swift 5.10, no Swift 6-only syntax. iOS deployment target 17.5.
- firebase-ios-sdk pinned EXACTLY to 10.29.0 (FirebaseAuth, FirebaseFirestore). Never
  upgrade it or touch the package entries. Use only 10.x APIs.
- project.pbxproj: edit only PBXGroup, PBXFileReference, PBXBuildFile and Sources entries
  (to add or move files). Never touch package entries, build settings or the Swift version.
- Disk space is low: reuse Xcode's existing build folder, never create scratch build dirs.
- Every phase must end with a clean build (zero errors).
- Test targets: iPhone 15 (same screen size as iPhone 16) and iPad Air simulators.

## Remaining phases (stop after each one)

### Phase 5: validation, errors, profile
1. JWT: send Authorization: Bearer on ALL API requests while logged in. Do NOT build the
   /api/auth/me session check yet; the partner hasn't deployed it. Leave a clearly marked
   TODO where it will go. (Later: call it at launch, log out with "Session expired" ONLY
   on 401; 404, 5xx and offline keep the session.)
2. Seat limits: block adding a selection when the user already has Position.seatsAvailable
   selections for that position in the current city. Message like "You've already chosen
   1 of 1 Mayor." Disable Add on candidate detail with that explanation. Show
   "x of N chosen" per position on the selections screen.
3. Validation: no duplicates (confirm the existing protection), notes at most 500 chars,
   required fields checked before any write.
4. Errors: friendly messages for Firestore connection errors, auth errors, token expiry,
   network failures and invalid or undecodable responses. Add about a 10 s timeout on
   offline Firestore writes with "You appear to be offline, try again" instead of an
   endless spinner.
5. Profile: user email, current city, total saved selections (current city, plus the
   all-cities total if easy), and a "Last synced" timestamp updated after each successful
   Firestore load or write.
6. Update docs/DEFICIENCY-LOG.md.

### Phase 6: adaptive layout
iPhone portrait/landscape, iPad portrait/landscape, adjusting on rotation without a restart.
- NavigationStack on iPhone; NavigationSplitView with a sidebar on iPad.
- Directory: 1 column on iPhone portrait; more info per row on iPhone landscape;
  adaptive LazyVGrid on iPad portrait; multi-column on iPad landscape.
- Detail: photo stacked on compact width, beside the info on regular width.
- Selections, Home, Profile, Login, Register and the city picker: no clipping or overlap,
  proper scrolling, in all 4 layouts.
- Use size classes, ViewThatFits, adaptive grids or GeometryReader. No hard-coded sizes.
- Rotation preserves navigation state, session, loaded Firestore data and in-progress edits.
- Closes DEFICIENCY-LOG row #24.

### Phase 7: documentation notes in docs/
- Architecture notes for the diagram (SwiftUI app, ASP.NET Core API, Azure App Service,
  Firebase Auth + Firestore) with a Mermaid diagram.
- How the municipality is stored (Firestore users/{email}) and how filtering works.
- Screenshot checklist: login, register, directory in all 4 layouts, details,
  selections, profile, Firebase project, Firestore collections, security rules.
- Finalize DEFICIENCY-LOG.md.
- Note the security trade-off: the Firebase Auth password is mirrored from the API login
  (a server-issued custom token would be better, but it needs the Admin SDK).
- Do NOT write the AI-usage document. The user writes it.

## Progress
- [x] Phase 5 (committed)
- [x] Phase 6 (committed)
- [ ] Phase 7
