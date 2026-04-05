# Langoose Agent Guide

## Project Shape

- `apps/api`: backend solution root and backend-wide configuration boundary on .NET 10.
- `apps/api/src`: backend production-source area.
- `apps/api/src/Langoose.Api`: ASP.NET Core Web API with controller-based JSON endpoints.
- `apps/api/src/Langoose.Domain`: core persisted domain models and store abstractions.
- `apps/api/src/Langoose.Data`: EF Core, PostgreSQL persistence, and migrations.
- `apps/api/src/Langoose.Auth.Data`: auth persistence, Identity/OpenIddict EF Core wiring, and auth migrations.
- `apps/api/tests/Langoose.Api.UnitTests`: xUnit-based .NET unit-test project for isolated backend logic.
- `apps/api/tests/Langoose.Api.IntegrationTests`: xUnit-based .NET integration/behavior project for host, persistence, and auth flows.
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
  - `apps/api/tests/Langoose.Api.UnitTests`
  - `apps/api/tests/Langoose.Api.IntegrationTests`
  - optionally `apps/api/tests/Langoose.Api.FunctionalTests` if a third API-local test layer is needed later
  - optionally `tests/Langoose.E2E.Tests` or similar for repo-level end-to-end coverage
- If tests stay together temporarily, still organize them internally by test type and by the API class/service they exercise.

## Working Agreements

- Treat available plugins, connector tools, and MCP app capabilities as the mandatory first path in any scenario where
  they can do the job. This rule is paramount over convenience-based CLI or manual fallbacks.
- Do not ask the user to approve a CLI or manual fallback when an available plugin or connector path can accomplish the
  same work.
- Never use CLI, shell, `gh`, direct web browsing, or other fallback paths for inspection when an available plugin or
  connector can perform that inspection.
- Keep reads and writes separated. Do not bundle inspection or metadata reads into a fallback workflow when the
  available plugin or connector can perform those reads. Use the plugin path for reads first, then use fallback only
  for the exact unsupported write or mutation step if one remains.
- Generalize that separation across all plugin-capable work: if a plugin, connector, or MCP app can perform any
  sub-operation in a workflow, keep that sub-operation on the plugin path. Do not bundle plugin-capable search, fetch,
  inspection, metadata, or other supported steps into a broader fallback workflow just because a later step is
  unsupported.
- Use CLI, shell tools, direct web browsing, or other manual fallbacks only after verifying that the available plugin
  path does not support the required operation well enough.
- Before any fallback path is used, explicitly state which plugin or connector path was checked, which exact required
  operation it could not perform, and why the fallback is necessary. If that statement cannot be made clearly, do not
  use the fallback.
