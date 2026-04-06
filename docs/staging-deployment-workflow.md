# Staging Deployment Workflow

This note captures the GitHub-driven staging deployment path for issue `#40`.

Use it for:

- the GitHub Actions workflow that coordinates staging updates
- the required GitHub environment secrets and variables
- the release sequence across migrations, API deploy, and web deploy
- the provider-specific trigger model for Railway and Vercel

Related notes:

- [staging-db-operations.md](staging-db-operations.md)
- [staging-api-railway.md](staging-api-railway.md)
- [staging-web-vercel.md](staging-web-vercel.md)

## Workflow

The repo now provides one environment-deploy workflow:

- workflow: `.github/workflows/deploy-environment.yml`

It is:

- automatically triggered for `staging` on pushes to `main`
- manually runnable through `workflow_dispatch`

The staging path does not require per-step input toggles anymore.

## What It Can Do

The workflow orchestrates these steps:

- auth database migrations
- app database migrations
- API deploy to Railway
- web deploy trigger for Vercel

This keeps the update path explicit without folding base-content seeding into every deploy. Seeding remains a separate
maintenance workflow.

The unified deploy workflow builds the `Langoose.DbTool` image once and reuses that image across auth and app
migration jobs.

## Required GitHub Environment Setup

Create a GitHub environment for each target environment, starting with:

- `staging`

Use the same secret names in every environment.

### Secrets

- `AUTH_DATABASE`
  full auth database connection string in the same `.NET`/Npgsql format used by the API
- `APP_DATABASE`
  full app database connection string in the same `.NET`/Npgsql format used by the API
- `RAILWAY_TOKEN`
  Railway project token scoped to the target Railway environment
- `VERCEL_DEPLOY_HOOK_URL`
  Vercel Deploy Hook URL for the target web project and branch

### Variables

- `RAILWAY_PROJECT_ID`
  Railway project ID that contains the API service
- `RAILWAY_API_SERVICE`
  Railway API service name or ID

## Provider Trigger Model

### Railway

The workflow deploys the API through the Railway CLI using a project token.

The CLI supports project tokens for CI/CD through the `RAILWAY_TOKEN` environment variable, and `railway up` can target
specific services, environments, and projects. This makes it suitable for GitHub-driven deploys of the API from
trusted `main`. Sources:

- [Railway CLI](https://docs.railway.com/cli)
- [railway up](https://docs.railway.com/cli/up)

Current workflow behavior:

- checks out the resolved deployment ref
- installs the Railway CLI
- runs `railway up` in CI mode against the configured API service in the selected environment
- deploys `apps/api/src/Langoose.Api` explicitly with `--path-as-root` so the workflow does not rely on repo-root
  upload behavior or manual Railway root-directory setup

### Vercel

The workflow triggers the web deploy through a Vercel Deploy Hook.

Vercel documents Deploy Hooks as unique URLs that can trigger deployments for connected Git projects through an HTTP
`GET` or `POST` request, without requiring a new commit. Sources:

- [Deploying to Vercel](https://vercel.com/docs/deployments)
- [Deploy Hooks guide](https://vercel.com/kb/guide/set-up-and-use-deploy-hooks-with-vercel-and-headless-cms)

Current workflow behavior:

- sends a `POST` request to `VERCEL_DEPLOY_HOOK_URL`
- expects the hook to be configured for the trusted branch you want to deploy, normally `main`

## Recommended Staging Usage

For a normal staging release:

1. merge the change into `main`
2. let `.github/workflows/deploy-environment.yml` run automatically
3. let the workflow always apply auth and app migrations
4. let the workflow deploy the API and/or web app based on changed app context
5. trigger the separate seed workflow only when the app database is intentionally empty

The staging workflow always:

- applies auth migrations
- applies app migrations

For pushes to `main`, deploy lanes are selected like this:

- API deploy runs when API deployable inputs changed, such as `Langoose.Api`, `Langoose.Domain`, `Langoose.Data`,
  `Langoose.Auth.Data`, or backend build configuration files
- API deploy does not run for `Langoose.DbTool`-only changes
- web deploy runs when web deployable inputs changed, such as `apps/web/src`, `public`, or the web build
  configuration files
- web deploy does not run for frontend test-only changes under `__tests__`

For manual production dispatch:

- choose `target_environment=production`
- optionally set `deploy_ref` if you want a different ref than the one selected in the GitHub Actions UI
- both deploy lanes run
- auth and app migrations still run first

For manual staging dispatch:

- choose `target_environment=staging`
- optionally set `deploy_ref` if you want a different ref than the one selected in the GitHub Actions UI
- both deploy lanes run
- auth and app migrations still run first

## Release Order

The workflow enforces this order:

1. validate environment configuration
2. auth migrations
3. app migrations
4. API deploy when selected by deploy context
5. web deploy when selected by deploy context

This keeps schema-changing work ahead of hosted deploy steps.

## Why This Shape

This model keeps deployment automation aligned with the actual staging architecture:

- database maintenance stays explicit
- staging deploys happen automatically from trusted `main`
- manual dispatch can target either staging or production
- API deploy uses Railway's supported CLI and project-token path
- web deploy uses Vercel's supported deploy-hook path
- staging remains operable from GitHub without mixing provider-specific ad hoc steps into the normal release path
