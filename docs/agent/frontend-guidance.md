# Langoose Frontend Guidance

## Defaults

- The app uses `React.StrictMode` in `apps/web/src/main.tsx`.
- Avoid state in `apps/web/src/App.tsx` that merely mirrors other known values.
- Prefer named request and response shapes in `apps/web/src/api.ts`.
- Update `apps/web/src/api.ts` first when backend contracts evolve.

## TypeScript

- Keep `strict: true` in `apps/web/tsconfig.json`.
- Consider `exactOptionalPropertyTypes` and `noUncheckedIndexedAccess` only when the repo is ready to absorb the fixes.
- Evaluate `moduleResolution: "bundler"` against current Vite defaults when the frontend TS config is next revisited, but do not change it by rote.
- Prefer `unknown` plus narrowing over `any`.

## Review Checklist

- Is this effect synchronizing with something external, or is it derived-state logic?
- Is this state minimal?
- Is the API payload or response type precise enough to catch drift?
- Should this section be extracted into a focused child component?

## Sources

- React: [Keeping Components Pure](https://react.dev/learn/keeping-components-pure)
- React: [Choosing the State Structure](https://react.dev/learn/choosing-the-state-structure)
- React: [You Might Not Need an Effect](https://react.dev/learn/you-might-not-need-an-effect)
- React: [Responding to Events](https://react.dev/learn/responding-to-events)
- TypeScript: [strict](https://www.typescriptlang.org/tsconfig/strict.html)
