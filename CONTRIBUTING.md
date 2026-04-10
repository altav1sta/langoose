# Contributing

Keep the workflow lightweight and keep `main` healthy.

The basic expectations are:

- start from the latest `main`
- use one focused branch per issue or tightly related change
- keep pull requests small enough to review comfortably
- run the smallest useful validation for the change
- keep docs and repo guidance in sync when behavior or workflow changes

## Branches

Create a short, descriptive branch from `main`.

Preferred prefixes:

- `feat/...` for product or implementation work
- `fix/...` for bug fixes
- `infra/...` for CI, Docker, deployment, or workflow changes
- `docs/...` for documentation and repo-guidance changes
- `chore/...` for maintenance

## Pull Requests

Every pull request should include:

- a short summary of the change
- the linked issue
- the validation you ran
- any reviewer notes that explain tradeoffs, follow-ups, or known gaps

Use closing keywords when appropriate, for example `Closes #5`.

The repo includes a PR template at [.github/pull_request_template.md](.github/pull_request_template.md).

## Issues And Project Status

When you start work on an issue:

- assign the issue if that is part of your workflow
- move the project item to `In Progress` if the project board is being used

When a pull request is open:

- link the issue in the PR body
- move the project item to `In Review` if the project board is being used

When the pull request merges:

- confirm the linked issue closes
- move the project item to `Done` if it does not update automatically

## Validation

Run the smallest relevant checks for the change.

Common backend checks:

```powershell
dotnet build apps/api/Langoose.sln /p:RestoreConfigFile=apps/api/NuGet.Config
dotnet test apps/api/tests/Langoose.Api.UnitTests/Langoose.Api.UnitTests.csproj /p:RestoreConfigFile=apps/api/NuGet.Config
dotnet test apps/api/tests/Langoose.Api.IntegrationTests/Langoose.Api.IntegrationTests.csproj /p:RestoreConfigFile=apps/api/NuGet.Config
```

Common frontend checks:

```powershell
cd apps/web
npm run test
npm run build
```

Whole-app and browser-facing checks:

```powershell
docker compose up --build
docker compose --profile e2e up --build e2e
```

If you do not run a check, say so in the pull request.

## CI

The main CI workflow currently runs these checks when relevant:

- `Backend Build`
- `Backend Unit Tests`
- `Backend Integration Tests`
- `Frontend Tests`
- `Frontend Build`
- `E2E`

See [.github/workflows/ci.yml](.github/workflows/ci.yml) for the current definitions.

## Merge Expectation

- keep unrelated changes out of the same PR
- do not merge known-broken changes into `main`
- use squash merge into `main`
