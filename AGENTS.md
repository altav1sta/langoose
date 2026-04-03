# Langoose Agent Guide

## Project Shape

- `apps/api`: backend solution root and backend-wide configuration boundary on .NET 10.
- `apps/api/src`: backend production-source area.
- `apps/api/src/Langoose.Api`: ASP.NET Core Web API with controller-based JSON endpoints.
- `apps/api/src/Langoose.Domain`: core persisted domain models and store abstractions.
- `apps/api/src/Langoose.Data`: EF Core, PostgreSQL persistence, and migrations.
- `apps/api/src/Langoose.Auth.Data`: auth persistence, Identity/OpenIddict EF Core wiring, and auth migrations.
- `apps/api/tests/Langoose.Api.Tests`: xUnit-based .NET test project that exercises MVP behaviors and should be discoverable in Test Explorer.
- `apps/web`: React 19 + TypeScript + Vite single-page app with plain CSS.
- Persistence is PostgreSQL-backed through EF Core.

## Preferred Repo Layout

- Keep application code under `apps/`.
- Treat `apps/api` as the backend boundary root for the solution, shared backend build config, and backend-local test organization.
- Keep backend-wide configuration files such as `Langoose.sln`, `NuGet.Config`, `Directory.Build.props`, `Directory.Build.targets`, and `Directory.Packages.props` at `apps/api/`.
- Keep production/backend code projects under `apps/api/src`.
- Keep API-owned .NET test projects under `apps/api/tests`.
- Reserve a repo-root `tests/` folder only for future repo-level e2e, system, or cross-app suites.
- For this repo, prefer a future shape like:
  - `apps/api/Langoose.sln`
  - `apps/api/NuGet.Config`
  - `apps/api/src/Langoose.Api`
  - `apps/api/src/Langoose.Domain`
  - `apps/api/src/Langoose.Data`
  - `apps/api/src/Langoose.Auth.Data`
  - `apps/api/tests/Langoose.Api.Tests`
  - optionally `apps/api/tests/Langoose.Api.UnitTests` and `apps/api/tests/Langoose.Api.FunctionalTests` if the suite grows and the test types need to run separately
  - optionally `tests/Langoose.E2E.Tests` or similar for repo-level end-to-end coverage
- If tests stay together temporarily, still organize them internally by test type and by the API class/service they exercise.

## Working Agreements

