---
name: langoose-api-contracts
description: Keep Langoose backend and frontend API contracts aligned. Use when changing C# request or response models, controller payloads, enum serialization, frontend API types, error shapes, or endpoint behavior that must stay synchronized between apps/api and apps/web.
---

# Langoose Api Contracts

Read [D:\Projects\langoose\AGENTS.md](D:\Projects\langoose\AGENTS.md) first.

Use this skill for any change that crosses the backend/frontend boundary.

## Main Rule

- Change contract definitions and consumers together.

## Contract Workflow

- Inspect the C# models in `apps/api/Models`.
- Inspect the controller actions that expose those models.
- Inspect the frontend contract layer in `apps/web/src/api.ts`.
- Update all three when the payload shape or behavior changes.

## Langoose-Specific Guidance

- Prefer named request and response models over anonymous or weakly typed payloads.
- Preserve string enum serialization behavior where the API already exposes enums as strings.
- Keep frontend types narrow and aligned with the backend, especially for study, dictionary, auth, and import/export flows.
- If an endpoint returns `404`, `400`, `202`, or `204`, make sure the frontend request helper still handles that outcome correctly.

## Load Additional Detail Only When Needed

- For concrete repo rules and review points, read [D:\Projects\langoose\.codex\skills\langoose-api-contracts\references\contracts.md](D:\Projects\langoose\.codex\skills\langoose-api-contracts\references\contracts.md).
