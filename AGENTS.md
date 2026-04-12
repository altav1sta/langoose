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
- Keep repo guidance in sync with reality. If a change updates architecture, auth direction, test layout, Docker flow, or core commands, update the affected doc in the same change.

## Guidance Index

These docs capture detailed rules for specific areas. Read the relevant doc
when you need detail; update it when a change alters the behavior it describes.

Keep this table in sync when adding or removing docs under `docs/agent/`.

| Doc | Scope |
|-----|-------|
| `docs/agent/architecture.md` | Onion layers, project boundaries, dependency direction, anti-goals |
| `docs/agent/backend-conventions.md` | C# style conventions |
| `docs/agent/efcore-structure.md` | Entities, Guid v7, composite PKs, DbContext, migrations, entity config, seeding |
| `docs/agent/dotnet-testing.md` | Test layout, unit vs integration boundaries, test organization |
| `docs/agent/frontend-conventions.md` | React patterns, TypeScript config, CSS approach |
| `docs/agent/api-contracts.md` | DTO mapping, response shapes, frontend type alignment |
| `docs/agent/dictionary-rules.md` | Visibility, form-based dedup, CSV import/export, product invariants |
| `docs/agent/study-engine.md` | Study cards, answer evaluation, grading tolerances, scheduling |
| `docs/agent/enrichment-guidance.md` | Provider interface, worker, content generation, rate limiting |
| `docs/agent/auth-hosting.md` | Cookies, antiforgery, OpenIddict, proxy, Data Protection |
| `docs/agent/docker-guidance.md` | Dockerfiles, Compose, E2E testing |
| `docs/agent/git-conventions.md` | Commits, branches, PRs, issue lifecycle, git hygiene |
| `docs/agent/quality-gates.md` | File hygiene, validation, CI alignment, practical cautions |
| `docs/agent/workflows.md` | Build/test/run commands, migration commands |


## Skill Mapping

| Doc | Owner | Notes |
|-----|-------|-------|
| `docs/agent/architecture.md` | `langoose-architecture` | |
| `docs/agent/backend-conventions.md` | `doc-only` | C# style; links to efcore-structure and dotnet-testing |
| `docs/agent/efcore-structure.md` | `langoose-efcore-structure` | |
| `docs/agent/dotnet-testing.md` | `langoose-dotnet-testing` | |
| `docs/agent/frontend-conventions.md` | `langoose-react-typescript` | |
| `docs/agent/api-contracts.md` | `langoose-api-contracts` | |
| `docs/agent/dictionary-rules.md` | `langoose-dictionary-imports` | |
| `docs/agent/study-engine.md` | `langoose-study-engine` | |
| `docs/agent/enrichment-guidance.md` | `doc-only` | No dedicated skill yet |
| `docs/agent/auth-hosting.md` | `langoose-auth-hosting` | |
| `docs/agent/docker-guidance.md` | `langoose-docker` | |
| `docs/agent/git-conventions.md` | `langoose-git-conventions` | |
| `docs/agent/quality-gates.md` | `langoose-quality-gates` | |
| `docs/agent/workflows.md` | `langoose-workflows` | langoose-dev also uses this as primary |

## Skill Index

Skills are lightweight operational wrappers, not the canonical policy store.
When skill text and `docs/agent/` ever disagree, align the skill to the doc or
update the doc first if repo reality changed.

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
- Use [langoose-quality-gates](.codex/skills/langoose-quality-gates/SKILL.md) for file hygiene, validation lanes, and CI alignment.
- Use [langoose-workflows](.codex/skills/langoose-workflows/SKILL.md) for build, run, validation, and migration commands.
- Use [langoose-git-conventions](.codex/skills/langoose-git-conventions/SKILL.md) for branch, commit, PR, issue, and project workflow.