- Preserve the MVP architecture. Prefer small changes inside the current React SPA and ASP.NET Core service/controller structure before introducing new layers, packages, or infrastructure.
- Keep business rules on the backend when they affect grading, dictionary visibility, imports, or scheduling.
- Do not introduce a different persistence stack, background worker, or production-grade auth unless the task explicitly asks for that shift.
- For auth planning and implementation in this repo, keep [auth-mvp-decision.md](D:\Projects\langoose\docs\auth-mvp-decision.md) and [auth-m1-implementation-blueprint.md](D:\Projects\langoose\docs\auth-m1-implementation-blueprint.md) aligned with real repo guidance and implementation direction.
- Normalize line endings. Respect `.gitattributes`, avoid accidental whole-file line-ending churn, and prefer repository-consistent endings when editing files.
- Before finishing a change, normalize edited files to the line endings required by .gitattributes and run git diff --check to catch EOF and whitespace issues.
- Do not leave an edited file in a temporarily wrong line-ending state and treat the warning as acceptable. Before finishing, the working tree version of every edited file must already match the required line endings for that file.
- Do not introduce or leave stray leading blank lines at the start of source, config, solution, or documentation files unless the file format explicitly requires them.
- Do not leave an edited file in a temporarily wrong line-ending state and treat the warning as acceptable. Before finishing, the working tree version of every edited file must already match the required line endings for that file.
- Do not introduce or leave stray leading blank lines at the start of source, config, solution, or documentation files unless the file format explicitly requires them.
- When editing Markdown docs, verify relative links from the edited file's real directory rather than assuming repo-root paths. After doc changes, do a quick scan for broken sibling or nested `docs/...` links before finishing.
- Protect non-ASCII product text. When a file contains Russian or other non-ASCII user-facing text, preserve it as valid UTF-8 or use explicit C# `\u` escapes if tooling might corrupt the literal; do not replace such text with `?`, rely on shell-default encodings, or finish a change while mojibake or placeholder characters remain in source files.
- Treat seed files and other baseline content assets as especially sensitive to non-ASCII corruption. Inspect the actual file contents before finishing when they contain Russian or other non-ASCII text; do not trust terminal display alone.
- Prefer a standard maximum line length of 120 characters unless an existing file or construct clearly justifies an exception.
- For backend work, validate repository rules against all source and test files under `apps/api/src` and `apps/api/tests`, not only the files directly edited in the current change.
- Treat Visual Studio `.sln` project-entry lines as a narrow exception to the 120-character rule when the solution format requires a longer line.
- In C# code, prefer one top-level type per file. Split files when a class, record, enum, or interface would otherwise share a file with another top-level type.
- In C# code, keep patterns consistent within the same subsystem. If one entity or concern in a slice uses a dedicated configuration class, constant holder, or similar structural pattern, do not leave adjacent equivalent cases half-inline without a clear reason.
- In C# code, keep namespaces aligned with the current project and folder structure whenever files are added or moved. Do not leave mismatched namespaces behind after refactors.
- In C# types, keep public and protected members before private helpers. Place private helper methods at the bottom unless a nearby private member clearly improves readability.
- In C# code, prefer the shortest clear name available in scope. Do not use fully qualified type or member names when a normal `using`, alias, or local scope can express the same thing clearly without ambiguity.
- In C# code, do not add or keep collection materialization or conversion calls such as `ToArray()`, `ToList()`, or similar wrappers unless the target API actually requires that shape or the code depends on the materialized snapshot semantics. Verify the callee signature before treating such a conversion as necessary.
- Treat code inspection findings as part of normal correctness work, not optional polish. For every changed C# file, do a manual inspection pass for obvious highlighted issues such as unused or redundant imports, redundant conversions, wrong overloads, unnecessary qualification, and similar editor- or analyzer-visible problems, then fix them unless there is a concrete reason to keep them.
- For every changed C# file, always run the full inspection checklist by default. Do not wait for a user callout, a suspicious line, or a first discovered issue before checking imports, redundant conversions, overload usage, unnecessary qualification, redundant registrations, and similar inspection-level concerns across the whole file.
- Once an inspection issue pattern is discovered in a task, keep it active across the rest of the changed-file pass. Do not fix one instance and then forget to check for the same class of issue in adjacent files or later revisions of the same file.
- In C# code, do not leave unused `using` directives behind. Clean them up during the change instead of treating them only as a final polish step.
- After each C# file modification, re-check the `using` block for alignment. Keep `using` directives consistently ordered and grouped, with `System` namespaces first, instead of leaving the imports in an arbitrary edit order.
- Treat unused `using` directives as a mandatory correctness check, not optional cleanup. Before asking for review or reporting progress on C# changes, verify that newly added or edited files do not contain stray unused `using` directives.
- For backend C# work, always do a manual pass over the changed source and test files for obvious unused `using` directives, even when you also run analyzer-based checks.
- Analyzer-based checks can support unused-`using` verification, but they never replace the manual pass. Always inspect the changed C# files directly for stray imports even after analyzer checks report clean results.
- Do not verify a `using` directive from memory alone. When a C# import looks questionable, map it to the exact symbol(s) used in that file or treat it as suspect until proven necessary.
- When checking a `using` directive, verify both that the file uses symbols from that namespace and that the explicit import is actually needed in that project context. In SDK-style projects with implicit or global usings, treat redundant explicit imports as unused.
- Apply the full import check to every changed C# file by default. Do not wait for the user to emphasize a file before checking symbol ownership and implicit/global-using redundancy.
- For backend C# manual cleanup passes, use an explicit file-by-file flow: list the currently changed `.cs` files, include both modified tracked files and untracked new `.cs` files in that list, narrow to the files that still contain `using` directives, inspect each of those files directly, and only then report the sweep complete.
- If the user explicitly calls out a specific file, re-inspect that file directly in the current pass even if it was checked earlier. Do not rely on memory or an earlier partial pass for user-emphasized files.
- For user-emphasized files, do not justify an import by general framework familiarity or habit. Re-map each questioned `using` to a concrete symbol in that exact file before saying it is required.
- For user-emphasized files, also re-check whether the questioned import is redundant because of implicit or global usings. A symbol being available in the file is not proof that the explicit `using` is needed.
- Do not let a broad repo-wide pass override special attention the user asked for on a specific file. User-emphasized files must appear explicitly in the checked-file report.
- Do not claim a specific file was verified unless that exact file was directly checked in the current pass. Do not infer verification status from adjacent files, naming patterns, or prior assumptions.
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
- Treat build and test execution as part of the default sanity check, not optional extra validation. When a change affects compilable or testable code, attempt the relevant build and test commands in addition to manual inspection.
- If build or test execution fails or is blocked, keep that as an active validation lane: diagnose the failure, retry when possible, and report the exact blocker plainly instead of silently downgrading the check to inspection only.
- Treat inspection, import cleanup, build/test execution, and acceptance-path verification as cumulative validation lanes. Completing one of them never replaces the others that still apply to the change.
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
- For GitHub inspection and ordinary GitHub-side reads, prefer the GitHub plugin or connector tools first.
- Use `gh` only for GitHub-side operations that the available plugin tools do not support well enough, such as issue
  creation or specific project mutations.
