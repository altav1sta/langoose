# Langoose Agent Guide

## Project Shape

- `apps/api` is the backend boundary.
- `apps/api/src/Langoose.Domain` contains domain models, enums, constants, and service interfaces.
- `apps/api/src/Langoose.Core` contains service implementations and utilities.
- `apps/api/src/Langoose.Data` contains app persistence and seeding.
- `apps/api/src/Langoose.Api` contains controllers, DTOs, middleware, and DI setup.
- `apps/api/src/Langoose.Worker` contains the background processing host.
- `apps/api/src/Langoose.Auth.Data` contains auth persistence.
- `apps/api/tests` contains backend unit and integration tests.
- `apps/web` contains the React 19 + TypeScript + Vite frontend.
- App and auth persistence use PostgreSQL through EF Core.

## Core Rules

- Prefer small changes that fit the current architecture before introducing new layers or abstractions.
- Keep business rules on the backend when they affect grading, dictionary visibility, imports, scheduling, auth, or persistence.
- Prefer first-class integrations for hosted reads and metadata when they are available and reliable. Use local `git` for workspace branch management.
- Keep repo guidance in sync with reality. If a change updates architecture, auth direction, test layout, Docker flow, or core commands, update the affected skill or doc in the same change.

## File Hygiene

- Respect [`.gitattributes`](.gitattributes) and [`.editorconfig`](.editorconfig).
- Keep files UTF-8, preserve non-ASCII product text safely, and avoid line-ending-only churn.
- Prefer a 120-character line length unless the file format or local readability clearly justifies an exception.
- Do not leave stray leading blank lines at the top of source, config, solution, or documentation files.
- When editing Markdown under `docs/`, verify links relative to the file's real directory.

## C# Conventions

- Prefer one top-level type per file.
- Prefer primary constructors when they simplify the type.
- Prefer `record` or `record struct` for DTOs and other value-like data carriers when identity semantics are unnecessary.
- Keep namespaces aligned with the project and folder structure.
- Keep public and protected members before private helpers.
- Prefer names that are clear in local scope without adding unnecessary noise.
- Avoid redundant materialization, qualification, imports, and DI registrations.
- Replace non-obvious magic numbers with named constants or configuration.

## React And TypeScript Conventions

- Keep React components pure during render.
- Prefer derived state over duplicated state.
- Prefer event handlers for user-driven logic and use effects only for real external synchronization.
- Keep `strict` TypeScript and prefer precise domain types over broad fallback shapes.
- Prefer the current lightweight frontend style by default: plain React state plus plain CSS unless the change clearly needs more.

## Product Invariants

- The product is for Russian speakers learning English.
- Visible dictionary items must collapse duplicate normalized English terms across base and custom sources.
- Quick add and CSV import must merge duplicate custom entries instead of multiplying them.
- Terms already in the base dictionary should stay visible as a single base-backed item.
- CSV import must remain strict about header order and must not partially import malformed input.
- Study grading is intentionally tolerant for exact matches, known variants, missing articles, inflection mismatches, and minor typos.
- Clearing custom data must not revoke active sessions.

## Validation

- Run the smallest relevant build, test, and acceptance checks for the change.
- Prefer containerized whole-app validation when the change affects startup, persistence, auth, or cross-app behavior.
- If a validation lane is blocked or fails, say so plainly and do not report it as passing by inference.
- When a change renames or moves a project, Dockerfile, or test assembly, update the affected GitHub Actions workflows in the same change.

## Workflow

- Check the live GitHub state before answering "what's next" or starting issue work.
- Start new work from the latest `main`.
- Use one focused branch per issue or tightly related change whenever practical.
- Keep issue, PR, and project state aligned with the real state of the work.
- Use squash merge into `main`.
- When creating sub-issues for an epic, link them as real GitHub sub-issues using the `addSubIssue` GraphQL mutation — not just checklist text in the epic body.
- Assign the repo owner to every new issue and epic.

### Branch Naming

- `docs/...` for documentation and repo guidance.
- `infra/...` for CI, Docker, deployment, and workflow changes.
- `feat/...` for product or implementation work.
- `fix/...` for bug fixes.
- `chore/...` for maintenance.


## Guidance Index

These docs capture detailed rules for specific areas. Read the relevant doc
when you need detail; update it when a change alters the behavior it describes.

Keep this table in sync when adding or removing docs under `docs/agent/`.

| Doc | Scope |
|-----|-------|
| `docs/agent/architecture-guidance.md` | Onion layers (Domain, Core, Data, Api, Worker), project boundaries, dependency direction |
| `docs/agent/enrichment-guidance.md` | DictionaryEntry, EntryContext, async enrichment pipeline, LLM provider, batch processing |
| `docs/agent/dictionary-rules.md` | DictionaryEntry/UserDictionaryEntry visibility, form-based dedup, CSV import/export |
| `docs/agent/study-engine.md` | Sentence-based study cards, UserProgress, answer evaluation via Levenshtein, scheduling |
| `docs/agent/efcore-structure.md` | Entities, Guid v7, composite PKs, DbContext, migrations, entity config, seeding |
| `docs/agent/api-contracts.md` | DTO mapping pattern, request/response models, controller payloads, frontend API types |
| `docs/agent/frontend-guidance.md` | React components, state, effects, TypeScript config, API integration |
| `docs/agent/auth-hosting.md` | Auth cookies, antiforgery, OpenIddict, forwarded headers, hosting |
| `docs/agent/dotnet-testing.md` | Test layout, unit vs integration boundaries, test hosts |
| `docs/agent/docker-guidance.md` | Dockerfiles, Compose, containerized dev |
| `docs/agent/workflows.md` | Build/test commands, key file locations, validation |


## Skill Index

- Use [langoose-dev](.codex/skills/langoose-dev/SKILL.md) for general repo work or when no narrower skill clearly applies.
- Use [langoose-auth-hosting](.codex/skills/langoose-auth-hosting/SKILL.md) for auth flow, cookies, antiforgery, OpenIddict, forwarded headers, and hosted environment behavior.
- Use [langoose-dotnet-testing](.codex/skills/langoose-dotnet-testing/SKILL.md) for backend test layout and test-boundary decisions.
- Use [langoose-react-typescript](.codex/skills/langoose-react-typescript/SKILL.md) for frontend component, state, and TS decisions.
- Use [langoose-api-contracts](.codex/skills/langoose-api-contracts/SKILL.md) for backend/frontend contract changes.
- Use [langoose-study-engine](.codex/skills/langoose-study-engine/SKILL.md) for grading, scheduling, and card-selection work.
- Use [langoose-dictionary-imports](.codex/skills/langoose-dictionary-imports/SKILL.md) for dictionary and CSV rules.
- Use [langoose-docker](.codex/skills/langoose-docker/SKILL.md) for Docker and Compose work.
- Use [langoose-architecture](.codex/skills/langoose-architecture/SKILL.md) for project-boundary refactors.
- Use [langoose-efcore-structure](.codex/skills/langoose-efcore-structure/SKILL.md) for EF Core structure and migrations layout.
