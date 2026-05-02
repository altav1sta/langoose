# Staging Worker On Railway

This note captures the Railway deployment for the background worker service
([issue #89](https://github.com/altav1sta/langoose/issues/89)).

Use it for:

- the Railway service shape for the `Langoose.Worker` host
- the required staging environment variables
- where the worker fits in the staging release sequence

Related notes:

- [staging-hosting-decision.md](staging-hosting-decision.md)
- [staging-setup-runbook.md](staging-setup-runbook.md)
- [staging-db-operations.md](staging-db-operations.md)
- [staging-deployment-workflow.md](staging-deployment-workflow.md)
- [staging-api-railway.md](staging-api-railway.md)

## Service Shape

Create one Railway service for the worker in the staging environment, separate
from the API service. Both services live in the same Railway project and the
same Railway environment.

Use the repo's Railway config as code file:

- config file path: `/apps/api/src/Langoose.Worker/railway.json`

That file defines:

- Dockerfile deploy from `apps/api/src/Langoose.Worker/Dockerfile`
- watch scope limited to backend build inputs
- no HTTP healthcheck — the worker has no HTTP surface

The worker polls `background_jobs` for `Pending` rows and dispatches them to
the matching handler (see `docs/agent/workflows.md` → "Bulk dictionary
pipeline"). It shares the same app database as the API; deploying it on the
same Neon connection means submitting a job from any client (CLI, future
admin API) is enough to make the worker pick it up.

## Required Variables

Set these service variables in Railway, on the worker service only:

- `DOTNET_ENVIRONMENT=Staging`
- `ConnectionStrings__AppDatabase=<Neon staging connection string for langoose_app>`
- `ConnectionStrings__CorpusDatabase=<Neon staging connection string for langoose_corpus>`
- `PGGSSENCMODE=disable`

The worker does not need the auth database, CORS, or forwarded-header
configuration — it has no HTTP surface and never serves user requests.

`PGGSSENCMODE=disable` matches the API service and is set for the same reason
(see [staging-api-railway.md](staging-api-railway.md) → "Required Variables").
Flip it back to `prefer` only if the database moves behind Active Directory /
Kerberos auth, which would also require installing `libgssapi-krb5-2` in the
runtime image.

## Deploy Sequence

The unified deploy workflow handles the worker alongside the API:

- `.github/workflows/cd.yml` detects worker-relevant changes
  (`Langoose.Worker/`, `Langoose.Corpus.Data/`, or shared layers Core/Data/Domain)
  and sets `deploy_worker=true`
- `.github/workflows/deploy-environment.yml` runs migrations first, then
  deploys the API and the worker in parallel
- the worker deploy step uses `railway up` with the `RAILWAY_WORKER_SERVICE`
  variable to target the worker service explicitly

Because the worker and API run on the same database, the deploy order is:

1. auth migrations
2. app migrations
3. API deploy and worker deploy (parallel)
4. web deploy when applicable

## Manual Railway Setup

1. Open the staging Railway project that already hosts the API service.
2. Add a new service for the worker from this repository.
3. Point the service at `/apps/api/src/Langoose.Worker/railway.json`.
4. Set the required variables above.
5. Verify the service builds and the polling log lines `UserEntriesImportJob is starting.` and `CorpusImportJob is starting.` appear in the deploy logs.

## Smoke Check

After the first deploy succeeds, confirm the worker is alive by submitting a
small job and watching it transition states:

1. Run `submit-corpus-import --lang en --source <id>` against the staging app database (see
   `docs/agent/workflows.md` for the CLI command shape and required
   `ConnectionStrings__AppDatabase`).
2. Run `list-jobs --type CorpusImport` against the same database — the row should
   advance from `Pending` to `Running` to `Completed`.
3. Inspect the worker logs in Railway for the `Dispatching corpus-import job
   {JobId}` and per-batch progress lines.

## Required GitHub Variables

Add to the `staging` GitHub environment:

- `RAILWAY_WORKER_SERVICE` — Railway worker service name or ID

The existing `RAILWAY_PROJECT_ID`, `RAILWAY_ENVIRONMENT`, and `RAILWAY_TOKEN`
secrets are reused; both services share the same Railway project, environment,
and project token.
