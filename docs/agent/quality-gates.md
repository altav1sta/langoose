# Quality Gates

## File Hygiene

- Respect [`.gitattributes`](../../.gitattributes) and [`.editorconfig`](../../.editorconfig).
- Keep files UTF-8, preserve non-ASCII product text safely, and avoid line-ending-only churn.
- Prefer a 120-character line length unless the file format or local readability clearly justifies an exception.
- Do not leave stray leading blank lines at the top of source, config, solution, or documentation files.
- When editing Markdown under `docs/`, verify links relative to the file's real directory.

## Validation

- Run the smallest relevant build, test, and acceptance checks for the change.
- Run E2E tests (`docker compose --profile e2e up --build e2e`) as part of full validation when the change affects cross-layer behavior.
- Prefer containerized whole-app validation when the change affects startup, persistence, auth, or cross-app behavior.
- If a validation lane is blocked or fails, say so plainly and do not report it as passing by inference.
- When a change renames or moves a project, Dockerfile, or test assembly, update the affected GitHub Actions workflows, contributor commands, and repo guidance docs in the same change.
- When a change touches domain entities, DbContext, or EF configurations, generate or update the EF migration in the same change. In-memory tests do not validate migrations.
- When a change alters API endpoints, request shapes, or response shapes, update the frontend API types and calls in the same change. Backend tests do not validate frontend contract alignment.
- Validate every CI lane the change will trigger — not just the lanes that are convenient to run locally.
- Resolve code inspection and style analyzer warnings in the affected files before considering work done.

## CI Alignment

GitHub Actions workflows under `.github/workflows/` reference project paths,
Dockerfile paths, and test project names directly. When a change renames or
moves a project, Dockerfile, or test assembly, update the affected workflow
files in the same change to keep CI green.

## Practical Cautions

- Avoid relying on `bin`, `obj`, `.vs`, `.dotnet`, and `node_modules` as if they were source files.
- If a change touches API behavior, inspect tests under `apps/api/tests` because they encode product decisions clearly.
- If frontend and backend contracts move together, update `apps/web/src/api.ts` in the same change.
- For auth, cookie, antiforgery, OpenIddict, forwarded-header, or hosted environment changes, use `langoose-auth-hosting` and keep the related `docs/` guidance aligned.
