# Langoose Agent Guide

## Project Shape

- `apps/api`: ASP.NET Core Web API on .NET 10 with controller-based JSON endpoints.
- `tests/Langoose.Api.Tests`: xUnit-based .NET test project that exercises MVP behaviors and should be discoverable in Test Explorer.
- `apps/web`: React 19 + TypeScript + Vite single-page app with plain CSS.
- Persistence is a local JSON file store, not a database-backed system.

## Preferred Repo Layout

- Keep application code under `apps/`.
- Keep .NET test projects under a parallel root `tests/` folder rather than nesting them inside `apps/api`.
- For this repo, prefer a future shape like:
  - `apps/api`
  - `tests/Langoose.Api.Tests`
  - optionally `tests/Langoose.Api.UnitTests` and `tests/Langoose.Api.FunctionalTests` if the suite grows and the test types need to run separately.
- If tests stay together temporarily, still organize them internally by test type and by the API class/service they exercise.

## Working Agreements

- Preserve the MVP architecture. Prefer small changes inside the current React SPA and ASP.NET Core service/controller structure before introducing new layers, packages, or infrastructure.
- Keep business rules on the backend when they affect grading, dictionary visibility, imports, or scheduling.
- Do not introduce a database, ORM, background worker, or production-grade auth unless the task explicitly asks for that shift.
- Normalize line endings. Respect `.gitattributes`, avoid accidental whole-file line-ending churn, and prefer repository-consistent endings when editing files.
- Before finishing a change, normalize edited files to the line endings required by .gitattributes and run git diff --check to catch EOF and whitespace issues.
- Protect non-ASCII product text. When a file contains Russian or other non-ASCII user-facing text, preserve it as valid UTF-8 or use explicit C# `\u` escapes if tooling might corrupt the literal; do not replace such text with `?`, rely on shell-default encodings, or finish a change while mojibake or placeholder characters remain in source files.
- Prefer a standard maximum line length of 120 characters unless an existing file or construct clearly justifies an exception.
- For backend work, validate repository rules against all source and test files under `apps/api` and `tests/Langoose.Api.Tests`, not only the files directly edited in the current change.
- Treat Visual Studio `.sln` project-entry lines as a narrow exception to the 120-character rule when the solution format requires a longer line.
- In C# code, prefer one top-level type per file. Split files when a class, record, enum, or interface would otherwise share a file with another top-level type.
- In C# code, prefer primary constructors where they keep the type simpler and fit the repo's target framework and style.
- In C# code, add a separating blank line before `if` and `return` statements when it improves block readability, especially after variable setup or guard-condition preparation.
- In C# code, follow the same readability rule for functional block starters such as `try`, `for`, `foreach`, `while`, and `switch` when they follow setup code.
- In C# code, prefer `record` or `record struct` for POCO-style data carriers, request/response models, and immutable value-like objects when reference-identity semantics are not required.
- In React code, keep components pure and avoid mutating values during render.
- In React code, prefer deriving values during render over storing redundant or duplicated state.
- In React code, prefer event handlers for user-driven logic and use effects only when synchronizing with an external system.
- In TypeScript code, keep strict typing on and prefer precise request/response types over `Record<string, unknown>` or overly broad casts when practical.
- Prefer plain React state and existing patterns in [App.tsx](D:\Projects\langoose\apps\web\src\App.tsx) over adding a state library.
- Prefer plain CSS in [styles.css](D:\Projects\langoose\apps\web\src\styles.css) over CSS-in-JS or a component library.

## Product Invariants

- The product is for Russian speakers learning English. Preserve that framing in UX copy and data examples.
- Visible dictionary items collapse base and custom entries by normalized English text. Avoid changes that reintroduce duplicate visible terms.
- Quick add and CSV import should merge duplicate custom entries rather than creating extra visible records.
- Terms already present in the base dictionary should stay as base-backed visible items instead of becoming duplicate custom rows.
- CSV import is file-content based and strict: required columns must begin with `English term`, `Russian translation(s)`, `Type`, with only optional `Notes` and `Tags` after them.
- Malformed CSV input should fail without partial import.
- Study grading is intentionally tolerant: accept exact matches, known variants, missing articles, inflection mismatches, and minor typos.
- Clearing custom data must remove user-owned content and progress, but it should not revoke active session tokens.

## Data Store Notes

- The API persists to `App_Data/store.json` through [FileDataStore.cs](D:\Projects\langoose\apps\api\Infrastructure\FileDataStore.cs).
- Treat `App_Data`, `bin`, `obj`, `.vs`, and `node_modules` as runtime/generated artifacts unless the task is explicitly about them.
- Be careful not to depend on incidental contents of the checked-in local store when implementing features or tests.

## Preferred Validation

- Backend checks:
  - current API tests: `dotnet test tests/Langoose.Api.Tests/Langoose.Api.Tests.csproj /p:RestoreConfigFile=D:\Projects\langoose\apps\api\NuGet.Config`
- Backend build:
  - `dotnet build apps/api/Langoose.Api.csproj --configfile D:\Projects\langoose\apps\api\NuGet.Config`
- Frontend build:
  - `npm run build` from `D:\Projects\langoose\apps\web`

## Change Heuristics

- For API work, inspect `Controllers`, `Services`, and `Models` together before editing behavior.
- For study-flow changes, review both [StudyService.cs](D:\Projects\langoose\apps\api\Services\StudyService.cs) and the discoverable xUnit tests under [tests/Langoose.Api.Tests](D:\Projects\langoose\tests\Langoose.Api.Tests).
- For dictionary/import changes, review both [DictionaryService.cs](D:\Projects\langoose\apps\api\Services\DictionaryService.cs) and [DictionaryController.cs](D:\Projects\langoose\apps\api\Controllers\DictionaryController.cs).
- For frontend work, keep the API contract in sync with [api.ts](D:\Projects\langoose\apps\web\src\api.ts).
- For .NET test-organization work, use the `langoose-dotnet-testing` skill in `.codex/skills`.
- For React and TypeScript frontend decisions, use the `langoose-react-typescript` skill in `.codex/skills`.
- For request/response contract changes across backend and frontend, use the `langoose-api-contracts` skill in `.codex/skills`.
- For grading, scheduling, and card-selection changes, use the `langoose-study-engine` skill in `.codex/skills`.
- For CSV, duplicate normalization, and dictionary visibility rules, use the `langoose-dictionary-imports` skill in `.codex/skills`.
- For Dockerfiles, Compose, containerized local development, and file-store persistence decisions, use the `langoose-docker` skill in `.codex/skills`.
