# Langoose Agent Guide

## Project Shape

- `apps/api` is the backend boundary.
- `apps/api/src/Langoose.Domain` contains domain models, enums, constants, and service interfaces.
- `apps/api/src/Langoose.Core` contains service implementations and utilities.
- `apps/api/src/Langoose.Data` contains app persistence and seeding.
- `apps/api/src/Langoose.Api` contains controllers, DTOs, middleware, and DI setup.
- `apps/api/src/Langoose.Worker` contains the background processing host.
- `apps/api/src/Langoose.Auth.Data` contains auth persistence.
- `apps/api/src/Langoose.Corpus.Data` contains the read-only corpus database access layer (Dapper, raw SQL schema).
- `apps/api/src/Langoose.Corpus.DbTool` contains the CLI for initialising and importing into the corpus database.
- `apps/api/tests` contains backend unit and integration tests.
- `apps/web` contains the React 19 + TypeScript + Vite frontend.
- App and auth persistence use PostgreSQL through EF Core. Corpus persistence uses PostgreSQL through Dapper (read-only).

## Core Rules

- Before writing code for a GitHub issue, follow the full issue lifecycle in `docs/agent/git-conventions.md`.
- Before writing code, read the relevant `docs/agent/` guides for the areas being changed.
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
