# Staging DB Operations

This note defines the staging PostgreSQL topology and the operational model for issue `#37`.

Use it for:

- the Neon project, branch, and database layout
- the required staging secrets
- the migration and seeding policy
- the recommended reset, wipe, inspection, and recovery procedures

Related notes:

- [staging-api-railway.md](staging-api-railway.md)

## Topology

Use the existing Neon project for the environment boundary and a dedicated long-lived staging branch:

- Neon project: `Langoose`
- branch: `staging`

Use two PostgreSQL databases inside that staging branch:

- app database: `langoose_app`
- auth database: `langoose_auth`

This keeps the runtime connection-string names aligned with local development while still separating staging from local
and future production at the Neon branch boundary.

## Required Secrets

The API already expects these connection string keys:

- `ConnectionStrings:AppDatabase`
- `ConnectionStrings:AuthDatabase`

In hosted environments, set them through:

- `ConnectionStrings__AppDatabase`
- `ConnectionStrings__AuthDatabase`

Each should be a full PostgreSQL connection string for the correct Neon database.

## Startup And Migration Policy

Local development and local Docker can keep automatic startup initialization.

Staging should follow the same operational model as production:

- normal API startup should not be responsible for staging database migrations
- normal API startup should not be responsible for staging base-content seeding
- migrations and any required seeding should happen as an explicit pre-start or maintenance process

The intended staging flow is:

1. create or select the Neon databases
2. run the explicit migration step before deploy when schema changes are present
3. start the API with normal startup initialization disabled

For the staging API on Railway, the explicit migration steps are separate GitHub Actions workflows:

- auth workflow: `.github/workflows/auth-db-migrations.yml`
- app workflow: `.github/workflows/app-db-migrations.yml`
- dispatch input: `target_environment=staging`
- staging environment secrets:
  - auth workflow: `AUTH_DATABASE`
  - app workflow: `APP_DATABASE`
- execution model: manually trigger the needed workflow, build a trusted `Langoose.DbTool` Docker image from `main`,
  then apply EF migrations directly against the selected environment

Base-content seeding stays separate and should be run only when the environment is first prepared or recreated.

For staging, the explicit base-content seeding step is the separate GitHub Actions workflow:

- workflow: `.github/workflows/app-seed.yml`
- dispatch input: `target_environment=staging`
- staging environment secret: `APP_DATABASE`
- execution model: manually trigger the workflow, build a trusted `Langoose.DbTool` Docker image from `main`, then run
  `seed-app` in that image against the selected environment's app database

## Operational Procedures

### Inspect

Use Neon SQL access or a normal PostgreSQL client against the staging databases for:

- schema inspection
- row inspection
- migration validation
- targeted debugging

### Full Reset

This is the default safe reset path.

Recommended procedure:

1. recreate the staging Neon branch or recreate both staging databases
2. run the explicit auth and app migration steps
3. run base-content seeding when the rebuilt environment needs it
4. start the API with startup initialization disabled

Prefer this over manual in-place deletes when the environment is no longer trustworthy.

### Wipe Staging User Data

For staging, the safest wipe strategy is still full environment recreation unless a smaller wipe is clearly needed.

If a smaller wipe is needed:

- preserve base dictionary content
- treat auth and app data together as one user-data boundary
- document the exact SQL or scripted procedure before running it against shared staging

Until dedicated maintenance tooling exists, prefer a full reset over ad hoc partial cleanup.

### Reseed Base Content

The base dictionary seeder is empty-database-only. If dictionary data already exists, the seeding step exits without
rewriting or repairing existing rows.

Recommended reseed path:

1. ensure the app database is intentionally empty or freshly recreated
2. ensure app schema is current
3. run the base-content seeding step
4. verify base dictionary content

Do not rely on reseeding as a substitute for a full reset when auth or schema state is questionable.

### Recovery

When staging becomes unreliable after a failed migration, bad data change, or accidental cleanup:

- prefer restoring or recreating the Neon staging branch or database set
- rerun the explicit auth and app migration steps
- rerun base-content seeding when the recreated environment needs it
- only then reopen the environment for general use

## Why This Model

This model keeps staging operations predictable:

- one Neon branch represents the staging environment boundary
- two databases preserve the current app/auth separation
- connection string names stay aligned with the current API runtime
- staging follows the same explicit migration-before-deploy model as production

That gives later deployment issues a concrete and low-surprise PostgreSQL target.
