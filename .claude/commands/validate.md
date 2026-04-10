---
description: Run the smallest relevant build and test checks for the current change.
allowed-tools: Bash(dotnet *) Bash(npm *) Bash(npx *) Bash(docker *) Bash(git *) Bash(pwsh *)
---

Read `docs/agent/workflows.md` for the canonical commands and `CLAUDE.md` for file
hygiene rules. Then determine what changed (use `git diff --name-only HEAD` and
staged files) and run only the relevant checks:

- **Backend** (`apps/api/` changes): build the solution, run unit tests, run integration tests.
- **Frontend** (`apps/web/` changes): run tests and build.
- **Both**: backend first, then frontend.
- **Docker/infra**: `docker compose build`. If startup, persistence, auth, or cross-app
  behavior changed, prefer `docker compose up --build` and verify the key flow works.
- **E2E**: when auth, persistence, or cross-app behavior changed, run
  `docker compose --profile e2e up --build e2e` if the e2e profile is available.
- **Docs only**: confirm markdown links are valid relative to the file's directory.

After the build and test pass, also check:

- **Line endings**: if new files were created or files were moved, verify they don't
  have mixed line endings (`git diff --check`).
- **File hygiene**: no stray leading blank lines at the top of source files.

- **Guidance Index**: list files in `docs/agent/` and compare against the Guidance
  Index table in `CLAUDE.md`. Report any docs missing from the table or table entries
  pointing to files that no longer exist.

Report results concisely. If anything fails, show the relevant error output.