- Use local `git` for workspace branch creation, checkout, and other repository-state operations; do not treat the
  GitHub plugin as a replacement for local branch management.
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
- Do not start issue discovery or implementation while setup residue still exists. Before treating an issue as in work,
  clear unintended setup leftovers such as temporary stashes, accidental worktree changes, partial checkouts, or other
  workflow artifacts that are not part of the intended issue diff.
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
- When an issue belongs under an epic, create and maintain it as a real GitHub child issue or sub-issue whenever the
  available GitHub tools support that relationship. Do not treat a plain-text `Supports #...` mention as a complete
  substitute when the real child-issue relationship can be set.
- When starting an issue, move that issue to `In Progress`.
- If the issue belongs to an epic that is still not active, move the epic to `In Progress` when the parent work is
  meaningfully underway.
- When opening a PR, move the issue to `In Review`.
- After merge, move the issue to `Done`.
- Do not report an issue as finalized, handed off, or complete until every required repo workflow state transition has been verified live, including issue and project status changes where the repo flow requires them.
- Treat issue and project status updates as part of completion, not as optional cleanup after the code and PR work are done.
- Close child issues automatically from PRs when appropriate by using `Closes #...` in the PR body.
- Close epics manually only after confirming the grouped outcome is actually complete.
- When a new task truly belongs under an existing epic, add it as a real GitHub child issue rather than only mentioning
  the epic in plain text.

### Labels And Planning

- Preserve the existing labels unless the user explicitly asks to change the labeling system.
- Use area labels such as `backend`, `frontend`, `infra`, and `db` to reflect the affected system areas.
- Use planning labels such as `mvp`, `post-mvp`, `bug`, `tech-debt`, and `blocked` to reflect roadmap context.
- Keep milestones aligned with the issue's role in the roadmap. Do not leave milestone inheritance or alignment as an
  assumption; verify it explicitly for the epic and each child issue that should share roadmap timing.