- Preserve the MVP architecture. Prefer small changes inside the current React SPA and ASP.NET Core service/controller structure before introducing new layers, packages, or infrastructure.
- Keep business rules on the backend when they affect grading, dictionary visibility, imports, or scheduling.
- Do not introduce a different persistence stack, background worker, or production-grade auth unless the task explicitly asks for that shift.
- For auth planning and implementation in this repo, keep [auth-mvp-decision.md](D:\Projects\langoose\docs\auth-mvp-decision.md) and [auth-m1-implementation-blueprint.md](D:\Projects\langoose\docs\auth-m1-implementation-blueprint.md) aligned with real repo guidance and implementation direction.
- Normalize line endings. Respect `.gitattributes`, avoid accidental whole-file line-ending churn, and prefer repository-consistent endings when editing files.
- Before finishing a change, normalize edited files to the line endings required by .gitattributes and run git diff --check to catch EOF and whitespace issues.
- When editing Markdown docs, verify relative links from the edited file's real directory rather than assuming repo-root paths. After doc changes, do a quick scan for broken sibling or nested `docs/...` links before finishing.
- Protect non-ASCII product text. When a file contains Russian or other non-ASCII user-facing text, preserve it as valid UTF-8 or use explicit C# `\u` escapes if tooling might corrupt the literal; do not replace such text with `?`, rely on shell-default encodings, or finish a change while mojibake or placeholder characters remain in source files.
- Treat seed files and other baseline content assets as especially sensitive to non-ASCII corruption. Inspect the actual file contents before finishing when they contain Russian or other non-ASCII text; do not trust terminal display alone.
- Prefer a standard maximum line length of 120 characters unless an existing file or construct clearly justifies an exception.
- For backend work, validate repository rules against all source and test files under `apps/api/src` and `apps/api/tests`, not only the files directly edited in the current change.
- Treat Visual Studio `.sln` project-entry lines as a narrow exception to the 120-character rule when the solution format requires a longer line.
- In C# code, prefer one top-level type per file. Split files when a class, record, enum, or interface would otherwise share a file with another top-level type.
- In C# code, keep patterns consistent within the same subsystem. If one entity or concern in a slice uses a dedicated configuration class, constant holder, or similar structural pattern, do not leave adjacent equivalent cases half-inline without a clear reason.
- In C# code, keep namespaces aligned with the current project and folder structure whenever files are added or moved. Do not leave mismatched namespaces behind after refactors.
- In C# code, do not leave unused `using` directives behind. Clean them up during the change instead of treating them only as a final polish step.
- For backend C# work, always do a manual pass over the changed source and test files for obvious unused `using` directives, even when you also run analyzer-based checks.
- For backend C# manual cleanup passes, use an explicit file-by-file flow: list the currently changed `.cs` files, include both modified tracked files and untracked new `.cs` files in that list, narrow to the files that still contain `using` directives, inspect each of those files directly, and only then report the sweep complete.
- If the user explicitly calls out a specific file, re-inspect that file directly in the current pass even if it was checked earlier. Do not rely on memory or an earlier partial pass for user-emphasized files.
- Do not let a broad repo-wide pass override special attention the user asked for on a specific file. User-emphasized files must appear explicitly in the checked-file report.
- Do not treat an earlier partial cleanup pass as evidence for a later full pass. A new full pass must rebuild the file list from the current working tree and re-check every file on that list.
- For unused `using` cleanup, do not infer correctness from how familiar, busy, or framework-heavy a file looks. Verify each import against symbols or extension methods actually used in that file.
- Do not claim a manual cleanup pass is complete without a proof artifact in the response: the exact checked-file list, the files changed by the pass, and the post-fix validation results.
- In dependency injection setup, do not keep redundant registrations. Register only the service forms the current code actually resolves, and remove duplicate `AddDbContext` or factory registrations when one of them is unused.
- In C# code, keep property blocks internally consistent within a type. Do not leave stray one-off blank lines between consecutive properties, and prefer flat property blocks over subjective semantic grouping.
- In C# code, prefer `""` over `string.Empty` for ordinary empty-string literals and default property values unless a specific API shape clearly benefits from `string.Empty`.
- In C# code, prefer `required` over fake empty-string defaults on mandatory entity and model properties. For entity and model strings and enums, set values explicitly at creation sites instead of hiding them behind property defaults.
- In C# entity models, prefer explicit assignment of IDs and timestamps at creation sites or through persistence configuration. Do not hide identity or time semantics behind automatic property defaults unless the default is a deliberate domain rule.
- In C# code, do not leave unexplained magic numbers in domain or behavior logic. Extract non-obvious numeric values into named constants or configuration so their purpose is visible at the call site.
- In C# code, prefer primary constructors where they keep the type simpler and fit the repo's target framework and style.
- In C# code, add a separating blank line before `if` and `return` statements when it improves block readability, especially after variable setup or guard-condition preparation.
- In C# code, follow the same readability rule for functional block starters such as `try`, `for`, `foreach`, `while`, and `switch` when they follow setup code.
- In C# code, use blank lines to separate distinct logic phases inside a method. Prefer grouping by purpose, such as setup, data loading, transformation, branching, and persistence, instead of treating long blocks as one uninterrupted sequence.
- In C# lambda expressions, prefer short parameter names such as `x` or `y` when the expression is simple and the role is obvious locally. Use more descriptive parameter names when the lambda is nested, has multiple parameters with non-obvious roles, or would otherwise become harder to read.
- In C# code, prefer `record` or `record struct` for POCO-style data carriers, request/response models, and immutable value-like objects when reference-identity semantics are not required.
- In React code, keep components pure and avoid mutating values during render.
- In React code, prefer deriving values during render over storing redundant or duplicated state.
- In React code, prefer event handlers for user-driven logic and use effects only when synchronizing with an external system.
- In TypeScript code, keep strict typing on and prefer precise request/response types over `Record<string, unknown>` or overly broad casts when practical.
- Prefer plain React state and existing patterns in [App.tsx](D:\Projects\langoose\apps\web\src\App.tsx) over adding a state library.
- Prefer plain CSS in [styles.css](D:\Projects\langoose\apps\web\src\styles.css) over CSS-in-JS or a component library.
- Before declaring a task done, verify the real acceptance path the user will exercise. For Docker, persistence, startup,
  auth, frontend/backend integration, or full-app questions, prefer a live end-to-end check over code-only confidence.
- For whole-app verification, treat the containerized stack in `compose.yml` as the default validation path instead of
  relying only on host-local `npm` or partial backend-only checks.
