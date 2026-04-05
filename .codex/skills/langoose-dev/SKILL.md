---
name: langoose-dev
description: Work effectively in the Langoose repository. Use when Codex is implementing, debugging, reviewing, or extending the React/Vite frontend, the ASP.NET Core API, the PostgreSQL-backed dictionary and study flow, CSV import/export, or the xUnit-based backend test suite in this project.
---

# Langoose Dev

Read [D:\Projects\langoose\AGENTS.md](D:\Projects\langoose\AGENTS.md) first.

Use this skill to stay aligned with the repo's MVP architecture and product invariants.

## Follow The Existing Shape

- Keep frontend work inside `apps/web` with React, TypeScript, and plain CSS.
- Keep backend work inside `apps/api`, with backend-wide config at `apps/api/`, production projects under `apps/api/src`, and API-owned tests under `apps/api/tests`.
- Treat `Langoose.Data` as app-domain persistence and `Langoose.Auth.Data` as auth persistence in the current repo layout.
- Prefer extending existing services over adding new abstractions.
- For branch, issue, PR, and project workflow behavior, follow `AGENTS.md` directly instead of restating or inventing
  alternate workflow rules in the skill layer.
- Keep issue startup and implementation residue-free.
- Respect `.gitattributes` and keep line endings normalized when creating or editing files.
- Leave edited files in their final required line-ending state before finishing. Do not treat a temporary line-ending warning as acceptable cleanup debt.
- When editing Markdown docs, verify relative links from the file's actual directory. Do not assume repo-root-style `docs/...` links work from files that already live under `docs/`.
- When files are created or rewritten through shell commands, explicitly normalize their line endings before finishing.
- If files were moved or created in bulk, verify they do not contain mixed line endings at the byte level before declaring the task clean.
- Do not leave stray leading blank lines at the top of source, config, solution, or documentation files unless the file format explicitly requires them.
- Preserve non-ASCII product text safely. Keep Russian and other non-ASCII literals as valid UTF-8, or switch to explicit C# `\u` escapes when Windows tooling or shell encoding could corrupt them.
- Treat seed assets and other baseline content files as high-risk for non-ASCII corruption. If they contain Russian or other non-ASCII text, inspect the actual file contents before finishing; do not assume terminal rendering tells the truth.
- Prefer the repository line-length standard of 120 characters where practical.
- In C# code, prefer one top-level type per file unless a tiny local exception is clearly justified.
- In C# code, keep structural patterns consistent within the same subsystem. If one entity or concern uses a dedicated configuration class, constants type, or similar structure, make equivalent nearby cases follow the same pattern unless there is a clear reason not to.
- In C# code, always check namespace alignment after moving or adding files so the namespace still matches the current project and folder structure.
- In C# types, keep the main public and protected flow first and move private helper methods to the bottom unless a nearby private member genuinely improves readability.
- In C# code, prefer the shortest clear name available in scope. Avoid fully qualified type or member names when a normal `using`, alias, or local scope can express the same thing clearly without ambiguity.
- In C# code, remove unused `using` directives as part of the normal edit, not only as a final cleanup step.
- After each C# file modification, re-check the `using` block for alignment. Keep `using` directives consistently ordered and grouped, with `System` namespaces first, instead of leaving imports in arbitrary edit order.
- For backend C# cleanup passes, use an explicit file-by-file workflow: list the currently changed `.cs` files, include both modified tracked files and untracked new `.cs` files in that list, narrow to the ones that still contain `using` directives, inspect each of those files directly, and only then treat the manual pass as complete.
- If the user explicitly calls out a specific file, re-inspect that file directly in the current pass even if it was checked earlier. Do not rely on memory or an earlier partial pass for user-emphasized files.
- Do not let a broad repo-wide pass override special attention the user asked for on a specific file. User-emphasized files must appear explicitly in the checked-file report.
- Do not claim a specific file was verified unless that exact file was directly checked in the current pass. Do not infer verification status from adjacent files, naming patterns, or prior assumptions.
- Do not treat an earlier partial cleanup pass as evidence for a later full pass. A new full pass must rebuild the file list from the current working tree and re-check every file on that list.
- For unused `using` cleanup, do not infer correctness from how familiar, busy, or framework-heavy a file looks. Verify each import against symbols or extension methods actually used in that file.
- Do not claim a manual cleanup pass is complete without a proof artifact in the response: the exact checked-file list, the files changed by the pass, and the post-fix validation results.
- In dependency injection setup, keep registrations minimal and non-redundant. If the code only resolves a DbContext factory or only resolves the scoped DbContext directly, remove the unused duplicate registration.
- In C# code, check property spacing after refactors so consecutive properties stay flat and do not accumulate stray one-off blank lines or subjective semantic grouping.
- In C# code, prefer `""` over `string.Empty` for normal empty-string literals and default values unless a specific API usage genuinely reads better with `string.Empty`.
- In C# code, prefer `required` over empty-string fallback defaults for mandatory entity and model properties. For entity and model strings and enums, set values explicitly at creation sites instead of hiding them behind property defaults.
- In C# entity models, prefer explicit ID and timestamp assignment at creation sites or in persistence configuration instead of hiding those values behind automatic property defaults. Keep automatic defaults only when they represent a deliberate domain rule.
- In C# code, replace non-obvious numeric literals with named constants or configuration so domain and algorithm tuning values are understandable where they are used.
- Prefer primary constructors for C# types when dependency injection or simple state capture makes them a cleaner fit than a separate constructor body.
- Prefer `record` types for DTOs, API models, immutable configuration-shaped objects, and other POCO-like data carriers where value semantics make sense.
- In C# methods, use blank lines to separate real logic phases such as setup, loading, transformation, branching, and persistence. Prefer that over treating spacing as a rule tied only to `if`, `return`, or loop keywords.
- In C# lambdas, use short parameter names when the expression is simple and locally obvious. Switch to descriptive names when nested logic, multiple parameters, or non-obvious roles would make terse names harder to follow.
- In React code, prefer pure render logic, derived state, and event-driven updates over effect-driven synchronization.
- In TypeScript code, prefer exact domain types and strict narrowing over broad fallback object types.
- Treat the current persistence mechanism in the repo as the source of truth. Do not assume the repo still uses the older JSON-file store if the code has already moved on.
- Treat auth persistence as a separate concern from app-domain persistence. For this repo's planned auth direction, prefer a separate auth database on the same PostgreSQL server, with its own migration stream, instead of mixing auth and app data in one database.
- For auth work, keep [D:\Projects\langoose\docs\auth-mvp-decision.md](D:\Projects\langoose\docs\auth-mvp-decision.md) and [D:\Projects\langoose\docs\auth-m1-implementation-blueprint.md](D:\Projects\langoose\docs\auth-m1-implementation-blueprint.md) aligned with repo guidance and implementation reality.
- When base database content must be initialized, keep the seeding implementation and seed assets in Langoose.Data; let Program.cs only trigger that initialization at startup.

