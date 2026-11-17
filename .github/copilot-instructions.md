# Municipal Elections repository guidance

- Keep `COMP4976-MunicipalElections-CSharp/MEMS.sln` on .NET 10. AppHost owns
  local orchestration; ServiceDefaults owns server observability/discovery;
  Data owns SQL Server EF/Identity persistence; Shared owns browser-safe DTOs;
  Api owns REST/security; Admin owns Interactive Server components.
- Preserve `MunicipalElections.Web`, its SQLite migrations, JWT API, Azure
  configuration, original documentation, and `COMP4977-MunicipalElections-iOS`.
  Never connect the legacy backend to the MEMS SQL Server database.
- Coordinate public Blazor and SwiftUI changes with their owner. Keep public
  voting selections in browser `sessionStorage`; do not add selection write
  endpoints, SQL persistence, or Firestore to the C# public application.
- Reuse `ElectionRepository`, shared request validation, and current Identity
  assignments. Tenant IDs in routes/bodies are selectors, not authorization.
  Keep EF tenant query filters/write guards enabled for application operations.
- In Blazor circuits, use fresh scopes/DbContexts per operation. Validate current
  role, assignment, security stamp, and expiry on every sensitive operation.
- Keep credentials in user secrets/environment or an appropriate secret store.
  Never log credentials, tokens, connection strings, request bodies, or sensitive
  EF parameter values. Automatic migrations/seeding are opt-in Development only.
- Keep image size/type/decode/path restrictions, concurrency checks, antiforgery,
  lockout, and secure cookies. Show safe errors without exception details.
- Verify relevant tests, then restore/build Debug and Release before reporting a
  milestone. Live SQL/Aspire tests require explicit environment configuration;
  report skipped/failed checks and deployment limitations honestly.
- Do not fabricate instructor feedback, AI-tool usage, screenshots, or test
  evidence. Do not commit, push, or deploy unless explicitly requested.

These instructions were added during Task 4 for future assistance. Their presence
does not establish that GitHub Copilot was used during development.
