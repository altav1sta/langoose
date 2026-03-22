# Langoose Frontend Guidance

## Official Guidance Applied Here

- React's official docs say components should be pure during render.
- React's official docs recommend choosing state carefully, avoiding redundant or duplicated state, and flattening nested state where practical.
- React's official docs say effects are an escape hatch and should be used only when synchronizing with an external system.
- TypeScript's official docs mark `strict` as recommended.
- TypeScript's official docs describe `exactOptionalPropertyTypes`, `noUncheckedIndexedAccess`, and `noImplicitOverride` as stricter options that can improve correctness when a codebase is ready for them.

## What That Means For Langoose

- The current app already uses `React.StrictMode` in `apps/web/src/main.tsx`. Keep frontend code resilient to development double-invocation behavior.
- In `apps/web/src/App.tsx`, avoid adding state that merely mirrors props, other state, or easily computed values.
- If a value can be computed from `state.auth`, `state.dictionary`, `state.dashboard`, or `state.card`, prefer deriving it during render or from a helper rather than storing another synced field.
- Prefer moving request/response shapes into named types in `apps/web/src/api.ts`.
- When backend contracts evolve, update `apps/web/src/api.ts` first so component code stays narrow and predictable.

## Current Repo Recommendations

- Keep `strict: true` in `apps/web/tsconfig.json`.
- Consider enabling these later, not blindly:
  - `exactOptionalPropertyTypes`
  - `noUncheckedIndexedAccess`
- Consider `noImplicitOverride` only if the frontend starts using meaningful class inheritance, which it currently does not.
- Prefer function components and hooks over classes.
- Prefer small helper functions for formatting and display mapping when they reduce repeated branching in JSX.

## Practical Review Checklist

- Is this effect actually synchronizing with something external, or is it derived-state logic that should move back into render or an event handler?
- Is this state minimal, or does it duplicate something already known?
- Is this API payload/response type precise enough to catch contract drift?
- Is this component staying understandable, or should a feature section be extracted into a focused child component?

## Official Sources

- React: [Keeping Components Pure](https://react.dev/learn/keeping-components-pure)
- React: [Choosing the State Structure](https://react.dev/learn/choosing-the-state-structure)
- React: [You Might Not Need an Effect](https://react.dev/learn/you-might-not-need-an-effect)
- React: [Responding to Events](https://react.dev/learn/responding-to-events)
- TypeScript: [strict](https://www.typescriptlang.org/tsconfig/strict.html)
- TypeScript: [exactOptionalPropertyTypes](https://www.typescriptlang.org/tsconfig/exactOptionalPropertyTypes.html)
- TypeScript: [noUncheckedIndexedAccess](https://www.typescriptlang.org/tsconfig/noUncheckedIndexedAccess.html)
- TypeScript: [noImplicitOverride](https://www.typescriptlang.org/tsconfig/noImplicitOverride.html)