- Track issues in the project unless the repo flow explicitly calls for something else.

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
  - unit tests: `dotnet test apps/api/tests/Langoose.Api.UnitTests/Langoose.Api.UnitTests.csproj /p:RestoreConfigFile=D:\Projects\langoose\apps\api\NuGet.Config`
  - integration tests: `dotnet test apps/api/tests/Langoose.Api.IntegrationTests/Langoose.Api.IntegrationTests.csproj /p:RestoreConfigFile=D:\Projects\langoose\apps\api\NuGet.Config`
- Backend build:
  - `dotnet build apps/api/Langoose.sln /p:RestoreConfigFile=D:\Projects\langoose\apps\api\NuGet.Config`
- Frontend build:
  - `npm run build` from `D:\Projects\langoose\apps\web`
- Frontend tests:
  - `npm run test` from `D:\Projects\langoose\apps\web`
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
  - when auth flow changes are involved, also verify sign-up, sign-in, sign-out, `/auth/me`, and at least one
    protected write through the running stack
  - when auth/session/CSRF behavior needs full browser proof, run `docker compose --profile e2e up --build e2e`
  - if the change affects user-facing behavior, exercise the real browser-facing flow against the running stack rather
    than stopping at container startup alone

## Change Heuristics

- For API work, inspect `Controllers`, `Services`, and `Models` together before editing behavior.
- For study-flow changes, review both [StudyService.cs](D:\Projects\langoose\apps\api\src\Langoose.Api\Services\StudyService.cs) and the discoverable xUnit tests under [apps/api/tests](D:\Projects\langoose\apps\api\tests).
- For dictionary/import changes, review both [DictionaryService.cs](D:\Projects\langoose\apps\api\src\Langoose.Api\Services\DictionaryService.cs) and [DictionaryController.cs](D:\Projects\langoose\apps\api\src\Langoose.Api\Controllers\DictionaryController.cs).
- For frontend work, keep the API contract in sync with [api.ts](D:\Projects\langoose\apps\web\src\api.ts).
- For auth work, keep [auth-mvp-decision.md](D:\Projects\langoose\docs\auth-mvp-decision.md) and [auth-m1-implementation-blueprint.md](D:\Projects\langoose\docs\auth-m1-implementation-blueprint.md) aligned with the implementation.
- For .NET test-organization work, use the `langoose-dotnet-testing` skill in `.codex/skills`.
- For backend test-boundary decisions and the rationale behind the current split, also consult [backend-test-strategy.md](D:\Projects\langoose\docs\backend-test-strategy.md).
- If a backend change introduces PostgreSQL-specific query behavior, migrations risk, transaction semantics, provider-specific SQL, database constraints/indexes/defaults, or a bug that reproduces only against real PostgreSQL behavior, treat that as the trigger to add or extend Testcontainers-backed backend integration coverage rather than relying only on EF InMemory tests.
- For broader architecture and project-boundary decisions such as `API + Data` versus `API + Domain + Data`, use the `langoose-architecture` skill in `.codex/skills`.
- For EF Core structure, separate data-project boundaries, DbContext placement, and migrations layout decisions, use the `langoose-efcore-structure` skill in `.codex/skills`.
- For React and TypeScript frontend decisions, use the `langoose-react-typescript` skill in `.codex/skills`.
- For request/response contract changes across backend and frontend, use the `langoose-api-contracts` skill in `.codex/skills`.
- For grading, scheduling, and card-selection changes, use the `langoose-study-engine` skill in `.codex/skills`.
- For CSV, duplicate normalization, and dictionary visibility rules, use the `langoose-dictionary-imports` skill in `.codex/skills`.
- For Dockerfiles, Compose, containerized local development, and persistence wiring decisions, use the `langoose-docker` skill in `.codex/skills`.
