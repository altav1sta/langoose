---
name: langoose-dev
description: Work effectively in the Langoose repository. Use when working across multiple parts of the repo, when no narrower Langoose skill clearly applies, or when you need the high-level repo map, core conventions, commands, and skill routing for this project.
---

# Langoose Dev

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill as the repo router when no narrower Langoose skill fits.

## Use When

- The task spans multiple repo areas.
- You need the repo map, validation commands, or the right next skill.

## Primary Doc

- [workflows.md](../../../docs/agent/workflows.md)

## Related Docs

- [backend-conventions.md](../../../docs/agent/backend-conventions.md)
- [dotnet-testing.md](../../../docs/agent/dotnet-testing.md)
- [efcore-structure.md](../../../docs/agent/efcore-structure.md)
- [architecture.md](../../../docs/agent/architecture.md)
- [quality-gates.md](../../../docs/agent/quality-gates.md)
- [git-conventions.md](../../../docs/agent/git-conventions.md)

## Repo Map

- Frontend: `apps/web`
- Backend: `apps/api`
- Backend projects: `apps/api/src/*`
- Backend tests: `apps/api/tests`
- App persistence: `apps/api/src/Langoose.Data`
- Auth persistence: `apps/api/src/Langoose.Auth.Data`

## Route To Narrower Skills When They Fit

- `langoose-dotnet-testing` for backend test layout and test-boundary decisions.
- `langoose-auth-hosting` for auth flow, cookies, antiforgery, OpenIddict, forwarded headers, and hosted behavior.
- `langoose-react-typescript` for frontend state, component, and TS decisions.
- `langoose-api-contracts` for backend/frontend payload changes.
- `langoose-study-engine` for grading, scheduling, and card-selection work.
- `langoose-dictionary-imports` for dictionary and CSV rules.
- `langoose-docker` for Docker and Compose.
- `langoose-architecture` for project-boundary refactors.
- `langoose-efcore-structure` for EF Core and migrations layout.
- `langoose-quality-gates` for file hygiene, validation lanes, and CI alignment.
- `langoose-workflows` for build, run, validation, and migration commands.
- `langoose-git-conventions` for branch, commit, PR, issue, and project workflow.

## Critical Reminders

- Run the smallest relevant build, test, and acceptance checks.
- Prefer the containerized path for whole-app, startup, persistence, or auth verification.
- If a validation lane is blocked, report the gap plainly.
- If repo reality changed, update the affected skill or doc before finishing.