- When the repo has a Docker/Compose path for the frontend, use that path by default for frontend acceptance
  verification. Do not fall back to host-local `npm` build or runtime checks first unless Docker is unavailable, blocked,
  or the user explicitly asks for host-local validation.
- Clean up machine-local and generated artifacts created during the task, including `.dotnet`, `bin`, `obj`, `.vs`, and
  any local config that does not belong in the repo.
- Keep `Microsoft.EntityFrameworkCore.Design` package references lean. Do not widen `IncludeAssets` to runtime/content
  assets unless there is a proven need, because that can surface `BuildHost-*` generated folders in normal project
  output and pollute local project views.
- If startup seeding or repair logic can rewrite base content, verify that the seed source is correct before finishing. Do not ship a corrupted seed file that would overwrite existing base data on startup.

## GitHub Workflow

- Before answering "what's next" or starting work, check the live GitHub state first rather than relying on an older
  plan or memory.
- Treat the GitHub Project `Langoose MVP` as the source of truth for roadmap status.
- Keep issue, epic, and PR status aligned with the real state of the work.
- Follow the established repo workflow automatically. Apply the written rules instead of inventing extra workflow steps,
  duplicate requirements, or additional project tracking that the guide does not call for.
- Before any GitHub-side workflow mutation, verify the workflow checklist yourself: correct issue, correct branch from
  latest `main`, correct issue status, complete issue metadata, complete PR metadata when a PR exists, PR mergeable
  when relevant, and project tracking only where the repo flow actually requires it.
- Only stop to ask the user when the next workflow step is genuinely ambiguous, conflicts with the written repo flow,
  would change or bypass the normal issue/PR process, or would take a destructive Git action not already requested.
- Never start issue work on an unrelated existing branch. If the current branch is not the correct issue branch, stop, switch back to a clean latest `main`, and create the proper branch before making changes.
- Use one branch per issue or one tightly related chunk of work whenever practical.
- Start issue branches from the latest local `main` branch after updating it from `origin/main`. Before creating a
  branch, fetch `origin/main`, check out `main`, fast-forward `main`, and only then create the new branch from `main`.
- If work was accidentally started on the wrong branch, do not continue by improvising with merges, stash juggling, or cross-branch cleanup. Preserve the work if needed, then restart from a clean correct branch and reapply only the intended issue changes.
- Prefer one PR per issue whenever practical. If a task clearly belongs in one focused PR, do not split it
  unnecessarily.
- For large refactors or project-structure changes, sync the branch with the current `main` branch and resolve merge
  conflicts locally before calling the PR handoff complete.
- If a change moves projects, solution files, Dockerfiles, or config paths, update CI/workflow files in the same change
  and verify they still reference the correct locations.
- If a change moves projects, solution files, Dockerfiles, config paths, or backend directory layout, also verify
  design-time tooling paths and path-sensitive docs against the new structure. Do not treat green build/test/CI alone
  as proof that EF design-time commands, Docker notes, README paths, or other location-dependent guidance still work.
- Use squash merge into `main`.
- When describing remote branch state, do not rely only on local `origin/*` refs. Verify against the live remote or prune stale refs first if branch existence matters to the answer or workflow.

### Branch Naming

- Do not create branches with the `codex/` prefix for this repo.
- Use `docs/...` for documentation and repo guidance changes.
- Use `infra/...` for CI, Docker, deployment, and workflow changes.
- Use `feat/...` for product or implementation work.
- Use `fix/...` for bug fixes.
- Use `chore/...` for maintenance that does not fit the categories above.

### Issue And Epic Flow

- When creating or taking over an issue, make its metadata complete early: confirm project placement, status, labels, milestone, and assignee rather than leaving those for later cleanup.
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
- Keep milestones aligned with the issue's role in the roadmap. Track issues in the project unless the repo flow
  explicitly calls for something else.

### Pull Request Conventions

- PR titles should describe the actual change, not process commentary.
- PR bodies should include a short summary, the linked issue via `Closes #...` or `Refs #...`, and the validation that
  was run.
- Before reporting a PR as ready, make sure its metadata is complete enough for normal repo flow:
  - the linked issue is correct
  - the issue itself has the right labels, milestone, assignee, and project placement
  - the PR itself has the expected assignee, labels, milestone, and review state for the repo flow
- Before reporting a PR as ready, verify that GitHub shows it as mergeable. If it is conflicted, resolve that before
  treating the issue as handed off to review.
- Do not mix unrelated changes in one PR.
- When editing issue or PR bodies through the CLI, verify the final rendered text afterward. Do not assume shell escaping, backticks, or inline paths survived correctly.

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

