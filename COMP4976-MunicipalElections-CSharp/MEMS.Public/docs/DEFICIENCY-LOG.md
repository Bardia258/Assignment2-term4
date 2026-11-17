# Assignment 1 deficiencies resolved in the Assignment 2 public site

Running list for the Deficiency Resolution Report. Each entry names the Assignment 1 (A1) problem,
where it is in the A1 code (`MunicipalElections.Web/`, kept read-only as reference), and how the
Blazor WebAssembly public site (`MEMS.Public/`) resolves it.

## Voting guide (Phase 4)

| # | A1 deficiency | A1 evidence | A2 resolution |
|---|---|---|---|
| 1 | One guide shared by every municipality: all saved ids lived under a single `"VotingGuide"` session key, so Burnaby and Surrey picks were mixed together. | `Controllers/VotingGuideController.cs` (`Session.Get<List<int>>("VotingGuide")`) | One guide per municipality in browser sessionStorage under `ballot:{slug}` (`Services/Ballot/BallotSessionService.cs`). |
| 2 | No selection limits: a voter could add any number of candidates for a position (e.g. five Mayors). | `VotingGuideController.Add` only checks for duplicates | `AddAsync` enforces `PositionResponse.SeatsAvailable`; once a position is full its other candidates show a disabled "&lt;Position&gt; is full" button with an explanation (`Components/Ballot/BallotToggleButton.razor`). |
| 3 | The guide was kept in server-side session (cookie plus server memory), so voter choices were held on the server. | `Program.cs` (`AddSession`/`UseSession`), `VotingGuideController.cs` | Selections stay only in the browser tab's sessionStorage and are never sent to any API (checked in the browser: no requests besides static files). |
| 4 | Add and Remove changed state through GET links (`<a asp-action="Add">`). Link prefetchers or crawlers could change the guide, and a malicious page could link a user to these URLs. | `Views/Candidates/Index.cshtml`, `Views/Candidates/Details.cshtml`, `Views/VotingGuide/Index.cshtml` | No URL changes the guide. Add/Remove are client-side button actions. |
| 5 | The "+ Add to Guide" button never showed whether a candidate was already chosen, and there was no way to remove someone from the directory or profile page. | `Views/Candidates/Index.cshtml`, `Views/Candidates/Details.cshtml` | A toggle button shows "+ Add to guide" or "✓ In your guide · Remove" on cards and the profile page. Section headings show "x of N in your guide", and the navbar shows a guide count. |
| 6 | Every Add/Remove reloaded the page. Remove always redirected to the guide, losing the voter's place. | `VotingGuideController.Add` (Referer redirect), `VotingGuideController.Remove` (`RedirectToAction("Index")`) | Updates happen in place without navigation. Keyboard focus is kept on the button, or moved to a status message when the clicked item disappears. |
| 7 | One database query per saved candidate when showing the guide (N+1 queries). | `VotingGuideController.Index` and `Print` (loop calling `GetCandidateByIdAsync`) | The guide loads the municipality's positions and candidates with two parallel tenant-scoped API calls (`Components/Ballot/VotingGuide.razor`). |
| 8 | Candidates who were deleted stayed in the session list forever, silently. | `VotingGuideController.Index` skips nulls but never removes the id | `ReconcileAsync` removes candidates who are no longer running, updates changed positions, and the guide tells the voter what was removed. |
| 9 | The guide was a flat list with no grouping, seat counts, or guidance on what's left to choose. | `Views/VotingGuide/Index.cshtml` | Grouped by position in ballot order with "x of N chosen" and "You can choose N more · Browse &lt;Position&gt; candidates" links. |
| 10 | Stored session data was trusted as-is. | `Services/SessionExtensions.cs` (`JsonSerializer.Deserialize` with no validation) | Stored JSON is versioned and validated on read: malformed values, invalid ids, duplicates, excess entries, and picks over a seat limit are discarded. If storage is blocked, the guide keeps working in memory and shows a notice. |
| 11 | No confirmation or bulk action. Clearing meant removing candidates one by one. | `Views/VotingGuide/Index.cshtml` | "Clear guide" with an inline confirmation. |

## Candidate profile page (Phase 2)

| # | A1 deficiency | A1 evidence | A2 resolution |
|---|---|---|---|
| 12 | Candidate website rendered straight into `href` with no validation. A `javascript:` value would run script (stored XSS). | `Views/Candidates/Details.cshtml` (`<a href="@Model.Website">`) | Only absolute `http`/`https` URLs become links, and emails must be well-formed (`Services/SafeLinks.cs`). |
| 13 | External website links used `target="_blank"` without `rel="noopener"`, so the opened site could control the original tab. | `Views/Candidates/Details.cshtml` | `rel="noopener noreferrer nofollow"`, plus screen-reader text "(opens in a new tab)". |
| 14 | An unknown candidate id returned a bare 404 page with no way back. | `Controllers/CandidatesController.Details` (`return NotFound()`) | A friendly "Candidate not found" page with a link back to that municipality's candidates. |
| 15 | Missing photos showed a large grey "No Photo" block. Image `alt` repeated the name shown right next to it. | `Views/Candidates/Index.cshtml`, `Details.cshtml` | Initials avatar with decorative `alt=""`. A broken photo URL falls back to initials. |

## Candidate directory search and filters (Phase 3)

| # | A1 deficiency | A1 evidence | A2 resolution |
|---|---|---|---|
| 16 | The position dropdown listed every municipality's positions together (repeated "Mayor", "Councillor"…), so a voter could filter by another city's position. | `Controllers/CandidatesController.Index` (`GetAllPositionsAsync()`) | Only the current municipality's positions, in ballot order (Mayor, Councillor, School Trustee), each with a candidate count (`Components/Candidates/PositionFilter.razor`). |
| 17 | Searching needed a form submit and a full page reload. A filter that matched nothing showed only "No candidates found". | `Views/Candidates/Index.cshtml` | Results update as you type. The visible count ("Showing 1 of 13 candidates") is announced to screen readers, and there's a "Clear filters" button. |
| 18 | Profile excerpts were cut to a single line (`text-truncate`). | `Views/Candidates/Index.cshtml` | Three-line excerpt (`.text-clamp-3`). |
| 19 | Candidates weren't grouped by position, and seats per position weren't shown in the directory. | `Views/Candidates/Index.cshtml` | Directory grouped by position with "Vote for 1" / "Vote for up to N" badges. |
