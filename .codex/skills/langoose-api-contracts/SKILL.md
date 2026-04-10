---
name: langoose-api-contracts
description: Keep Langoose backend and frontend API contracts aligned. Use when changing C# request or response models, controller payloads, enum serialization, frontend API types, error shapes, or endpoint behavior that must stay synchronized between apps/api and apps/web.
---

# Langoose Api Contracts

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill for any change that crosses the backend/frontend boundary.

## Main Rule

- Change contract definitions and consumers together.

## Contract Workflow

- Inspect the C# models in `apps/api/src/Langoose.Api/Models`.
- Inspect the controller actions that expose those models.
- Inspect the frontend contract layer in `apps/web/src/api.ts`.
- Update all three when the payload shape or behavior changes.

## Load Additional Detail Only When Needed

- For concrete repo rules and review points, read [api-contracts.md](../../../docs/agent/api-contracts.md).
