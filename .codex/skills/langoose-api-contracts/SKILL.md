---
name: langoose-api-contracts
description: Keep Langoose backend and frontend API contracts aligned. Use when changing C# request or response models, controller payloads, enum serialization, frontend API types, error shapes, or endpoint behavior that must stay synchronized between apps/api and apps/web.
---

# Langoose Api Contracts

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill for any change that crosses the backend/frontend boundary.

## Use When

- The task changes controller request or response models.
- The task changes endpoint behavior that the frontend depends on.
- The task changes frontend API types, enum serialization, or error shapes.

## Primary Doc

- [api-contracts.md](../../../docs/agent/api-contracts.md)

## Related Docs

- [frontend-conventions.md](../../../docs/agent/frontend-conventions.md)

## Critical Reminders

- Change contract definitions and consumers together.
- Inspect the C# models in `apps/api/src/Langoose.Api/Models`.
- Inspect the controller actions that expose those models.
- Inspect the frontend contract layer in `apps/web/src/api.ts`.
- This skill owns cross-boundary payload and endpoint shape, not general frontend component design or state structure.
- Update all three when the payload shape or behavior changes.
