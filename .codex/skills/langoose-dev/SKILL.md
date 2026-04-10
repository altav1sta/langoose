---
name: langoose-dev
description: Work effectively in the Langoose repository. Use when working across multiple parts of the repo, when no narrower Langoose skill clearly applies, or when you need the high-level repo map, core conventions, commands, and skill routing for this project.
---

# Langoose Dev

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill as the lightweight router for the repo.

## Repo Map

- Keep frontend work inside `apps/web`.
- Keep backend work inside `apps/api`, with projects under `apps/api/src` and tests under `apps/api/tests`.
- Treat `Langoose.Data` as app-data persistence and `Langoose.Auth.Data` as auth persistence.
- Keep auth and hosting docs in [docs](../../../docs) aligned when auth direction changes.

## Use Narrower Skills When They Fit

- `langoose-dotnet-testing` for backend test layout and test-boundary decisions.
- `langoose-auth-hosting` for auth flow, cookies, antiforgery, OpenIddict, forwarded headers, and hosted behavior.
- `langoose-react-typescript` for frontend state, component, and TS decisions.
- `langoose-api-contracts` for backend/frontend payload changes.
- `langoose-study-engine` for grading, scheduling, and card-selection work.
- `langoose-dictionary-imports` for dictionary and CSV rules.
- `langoose-docker` for Docker and Compose.
- `langoose-architecture` for project-boundary refactors.
- `langoose-efcore-structure` for EF Core and migrations layout.

## Finish Cleanly

- Run the smallest relevant build, test, and acceptance checks.
- Prefer the containerized path for whole-app, startup, persistence, or auth verification.
- If a validation lane is blocked, report the gap plainly.
- If repo reality changed, update the affected skill or doc before finishing.

## Load Additional Detail Only When Needed

- For commands and repo-specific validation, read [workflows.md](../../../docs/agent/workflows.md).
- For test-boundary rationale, also read [backend-test-strategy.md](../../../docs/backend-test-strategy.md).
