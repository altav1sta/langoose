# Staging Web On Vercel

This note captures the staging web deployment model for issue `#39`.

Use it for:

- the Vercel project shape for `apps/web`
- the chosen staging browser routing model
- the required Vercel variables
- the first hosted browser smoke checks

Related notes:

- [staging-hosting-decision.md](staging-hosting-decision.md)
- [staging-api-railway.md](staging-api-railway.md)
- [staging-deployment-workflow.md](staging-deployment-workflow.md)

## Browser Routing Model

The staging browser contract is:

- the web app talks to relative `/api` by default outside local Vite development
- Vercel proxies `/api/:path*` to the hosted Railway API
- the browser stays on one staging web origin
- the Railway API keeps its own direct host for health checks and debugging

That keeps auth cookies, CSRF, and browser requests on a same-origin path from the SPA's perspective.

## Required Repo Configuration

The web app now resolves its API base like this:

1. `window.LANGOOSE_CONFIG.apiBaseUrl` when provided
2. `VITE_API_BASE_URL` when provided
3. `http://localhost:5000` during local Vite development
4. `/api` everywhere else

Vercel routing lives in:

- `/apps/web/vercel.json`

That file:

- proxies `/api/:path*` to `${LANGOOSE_API_ORIGIN}/:path*`
- keeps SPA deep links working by rewriting unmatched browser paths to `/index.html`

The static frontend also ships a baseline `/config.js` file from:

- `/apps/web/public/config.js`

That keeps static hosts such as Vercel from failing on the runtime-config script tag. The nginx-based Docker path still
overrides that file at container startup with the configured `LANGOOSE_API_BASE_URL`.

## Required Vercel Variables

Set these on the Vercel project for the staging environment:

- `LANGOOSE_API_ORIGIN=<direct Railway API origin>`

Do not set `VITE_API_BASE_URL` for normal staging web deploys. The hosted browser path should stay on relative `/api`.

## Vercel Project Shape

Use `apps/web` as the Vercel project root.

Expected shape:

- framework preset: Vite
- project root: `apps/web`
- repo config file: `apps/web/vercel.json`

## GitHub-Driven Deploy Trigger

For the GitHub-driven staging deploy flow, the web deploy runs through the Vercel CLI from the checked-out workflow
commit instead of a branch-bound Deploy Hook.

The GitHub deployment workflow uses:

- secret: `VERCEL_TOKEN`
- variables:
  - `VERCEL_ORG_ID`
  - `VERCEL_PROJECT_ID`
- workflow: `.github/workflows/deploy-environment.yml`

That lets the workflow deploy the same commit it checked out for the rest of the environment update, including reruns
of older successful workflow runs.

## First Hosted Smoke Checks

After the first staging web deploy succeeds, verify:

1. the Vercel URL loads the SPA shell
2. `/api/health` through the Vercel origin returns `200`
3. auth bootstrap succeeds from the web origin
4. sign-up or sign-in works through the web origin
5. a protected write succeeds through the web origin with the hosted cookie and CSRF flow

These checks are enough for `#39` to prove that the chosen staging routing model works in a real browser-facing path.