## Finish Cleanly

- Before claiming a task is done, verify the acceptance path that the user will actually exercise. If the change is meant to work through Docker, local UI, or a live service boundary, prefer that real path over code-only confidence.
- Do not report containerized or end-to-end success unless the live stack was actually started and the relevant request path was exercised successfully.
- When the repo already provides a Docker or Compose path for frontend validation, use that containerized frontend path
  first for acceptance checks. Do not default to host-local `npm` build or runtime validation unless Docker is blocked,
  unavailable, or the user explicitly asks for host-local verification.
- After implementation, do a cleanup sweep for generated or machine-local artifacts created during the task, including `.dotnet`, `bin`, `obj`, `.vs`, runtime data, and any local config that should not stay in the repo.
- If generated artifacts appear in Solution Explorer or Git status unexpectedly, find the build or package source that is producing them before adding exclusion workarounds. Prefer removing the root cause over hiding the symptom.
- Keep `Microsoft.EntityFrameworkCore.Design` references on the lean asset set used by the repo unless a verified design-time need requires otherwise. Widening the package to runtime/content assets can surface `BuildHost-*` folders in normal project output.
- If the user has already asked for cleanup discipline, treat that as part of the task rather than an optional follow-up.
- Before finalizing an issue, check whether the repo skills or their reference files still describe the pre-change state. If the work changed repo reality, commands, persistence, test locations, or finish flow expectations, update the affected skills in the same issue instead of leaving them stale.
- Before finalizing an issue, run both `git diff --check` and an explicit line-ending check over newly created or moved files so mixed newlines are caught before the user opens them in Visual Studio.
- Before finalizing backend work, run an explicit unused-namespace-import check for C# files, preferably with `dotnet format analyzers ... --diagnostics IDE0005 --verify-no-changes`, and also do a manual pass over the changed backend source and test files. If the analyzer is blocked, the manual pass is still required.
- Do not call an issue finalized, handed off, or complete until the required repo workflow state transitions are verified live, including issue and project status changes when the repo flow requires them.
- Treat issue and project status updates as part of completion, not as optional cleanup after the code and PR work are done.
- If startup seeding or repair logic can overwrite existing persisted base content, verify the seed source itself is not corrupted before shipping. A broken seed file is a data rewrite bug, not just a fixture bug.
- If a verification step fails, is blocked by the environment, or does not complete, do not report it as passing from memory or inference. State the verification gap plainly, rerun it if possible, and only claim a clean result after a successful run.
- For issue-branch creation, PR handoff, mergeability, and issue/PR metadata alignment, follow the GitHub workflow
  rules in `AGENTS.md` directly instead of repeating or improvising them here.
