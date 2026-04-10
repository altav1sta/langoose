# Staging Deployment Workflow

This note captures the GitHub-driven staging deployment path for issue `#40`.

Use it for:

- the GitHub Actions workflow that coordinates staging updates
- the required GitHub environment secrets and variables
- the release sequence across migrations, API deploy, and web deploy
- the provider-specific trigger model for Railway and Vercel

Related notes:

- [staging-setup-runbook.md](staging-setup-runbook.md)
- [staging-db-operations.md](staging-db-operations.md)
- [staging-api-railway.md](staging-api-railway.md)
- [staging-web-vercel.md](staging-web-vercel.md)

## Workflow

The repo provides two deployment-related workflows:

- `.github/workflows/cd.yml` — triggers on push to `main`, runs CI then deploys to staging
- `.github/workflows/deploy-environment.yml` — reusable deploy pipeline, also manually runnable through
  `workflow_dispatch`

The CD workflow calls CI (`.github/workflows/ci.yml`) as a reusable workflow, then calls the deploy workflow for
staging. This keeps CI and deploy in a single pipeline run for main pushes without creating phantom workflow runs for
PR CI.

The CD workflow detects which parts of the app changed and passes granular deploy flags. When only frontend files
change, the deploy skips the DbTool image build, database migrations, and API deploy. Manual runs through
`deploy-environment.yml` can still choose whether to deploy the API, the web app, or both.

## What It Can Do

The workflow orchestrates these steps:

- auth database migrations
- app database migrations
- API deploy to Railway
- web deploy to Vercel

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
- `VERCEL_TOKEN`
  Vercel token allowed to build and deploy the target web project

### Variables

- `RAILWAY_PROJECT_ID`
  Railway project ID that contains the API service
- `RAILWAY_API_SERVICE`
  Railway API service name or ID
- `RAILWAY_ENVIRONMENT`
  Railway environment name or ID used by the API deploy step
- `VERCEL_ORG_ID`
  Vercel team or personal account ID for the web project
- `VERCEL_PROJECT_ID`
  Vercel project ID for the web app

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
- runs `railway up` in CI mode against the configured API service in the configured Railway environment
- deploys from the repo root so Railway can resolve the existing repo-root-relative config and Dockerfile paths

### Vercel

The workflow deploys the web app through the Vercel CLI from the checked-out workflow commit.

Vercel documents custom CI/CD workflows around `vercel pull` and `vercel deploy`, using `VERCEL_TOKEN`,
`VERCEL_ORG_ID`, and `VERCEL_PROJECT_ID` from CI. Sources:

- [Deploying GitHub projects with Vercel](https://vercel.com/docs/deployments/git/vercel-for-github)
- [Using the Vercel CLI for custom workflows](https://vercel.com/kb/guide/using-vercel-cli-for-custom-workflows)

Current workflow behavior:

- checks out the workflow commit
- pulls the target Vercel environment configuration from the repo root
- sends the checked-out source to Vercel through the CLI from the repo root
- lets Vercel perform the hosted build in its normal environment

## Recommended Staging Usage

For a normal staging release:

1. merge the change into `main`
2. let `.github/workflows/cd.yml` run CI and then deploy staging automatically
4. let the workflow always apply auth and app migrations
5. let the workflow deploy the API
6. let the workflow deploy the web app
7. trigger the separate seed workflow only when the app database is intentionally empty

For manual production dispatch:

- choose `target_environment=production`
- choose whether to deploy the API, the web app, or both
- auth and app migrations still run first

For manual staging dispatch:

- choose `target_environment=staging`
- choose whether to deploy the API, the web app, or both
- auth and app migrations still run first

For automatic staging runs after `CD` on `main`:

- changes under `apps/api/` trigger migrations and API deploy
- changes under `apps/web/` trigger web deploy
- changes outside `apps/` skip the deploy entirely
- migrations and DbTool image build only run when backend changes are detected

## Release Order

The workflow enforces this order:

1. validate environment configuration
2. auth migrations
3. app migrations
4. API deploy when selected
5. web deploy when selected

This keeps schema-changing work ahead of hosted deploy steps.

## Why This Shape

This model keeps deployment automation aligned with the actual staging architecture:

- database maintenance stays explicit
- staging deploys happen automatically from trusted `main`
- manual dispatch can target either staging or production
- API deploy uses Railway's supported CLI and project-token path
- web deploy uses Vercel's supported CLI path
- staging remains operable from GitHub without mixing provider-specific ad hoc steps into the normal release path
