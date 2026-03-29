---
name: langoose-dev
description: Work effectively in the Langoose repository. Use when Codex is implementing, debugging, reviewing, or extending the React/Vite frontend, the ASP.NET Core API, the PostgreSQL-backed dictionary and study flow, CSV import/export, or the xUnit-based backend test suite in this project.
---

# Langoose Dev

Read [D:\Projects\langoose\AGENTS.md](D:\Projects\langoose\AGENTS.md) first.

Use this skill to stay aligned with the repo's MVP architecture and product invariants.

## Follow The Existing Shape

- Keep frontend work inside `apps/web` with React, TypeScript, and plain CSS.
- Keep backend work inside `apps/api` with controller-based endpoints and service-layer business logic.
- Prefer extending existing services over adding new abstractions.
- Respect `.gitattributes` and keep line endings normalized when creating or editing files.
- Preserve non-ASCII product text safely. Keep Russian and other non-ASCII literals as valid UTF-8, or switch to explicit C# `\u` escapes when Windows tooling or shell encoding could corrupt them.
- Prefer the repository line-length standard of 120 characters where practical.
- In C# code, prefer one top-level type per file unless a tiny local exception is clearly justified.
- Prefer primary constructors for C# types when dependency injection or simple state capture makes them a cleaner fit than a separate constructor body.
- Prefer `record` types for DTOs, API models, immutable configuration-shaped objects, and other POCO-like data containers where value semantics make sense.
- In React code, prefer pure render logic, derived state, and event-driven updates over effect-driven synchronization.
- In TypeScript code, prefer exact domain types and strict narrowing over broad fallback object types.
- Treat the current persistence mechanism in the repo as the source of truth. Do not assume the repo still uses the older
  JSON-file store if the code has already moved on.

## Finish Cleanly

- Before claiming a task is done, verify the acceptance path that the user will actually exercise. If the change is meant
  to work through Docker, local UI, or a live service boundary, prefer that real path over code-only confidence.
- Do not report containerized or end-to-end success unless the live stack was actually started and the relevant request
  path was exercised successfully.
- After implementation, do a cleanup sweep for generated or machine-local artifacts created during the task, including
  `.dotnet`, `bin`, `obj`, `.vs`, runtime data, and any local config that should not stay in the repo.
- If generated artifacts appear in Solution Explorer or Git status unexpectedly, find the build or package source that is
  producing them before adding exclusion workarounds. Prefer removing the root cause over hiding the symptom.
- If the user has already asked for cleanup discipline, treat that as part of the task rather than an optional follow-up.
- Before finalizing an issue, check whether the repo skills or their reference files still describe the pre-change state.
  If the work changed repo reality, commands, persistence, test locations, or finish flow expectations, update the
  affected skills in the same issue instead of leaving them stale.

## Validate In The Smallest Useful Way

- Run the discoverable xUnit backend tests for backend behavior changes.
- Run the frontend build for web changes.
- Prefer targeted validation over broad churn.
- When persistence, startup, or auth changes are involved, add at least one realistic runtime check that covers app
  startup and the user-facing path most likely to break.

## Protect Core Behaviors

- Preserve duplicate-collapsing behavior between base and custom dictionary items.
- Preserve strict CSV header/order validation and no-partial-import behavior.
- Preserve tolerant study grading unless the task explicitly changes the grading rules.
- Preserve the current MVP auth model unless the task explicitly asks to redesign it.

## Load Additional Detail Only When Needed

- For commands, invariants, and task-specific review points, read [D:\Projects\langoose\.codex\skills\langoose-dev\references\workflows.md](D:\Projects\langoose\.codex\skills\langoose-dev\references\workflows.md).
- For .NET test layout and ASP.NET Core test-boundary decisions, use [D:\Projects\langoose\.codex\skills\langoose-dotnet-testing\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-dotnet-testing\SKILL.md).
- For React and TypeScript frontend guidance, use [D:\Projects\langoose\.codex\skills\langoose-react-typescript\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-react-typescript\SKILL.md).
- For API contract synchronization, use [D:\Projects\langoose\.codex\skills\langoose-api-contracts\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-api-contracts\SKILL.md).
- For study-loop behavior, use [D:\Projects\langoose\.codex\skills\langoose-study-engine\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-study-engine\SKILL.md).
- For dictionary and CSV import/export rules, use [D:\Projects\langoose\.codex\skills\langoose-dictionary-imports\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-dictionary-imports\SKILL.md).
- For Docker and Compose setup, use [D:\Projects\langoose\.codex\skills\langoose-docker\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-docker\SKILL.md).