- Do not start issue discovery or implementation while workflow setup residue still exists. Clear unintended stashes,
  accidental worktree edits, partial checkouts, or other setup artifacts before treating the issue as in work.
- If a refactor or project move changes solution paths, project paths, Dockerfile paths, or config locations, inspect CI/workflow files and update them in the same issue. Do not assume existing build and test workflows still point at the right files after the restructure.

## Validate In The Smallest Useful Way

- Run the discoverable xUnit backend tests for backend behavior changes.
- Run `npm run test` for frontend behavior changes that now have Vitest coverage.
- Run the frontend build for web changes.
- Run `docker compose --profile e2e up --build e2e` when auth/session/CSRF changes need full browser proof through the
  repo's Playwright flow under `tests/e2e`.
- Prefer targeted validation over broad churn.
- When persistence, startup, or auth changes are involved, add at least one realistic runtime check that covers app startup and the user-facing path most likely to break.

## Protect Core Behaviors

- Preserve duplicate-collapsing behavior between base and custom dictionary items.
- Preserve strict CSV header/order validation and no-partial-import behavior.
- Preserve tolerant study grading unless the task explicitly changes the grading rules.
- Preserve the current auth direction and published auth contracts unless the task explicitly asks to redesign them.

## Load Additional Detail Only When Needed

- For commands, invariants, and task-specific review points, read [D:\Projects\langoose\.codex\skills\langoose-dev\references\workflows.md](D:\Projects\langoose\.codex\skills\langoose-dev\references\workflows.md).
- For .NET test layout and ASP.NET Core test-boundary decisions, use [D:\Projects\langoose\.codex\skills\langoose-dotnet-testing\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-dotnet-testing\SKILL.md).
- For the repo-specific reasoning behind unit vs integration vs e2e boundaries, also read [D:\Projects\langoose\docs\backend-test-strategy.md](D:\Projects\langoose\docs\backend-test-strategy.md).
- When backend work starts depending on PostgreSQL-specific behavior such as migrations, constraints, indexes, transactions, collation/case-sensitivity, query translation, provider-specific SQL, or a bug that only appears against real PostgreSQL, treat that as the point to introduce or expand Testcontainers-backed integration coverage.
- For React and TypeScript frontend guidance, use [D:\Projects\langoose\.codex\skills\langoose-react-typescript\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-react-typescript\SKILL.md).
- For API contract synchronization, use [D:\Projects\langoose\.codex\skills\langoose-api-contracts\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-api-contracts\SKILL.md).
- For study-loop behavior, use [D:\Projects\langoose\.codex\skills\langoose-study-engine\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-study-engine\SKILL.md).
- For dictionary and CSV import/export rules, use [D:\Projects\langoose\.codex\skills\langoose-dictionary-imports\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-dictionary-imports\SKILL.md).
- For Docker and Compose setup, use [D:\Projects\langoose\.codex\skills\langoose-docker\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-docker\SKILL.md).