- The API persists through PostgreSQL and EF Core in [AppDbContext.cs](D:\Projects\langoose\apps\api\src\Langoose.Data\AppDbContext.cs) for app data and [AuthDbContext.cs](D:\Projects\langoose\apps\api\src\Langoose.Auth.Data\AuthDbContext.cs) for auth data.
- Backend-wide restore and solution configuration belong at the `apps/api` level; keep project-specific runtime files such as `Program.cs`, `appsettings*.json`, and project Dockerfiles inside the relevant project under `apps/api/src`.
- The planned auth direction uses a separate auth database on the same PostgreSQL server, with its own migration stream, rather than mixing auth and app data in one database.
- Base dictionary seed content lives in [base-store.json](D:\Projects\langoose\apps\api\src\Langoose.Data\Seeding\Json\base-store.json) and is applied through [DatabaseSeeder.cs](D:\Projects\langoose\apps\api\src\Langoose.Data\Seeding\DatabaseSeeder.cs).
- Treat `bin`, `obj`, `.vs`, `.dotnet`, and `node_modules` as runtime/generated artifacts unless the task is explicitly about them.
- Be careful not to depend on incidental contents of a local database volume when implementing features or tests.

## Preferred Validation

- Backend checks:
  - current API tests: `dotnet test apps/api/tests/Langoose.Api.Tests/Langoose.Api.Tests.csproj /p:RestoreConfigFile=D:\Projects\langoose\apps\api\NuGet.Config`
- Backend build:
  - `dotnet build apps/api/Langoose.sln /p:RestoreConfigFile=D:\Projects\langoose\apps\api\NuGet.Config`
- Frontend build:
  - `npm run build` from `D:\Projects\langoose\apps\web`
- Container verification for persistence/startup/auth changes:
  - `docker compose up -d postgres`
  - `docker compose up -d api --build`
  - verify `GET http://localhost:5000/health`
  - verify the real auth flow for the branch, such as the current placeholder `POST /auth/email-sign-in` or the planned `POST /auth/sign-in` once it lands
- Default whole-app verification:
  - `docker compose up -d postgres`
  - `docker compose up -d api --build`
  - `docker compose up -d web --build`
  - verify `GET http://localhost:5000/health`
  - verify `GET http://localhost:5173`
  - if the change affects user-facing behavior, exercise the real browser-facing flow against the running stack rather
    than stopping at container startup alone

## Change Heuristics

- For API work, inspect `Controllers`, `Services`, and `Models` together before editing behavior.
- For study-flow changes, review both [StudyService.cs](D:\Projects\langoose\apps\api\src\Langoose.Api\Services\StudyService.cs) and the discoverable xUnit tests under [apps/api/tests/Langoose.Api.Tests](D:\Projects\langoose\apps\api\tests\Langoose.Api.Tests).
- For dictionary/import changes, review both [DictionaryService.cs](D:\Projects\langoose\apps\api\src\Langoose.Api\Services\DictionaryService.cs) and [DictionaryController.cs](D:\Projects\langoose\apps\api\src\Langoose.Api\Controllers\DictionaryController.cs).
- For frontend work, keep the API contract in sync with [api.ts](D:\Projects\langoose\apps\web\src\api.ts).
- For auth work, keep [auth-mvp-decision.md](D:\Projects\langoose\docs\auth-mvp-decision.md) and [auth-m1-implementation-blueprint.md](D:\Projects\langoose\docs\auth-m1-implementation-blueprint.md) aligned with the implementation.
- For .NET test-organization work, use the `langoose-dotnet-testing` skill in `.codex/skills`.
- For broader architecture and project-boundary decisions such as `API + Data` versus `API + Domain + Data`, use the `langoose-architecture` skill in `.codex/skills`.
- For EF Core structure, separate data-project boundaries, DbContext placement, and migrations layout decisions, use the `langoose-efcore-structure` skill in `.codex/skills`.
- For React and TypeScript frontend decisions, use the `langoose-react-typescript` skill in `.codex/skills`.
- For request/response contract changes across backend and frontend, use the `langoose-api-contracts` skill in `.codex/skills`.
- For grading, scheduling, and card-selection changes, use the `langoose-study-engine` skill in `.codex/skills`.
- For CSV, duplicate normalization, and dictionary visibility rules, use the `langoose-dictionary-imports` skill in `.codex/skills`.
- For Dockerfiles, Compose, containerized local development, and persistence wiring decisions, use the `langoose-docker` skill in `.codex/skills`.
