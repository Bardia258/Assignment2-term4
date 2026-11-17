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

## Printable voting guide (Phase 5)

| # | A1 deficiency | A1 evidence | A2 resolution |
|---|---|---|---|
| 20 | The Print button used an inline `onclick="window.print()"` attribute, which needs inline script and breaks under a strict Content Security Policy. | `Views/VotingGuide/Print.cshtml` | `PrintButton` calls `window.print` through Blazor JS interop, and tells the user to use Ctrl+P / ⌘P if the browser refuses (`Components/PrintButton.razor`). |
| 21 | The printout had a generic "Municipal Elections Guide" heading with no municipality, repeated the election date under every candidate, and mixed candidates from different municipalities. | `Views/VotingGuide/Print.cshtml` | One title block: "&lt;Municipality&gt; municipal election", the election day, the print date, and the number chosen. Each printout covers one municipality's guide only (`Components/Ballot/PrintableGuide.razor`). |
| 22 | Picks printed as a flat list in the order added, with no grouping, seat counts, or sign of which positions were still empty. | `Views/VotingGuide/Print.cshtml` | Grouped by position in ballot order (Mayor, Councillor, School Trustee) with "Vote for up to N · x chosen". Positions with no pick print "No selection". |
| 23 | "Printed on" came from the server clock (`DateTime.Now` in the controller, the Azure server's time zone), so it could show the wrong day, and it used zero-padded days ("March 05"). | `Controllers/VotingGuideController.Print` (`ViewBag.PrintDate`) | The date comes from the voter's own browser (WebAssembly runs in the local time zone), formatted "November 17, 2026". |
| 24 | The print page could be opened with an empty guide and still offered "Print Now", printing a page that just said "No candidates selected". | `Views/VotingGuide/Print.cshtml` | An empty guide shows a message with a link to browse candidates, and no Print button. |
| 25 | No paper size or page-break rules: entries could split across pages, and the print CSS lived in an inline `<style>` that applied only to that one view, so Ctrl+P on other pages printed buttons and filters. | `Views/VotingGuide/Print.cshtml` (inline `@media print`) | Site-wide `@media print` in `wwwroot/css/app.css`: `@page { size: letter }`, half-inch margins, hidden navbar/buttons/filters/breadcrumbs, and no page breaks inside a position. Checked with Chrome's PDF print engine: a full Burnaby guide (11 picks) fits on one Letter page, both from the print view and with Ctrl+P on the guide page. |

## Home, municipality overview, theme, validation and logging (Phase 6)

| # | A1 deficiency | A1 evidence | A2 resolution |
|---|---|---|---|
| 26 | The home page hero hard-coded the election date ("Prepare for the October 17, 2026 election"), so it would be wrong for any other election. | `Views/Home/Index.cshtml` | The date comes from the municipality data. It's shown only when every municipality votes on the same day, otherwise the hero stays generic (`Pages/Home.razor`). |
| 27 | Election dates were shown in bold red (`text-danger`), the colour used for errors, and zero-padded ("October 07"). | `Views/Shared/_MunicipalityCard.cshtml`, `Views/Municipalities/Details.cshtml` | Normal emphasis ("Election day: October 17, 2026"), no zero padding. |
| 28 | The municipality badge's logo fallback used an inline `onerror="..."` script (breaks under a strict Content Security Policy). | `Views/Shared/_MunicipalityBadge.cshtml` | `MunicipalityBadge` falls back to initials with Blazor's `@onerror`, with no inline script (`Components/Municipalities/MunicipalityBadge.razor`). |
| 29 | The positions/seats table listed positions in database order. | `Views/Shared/_PositionSeats.cshtml` | Ballot order (Mayor, Councillor, School Trustee) via `PositionOrder`. If positions fail to load, only that table shows a short note and the rest of the page still works (`Components/Municipalities/PositionSeats.razor`). |
| 30 | The dark mode toggle changed its own text and accessible name ("Dark mode" ↔ "Light mode"), so screen-reader users heard a different control after pressing it. | `Views/Shared/_ThemeToggle.cshtml`, `wwwroot/js/theme.js` (`updateButtons`) | The label stays "Dark mode", and `aria-pressed` reports whether it's on. Like A1, the saved theme still applies before the first paint, and it also follows the system setting and other tabs (`Components/ThemeToggle.razor`, `wwwroot/js/theme.js`). |
| 31 | The public navbar linked to "Administration" on every page. | `Views/Shared/_Layout.cshtml` | The public site has no admin link. It shows only that municipality's links (Overview, Candidates, My voting guide) and a guide count. |
| 32 | The navbar collapsed at the `sm` breakpoint, so five links plus the theme toggle wrapped awkwardly on tablets, and the footer still had template text ("© 2026 - MunicipalElections.Web"). | `Views/Shared/_Layout.cshtml` | The navbar collapses below `lg` and links never wrap mid-label (checked at 390, 768 and 1280 px with no horizontal scrolling on any page). The footer explains where the voting guide is stored. |
| 33 | The candidate search had no length validation. Any `searchString` went straight into a database `ToUpper().Contains()` query. | `Controllers/CandidatesController.Index`, `Services/CandidateService.SearchCandidatesAsync` | The search box is validated with DataAnnotations using the same 100-character limit as the API's `CandidateQuery.Name`. It shows a Bootstrap `is-invalid` message with `aria-invalid`, and an invalid search is never applied or put in the URL (`Components/Candidates/CandidateSearchForm.cs`, `CandidateDirectory.razor`). |
| 34 | Template CSS overrode Bootstrap's colours and form validation styles (`.valid.modified`, `.invalid`, `.validation-message` in plain red/green outlines). | `wwwroot/css/site.css` (A1) and the Blazor template copy | Template overrides removed. Bootstrap 5.3 colours and `.is-invalid` / `.invalid-feedback` are used, via `BootstrapFieldCssClassProvider`. |
