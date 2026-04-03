# Contributing

Langoose keeps the workflow intentionally lightweight for MVP development. The goal is to keep `main` deployable,
make pull requests easy to review, and keep issue and board status in sync with real work.

## Branch workflow

1. Start from `main`.
2. Create a focused branch for one issue or one tightly related change.
3. Keep changes small enough to review without unpacking unrelated work.
4. Open a pull request back into `main`.

Preferred branch naming is descriptive and short, for example:

- `infra/ci-checks`
- `infra/web-dockerfile`
- `docs/pr-template-and-contrib-notes`

Issue numbers can be linked in the PR body instead of the branch name.

## Pull request expectations

Every PR should include:

- a short summary of what changed
- the linked GitHub issue
- the validation you ran
- any reviewer context that helps explain tradeoffs or known follow-ups

Use closing keywords when the PR should close the issue, for example `Closes #5`.

The repository includes a pull request template to keep this consistent.

## Issues and board status

When you start work on an issue:

- assign the issue
- move the board item to `In Progress`

When the PR is open:

- link the issue in the PR body
- mirror relevant assignees, labels, and milestone onto the PR
- move the board item to `In Review`

When the PR merges:

- confirm the linked issue closes
- move the board item to `Done` if it does not update automatically

## Validation

Run the smallest useful checks for the change.

Common validation commands:

```powershell
dotnet build apps/api/Langoose.sln /p:RestoreConfigFile=D:\Projects\langoose\apps\api\NuGet.Config
dotnet test apps/api/tests/Langoose.Api.Tests/Langoose.Api.Tests.csproj /p:RestoreConfigFile=D:\Projects\langoose\apps\api\NuGet.Config
cd apps/web
npm run build
```

If you do not run a check, say so in the PR.

## Keep main deployable

- Prefer small, reviewable pull requests.
- Avoid mixing unrelated changes in one branch.
- Keep documentation and workflow notes current when the process changes.
- Do not merge known-broken changes into `main`.
