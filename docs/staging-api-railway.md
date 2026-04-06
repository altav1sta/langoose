# Staging API On Railway

This note captures the first manual Railway deployment for issue `#38`.

Use it for:

- the Railway service shape for the ASP.NET API
- the required staging environment variables
- the separate staging migration workflow that runs before deploy when schema changes are present
- the first hosted smoke-check sequence

Related notes:

- [staging-hosting-decision.md](staging-hosting-decision.md)
- [staging-setup-runbook.md](staging-setup-runbook.md)
- [staging-db-operations.md](staging-db-operations.md)
- [staging-deployment-workflow.md](staging-deployment-workflow.md)

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

Use the separate GitHub Actions workflows when the deploy includes schema changes:

- auth workflow: `.github/workflows/auth-db-migrations.yml`
- app workflow: `.github/workflows/app-db-migrations.yml`
- trigger: manual `workflow_dispatch`
- dispatch input: `target_environment=staging`
- environment: `staging`
- required secrets:
  - auth workflow: `AUTH_DATABASE`
  - app workflow: `APP_DATABASE`

These workflows:

- checks out trusted `main`
- builds a trusted `Langoose.DbTool` Docker image within that run
- applies EF migrations directly through `Langoose.DbTool` using the normal `.NET` connection string
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

1. run the auth migration workflow so the auth DB catches up to trusted `main`
2. run the app migration workflow so the app DB catches up to trusted `main`
3. deploy the Railway API service
4. run the hosted smoke checks

Base-content seeding remains a separate maintenance operation rather than part of the normal Railway deploy path.

For the GitHub-driven deploy orchestration that can run these steps together, use:

- workflow: `.github/workflows/deploy-environment.yml`
- staging trigger: automatic after a successful `CI` run for a push to `main`
- manual trigger: `workflow_dispatch` with `target_environment=staging|production` and optional `deploy_api` /
  `deploy_web` toggles
- unified workflow behavior: automatic staging runs always apply migrations and deploy both hosted apps after
  successful `CI`, while manual runs can choose lanes explicitly
- unified workflow scope: it is self-contained and does not depend on the standalone maintenance workflow YAML files
- required GitHub environment variable for Railway target selection: `RAILWAY_ENVIRONMENT`
- API deploy path: `railway up apps/api/src/Langoose.Api --path-as-root --ci ...`

## Manual Railway Setup

1. Create or open the staging Railway project.
2. Add a service for the API from this repository.
3. Point the service at `/apps/api/src/Langoose.Api/railway.json`.
4. Set the required variables.
5. Run the needed auth and app migration workflows first when the release includes schema changes.
6. Review the staged changes and deploy.

## First Smoke Checks

After the first deploy succeeds, verify:

1. `GET /health` returns `200`
2. `GET /auth/antiforgery` returns `200`
3. `POST /auth/sign-up` succeeds for a fresh test user
4. `GET /auth/me` succeeds after sign-in with the returned auth cookie

These checks are enough for `#38` to unblock the web deployment issue.
