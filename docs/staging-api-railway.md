# Staging API On Railway

This note captures the first manual Railway deployment for issue `#38`.

Use it for:

- the Railway service shape for the ASP.NET API
- the required staging environment variables
- the separate staging migration workflow that runs before deploy when schema changes are present
- the first hosted smoke-check sequence

Related notes:

- [staging-hosting-decision.md](staging-hosting-decision.md)
- [staging-db-operations.md](staging-db-operations.md)

## Service Shape

Create one Railway service for the API in the staging environment.

Use the repo's Railway config as code file:

- config file path: `/apps/api/src/Langoose.Api/railway.json`

That file defines:

- Dockerfile deploy from `apps/api/src/Langoose.Api/Dockerfile`
- watch scope limited to backend build inputs
- `/health` as the service health check

## Required Variables

Set these service variables in Railway:

- `ASPNETCORE_ENVIRONMENT=Staging`
- `ConnectionStrings__AppDatabase=<Neon staging connection string for langoose_app>`
- `ConnectionStrings__AuthDatabase=<Neon staging connection string for langoose_auth>`
- `Cors__AllowedOrigins__0=<staging web origin>`
- `ForwardedHeaders__Enabled=true`

Why this is required now:

- Railway terminates HTTPS before forwarding requests to the container
- the staging API must trust the forwarded scheme so antiforgery and auth cookies can require secure requests
- for this direct hosted Railway path, enabling forwarded headers without specifying `KnownProxies` or `KnownNetworks`
  is the pragmatic staging-safe setting until a narrower trusted proxy model is proven for the platform

## Migration Workflow

Use the separate GitHub Actions workflow when the deploy includes schema changes:

- workflow: `.github/workflows/db-migrations.yml`
- trigger: manual `workflow_dispatch`
- dispatch input: `target_environment=staging`
- environment: `staging`
- required secrets:
  - `APP_DATABASE`
  - `AUTH_DATABASE`

This workflow:

- checks out trusted `main`
- builds a trusted `Langoose.DbTool` Docker image within that run
- generates separate auth and app idempotent EF SQL scripts from that image
- applies those scripts in separate guarded apply jobs through that image
- does not start the API
- does not make seeding part of every deploy
- does not execute arbitrary user-supplied refs against staging secrets

Base-content seeding remains a separate maintenance workflow:

- workflow: `.github/workflows/app-seed.yml`
- trigger: manual `workflow_dispatch`
- dispatch input: `target_environment=staging`
- environment: `staging`
- required secret:
  - `APP_DATABASE`
- execution model: build a trusted `Langoose.DbTool` Docker image from `main`, then run `seed-app` in that image
  against the app database only

Release sequence:

1. run the staging migration workflow when schema changes are present, or when the staging DB needs to catch up to trusted `main`
2. deploy the Railway API service
3. run the hosted smoke checks

Base-content seeding remains a separate maintenance operation rather than part of the normal Railway deploy path.

## Manual Railway Setup

1. Create or open the staging Railway project.
2. Add a service for the API from this repository.
3. Point the service at `/apps/api/src/Langoose.Api/railway.json`.
4. Set the required variables.
5. Run the staging migration workflow first when the release includes schema changes.
6. Review the staged changes and deploy.

## First Smoke Checks

After the first deploy succeeds, verify:

1. `GET /health` returns `200`
2. `GET /auth/antiforgery` returns `200`
3. `POST /auth/sign-up` succeeds for a fresh test user
4. `GET /auth/me` succeeds after sign-in with the returned auth cookie

These checks are enough for `#38` to unblock the web deployment issue.
