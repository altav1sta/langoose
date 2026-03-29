# Langoose Agent Guide

## Project Shape

- `apps/api`: ASP.NET Core Web API on .NET 10 with controller-based JSON endpoints.
- `tests/Langoose.Api.Tests`: xUnit-based .NET test project that exercises MVP behaviors and should be discoverable in Test Explorer.
- `apps/web`: React 19 + TypeScript + Vite single-page app with plain CSS.
- Persistence is PostgreSQL-backed through EF Core.

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
- Do not introduce a different persistence stack, background worker, or production-grade auth unless the task explicitly asks for that shift.
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
- Before declaring a task done, verify the real acceptance path the user will exercise. For Docker, persistence, startup,
  and auth changes, prefer a live end-to-end check over code-only confidence.
- Clean up machine-local and generated artifacts created during the task, including `.dotnet`, `bin`, `obj`, `.vs`, and
  any local config that does not belong in the repo.

## GitHub Workflow

- Before answering "what's next" or starting work, check the live GitHub state first rather than relying on an older
  plan or memory.
- Treat the GitHub Project `Langoose MVP` as the source of truth for roadmap status.
- Keep issue, epic, and PR status aligned with the real state of the work.
- Use one branch per issue or one tightly related chunk of work whenever practical.
- Prefer one PR per issue whenever practical. If a task clearly belongs in one focused PR, do not split it
  unnecessarily.
- Use squash merge into `main`.

### Branch Naming

- Use `docs/...` for documentation and repo guidance changes.
- Use `infra/...` for CI, Docker, deployment, and workflow changes.
- Use `feat/...` for product or implementation work.
- Use `fix/...` for bug fixes.
- Use `chore/...` for maintenance that does not fit the categories above.

### Issue And Epic Flow

- When starting an issue, move that issue to `In Progress`.
- If the issue belongs to an epic that is still not active, move the epic to `In Progress` when the parent work is
  meaningfully underway.
- When opening a PR, move the issue to `In Review`.
- After merge, move the issue to `Done`.
- Close child issues automatically from PRs when appropriate by using `Closes #...` in the PR body.
- Close epics manually only after confirming the grouped outcome is actually complete.
- When a new task truly belongs under an existing epic, add it as a real GitHub child issue rather than only mentioning
  the epic in plain text.

### Labels And Planning

- Preserve the existing labels unless the user explicitly asks to change the labeling system.
- Use area labels such as `backend`, `frontend`, `infra`, and `db` to reflect the affected system areas.
- Use planning labels such as `mvp`, `post-mvp`, `bug`, `tech-debt`, and `blocked` to reflect roadmap context.
- Keep milestones and project membership aligned with the issue's role in the roadmap.

### Pull Request Conventions

- PR titles should describe the actual change, not process commentary.
- PR bodies should include a short summary, the linked issue via `Closes #...` or `Refs #...`, and the validation that
  was run.
- Do not mix unrelated changes in one PR.

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

- The API persists through PostgreSQL and EF Core in [AppDbContext.cs](D:\Projects\langoose\apps\api\Infrastructure\AppDbContext.cs) and [PostgresDataStore.cs](D:\Projects\langoose\apps\api\Infrastructure\PostgresDataStore.cs).
- Treat `bin`, `obj`, `.vs`, `.dotnet`, and `node_modules` as runtime/generated artifacts unless the task is explicitly about them.
- Be careful not to depend on incidental contents of a local database volume when implementing features or tests.

## Preferred Validation

- Backend checks:
  - current API tests: `dotnet test tests/Langoose.Api.Tests/Langoose.Api.Tests.csproj /p:RestoreConfigFile=D:\Projects\langoose\apps\api\NuGet.Config`
- Backend build:
  - `dotnet build apps/api/Langoose.Api.csproj --configfile D:\Projects\langoose\apps\api\NuGet.Config`
- Frontend build:
  - `npm run build` from `D:\Projects\langoose\apps\web`
- Container verification for persistence/startup/auth changes:
  - `docker compose up -d postgres`
  - `docker compose up -d api --build`
  - verify `GET http://localhost:5000/health`
  - verify a real auth flow such as `POST /auth/email-sign-in`

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
- For Dockerfiles, Compose, containerized local development, and persistence wiring decisions, use the `langoose-docker` skill in `.codex/skills`.
