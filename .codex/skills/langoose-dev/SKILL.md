---
name: langoose-dev
description: Work effectively in the Langoose repository. Use when Codex is implementing, debugging, reviewing, or extending the React/Vite frontend, the ASP.NET Core API, the PostgreSQL-backed dictionary and study flow, CSV import/export, or the xUnit-based backend test suite in this project.
---

# Langoose Dev

Read [D:\Projects\langoose\AGENTS.md](D:\Projects\langoose\AGENTS.md) first.

Use this skill to stay aligned with the repo's MVP architecture and product invariants.

## Follow The Existing Shape

- Treat available plugins, connector tools, and MCP app capabilities as the mandatory first path whenever they can do
  the job. This rule is paramount over convenience-based CLI or other manual fallbacks.
- Do not ask for CLI or manual fallback approval when an available plugin or connector path can accomplish the same
  work.
- Never use CLI, shell, `gh`, direct web browsing, or other fallback paths for inspection when an available plugin or
  connector can perform that inspection.
- Keep reads and writes separated. Do not bundle inspection or metadata reads into a fallback workflow when the
  available plugin or connector can perform those reads. Use the plugin path for reads first, then use fallback only
  for the exact unsupported write or mutation step if one remains.
- Generalize that separation across all plugin-capable work: if a plugin, connector, or MCP app can perform any
  sub-operation in a workflow, keep that sub-operation on the plugin path. Do not bundle plugin-capable search, fetch,
  inspection, metadata, or other supported steps into a broader fallback workflow just because a later step is
  unsupported.
- Use CLI or other manual fallbacks only after verifying that the available plugin path does not support the required
  operation well enough.
- Before any fallback path is used, explicitly state which plugin or connector path was checked, which exact required
  operation it could not perform, and why the fallback is necessary. If that statement cannot be made clearly, do not
  use the fallback.
- Keep frontend work inside `apps/web` with React, TypeScript, and plain CSS.
- Keep backend work inside `apps/api`, with backend-wide config at `apps/api/`, production projects under `apps/api/src`, and API-owned tests under `apps/api/tests`.
- Treat `Langoose.Data` as app-domain persistence and `Langoose.Auth.Data` as auth persistence in the current repo layout.
- Prefer extending existing services over adding new abstractions.
- For branch, issue, PR, and project workflow behavior, follow `AGENTS.md` directly instead of restating or inventing
  alternate workflow rules in the skill layer.
- For GitHub inspection and other ordinary GitHub-side reads, prefer the GitHub plugin or connector tools first; use
  `gh` only for GitHub-side operations the plugin tools do not support well enough, and use local `git` for branch and
  checkout work inside the repo.
- For epic decomposition work, keep child issues as real GitHub child issues whenever the available GitHub tools support
  that relationship, and verify milestone alignment explicitly instead of assuming it will be inherited or cleaned up
  later.
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
- In C# code, do not add or keep collection materialization or conversion calls such as `ToArray()`, `ToList()`, or similar wrappers unless the target API actually requires that shape or the code depends on the materialized snapshot semantics. Verify the callee signature before treating such a conversion as necessary.
- Treat code inspection findings as part of normal correctness work, not optional polish. For every changed C# file, do a manual inspection pass for obvious highlighted issues such as unused or redundant imports, redundant conversions, wrong overloads, unnecessary qualification, and similar editor- or analyzer-visible problems, then fix them unless there is a concrete reason to keep them.
- For every changed C# file, always run the full inspection checklist by default. Do not wait for a user callout, a suspicious line, or a first discovered issue before checking imports, redundant conversions, overload usage, unnecessary qualification, redundant registrations, and similar inspection-level concerns across the whole file.
- Once an inspection issue pattern is discovered in a task, keep it active across the rest of the changed-file pass. Do not fix one instance and then forget to check for the same class of issue in adjacent files or later revisions of the same file.
- In C# code, remove unused `using` directives as part of the normal edit, not only as a final cleanup step.
- After each C# file modification, re-check the `using` block for alignment. Keep `using` directives consistently ordered and grouped, with `System` namespaces first, instead of leaving imports in arbitrary edit order.
- Treat unused `using` directives as a mandatory correctness check, not optional cleanup. Before asking for review or reporting progress on edited C# files, verify that newly added or modified files do not contain stray unused `using` directives.
- Analyzer-based checks can support unused-`using` verification, but they never replace the manual pass. Always inspect the changed C# files directly for stray imports even after analyzer checks report clean results.
- Do not verify a `using` directive from memory alone. When a C# import looks questionable, map it to the exact symbol(s) used in that file or treat it as suspect until proven necessary.
- When checking a `using` directive, verify both that the file uses symbols from that namespace and that the explicit import is actually needed in that project context. In SDK-style projects with implicit or global usings, treat redundant explicit imports as unused.
- Apply the full import check to every changed C# file by default. Do not wait for the user to emphasize a file before checking symbol ownership and implicit/global-using redundancy.
- For backend C# cleanup passes, use an explicit file-by-file workflow: list the currently changed `.cs` files, include both modified tracked files and untracked new `.cs` files in that list, narrow to the ones that still contain `using` directives, inspect each of those files directly, and only then treat the manual pass as complete.
- If the user explicitly calls out a specific file, re-inspect that file directly in the current pass even if it was checked earlier. Do not rely on memory or an earlier partial pass for user-emphasized files.
- For user-emphasized files, do not justify an import by general framework familiarity or habit. Re-map each questioned `using` to a concrete symbol in that exact file before saying it is required.
- For user-emphasized files, also re-check whether the questioned import is redundant because of implicit or global usings. A symbol being available in the file is not proof that the explicit `using` is needed.
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
- Treat build and test execution as part of the default sanity check, not optional extra validation. When a change affects compilable or testable code, attempt the relevant build and test commands in addition to manual inspection.
- If build or test execution fails or is blocked, keep that as an active validation lane: diagnose the failure, retry when possible, and report the exact blocker plainly instead of silently downgrading the check to inspection only.
- Treat inspection, import cleanup, build/test execution, and acceptance-path verification as cumulative validation lanes. Completing one of them never replaces the others that still apply to the change.
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
- Before reporting any issue or PR workflow as complete, rerun the full completion checklist from the current repo rules. Do not stop after the most visible transitions such as branch push, PR creation, mergeability, or issue status movement if metadata verification steps still remain.
- Treat workflow completion as an explicit checklist pass, not a narrative judgment. The final pass must verify both issue-side and PR-side metadata and state, even if the main user-visible transition already happened.
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
