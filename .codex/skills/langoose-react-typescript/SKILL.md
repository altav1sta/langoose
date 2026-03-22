---
name: langoose-react-typescript
description: Build, review, or refactor the Langoose React and TypeScript frontend using current official React and TypeScript guidance. Use when working on component state structure, effects, event handling, API typing, tsconfig decisions, frontend architecture, or React UI behavior in apps/web.
---

# Langoose React TypeScript

Read [D:\Projects\langoose\AGENTS.md](D:\Projects\langoose\AGENTS.md) first.

Use this skill when the task is primarily in `apps/web` or when frontend API types and UI behavior need to stay aligned.

## Preferred Frontend Style

- Keep components pure during render.
- Prefer deriving values during render instead of storing redundant state.
- Prefer event handlers for user-driven work.
- Use effects only when synchronizing with external systems such as browser APIs, network lifecycle edges, or subscriptions.
- Keep API contracts explicit in TypeScript rather than pushing shape uncertainty into component code.

## Langoose-Specific Guidance

- The current app is centered in [App.tsx](D:\Projects\langoose\apps\web\src\App.tsx). Preserve simplicity, but split code when a feature or model becomes hard to follow.
- Keep the typed API surface in [api.ts](D:\Projects\langoose\apps\web\src\api.ts) aligned with backend contract changes.
- Prefer narrow request payload types over `Record<string, unknown>` where the payload shape is known.
- Keep UI copy aligned with the product framing for Russian speakers learning English.

## TypeScript Biases

- Keep `strict` mode enabled.
- Prefer explicit unions and domain types over stringly typed helpers when practical.
- Prefer `unknown` plus narrowing over `any`.
- Add stricter TSConfig options only when the repo is ready to absorb the resulting fixes.

## Load Additional Detail Only When Needed

- For React principles, TypeScript options, and repo-specific frontend recommendations, read [D:\Projects\langoose\.codex\skills\langoose-react-typescript\references\frontend-guidance.md](D:\Projects\langoose\.codex\skills\langoose-react-typescript\references\frontend-guidance.md).
