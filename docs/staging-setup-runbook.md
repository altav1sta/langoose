# Staging Setup Runbook

This note captures the practical staging setup and verification path for issue `#41`.

Use it for:

- first-time staging setup
- the normal post-merge verification flow
- smoke checks across web, API, auth, and persistence
- the quickest path to the deeper staging notes when something goes wrong

Related notes:

- [staging-deployment-workflow.md](staging-deployment-workflow.md)
- [staging-api-railway.md](staging-api-railway.md)
- [staging-web-vercel.md](staging-web-vercel.md)
- [staging-db-operations.md](staging-db-operations.md)

## What Exists

The staging environment currently assumes:

- Neon branch: `staging`
- app database: `langoose_app`
- auth database: `langoose_auth`
- API hosting: Railway
- web hosting: Vercel
- deploy orchestration: GitHub Actions through `.github/workflows/deploy-environment.yml`

## One-Time Setup

### GitHub Environment

Create the `staging` GitHub environment and set:

- secrets:
  - `APP_DATABASE`
  - `AUTH_DATABASE`
  - `RAILWAY_TOKEN`
  - `VERCEL_TOKEN`
- variables:
  - `RAILWAY_PROJECT_ID`
  - `RAILWAY_API_SERVICE`
  - `RAILWAY_ENVIRONMENT`
  - `VERCEL_ORG_ID`
  - `VERCEL_PROJECT_ID`

### Railway API Service

Create or verify the staging API service and set:

- `ASPNETCORE_ENVIRONMENT=Staging`
- `ConnectionStrings__AppDatabase=<staging app connection string>`
- `ConnectionStrings__AuthDatabase=<staging auth connection string>`
- `Cors__AllowedOrigins__0=<staging web origin>`
- `ForwardedHeaders__Enabled=true`

Use:

- config file: `/apps/api/src/Langoose.Api/railway.json`

### Vercel Web Project

Create or verify the staging web project with:

- project root: `apps/web`
- framework preset: Vite
- config file: `apps/web/vercel.json`
- environment variable: `LANGOOSE_API_ORIGIN=<direct Railway API origin>`

Do not set `VITE_API_BASE_URL` for normal staging deploys. The hosted SPA should use relative `/api`.

## Normal Staging Update

For a normal change:

1. merge the change into `main`
2. let `CI` finish successfully for that merge
3. let `Deploy Environment` start from that successful `CI` run
4. confirm the deploy workflow applied auth and app migrations
5. confirm the expected API and/or web deploy lanes ran
6. run the smoke checks below

For manual staging verification or redeploy:

1. run `Deploy Environment`
2. choose `target_environment=staging`
3. choose whether to deploy the API, the web app, or both
4. verify the same smoke checks afterward

## Smoke Checks

Run these after the first setup, after environment repair, and after meaningful staging changes.

### API Checks

Verify against the direct Railway API origin:

1. `GET /health` returns `200`
2. `GET /auth/antiforgery` returns `200`
3. `POST /auth/sign-up` succeeds for a fresh test user
4. `GET /auth/me` succeeds after sign-in with the returned auth cookie

### Web Checks

Verify against the public Vercel staging origin:

1. the SPA shell loads
2. `GET /api/health` through the web origin returns `200`
3. auth bootstrap succeeds from the web origin
4. sign-up or sign-in works through the web origin
5. at least one protected write succeeds through the hosted web flow

### Persistence Checks

Verify that:

- both staging databases are reachable
- the expected migrations were applied
- base dictionary content exists when the app database was intentionally seeded
- no unexpected reset or wipe occurred during the release

## When To Seed

Base-content seeding is not part of the normal deploy path.

Run `.github/workflows/app-seed.yml` only when:

- the app database is brand new
- the app database was intentionally recreated
- you intentionally want to initialize an empty staging app database

The current seeder is empty-database-only. It does not repair drifted rows in a populated app database.

## Recovery Shortcuts

If staging becomes unreliable:

- for bad schema state:
  - rerun auth and app migrations first
- for bad app data or auth data:
  - prefer full staging database recreation over ad hoc deletes
- for missing base content after a rebuild:
  - seed only after confirming the app database is intentionally empty

Use the detailed database reset and recovery procedures in [staging-db-operations.md](staging-db-operations.md).

## Fast Navigation

Use these notes when you need more than the quick runbook:

- deployment orchestration:
  - [staging-deployment-workflow.md](staging-deployment-workflow.md)
- Railway-specific API setup:
  - [staging-api-railway.md](staging-api-railway.md)
- Vercel-specific web setup:
  - [staging-web-vercel.md](staging-web-vercel.md)
- database reset, wipe, reseed, and recovery:
  - [staging-db-operations.md](staging-db-operations.md)
