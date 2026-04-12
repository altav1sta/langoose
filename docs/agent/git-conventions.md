# Git Conventions

## Issue and Epic Lifecycle

- Move an issue to "In Progress" on the project board **before writing any code** — this is step zero.
- If the issue is a sub-issue of an epic, move the epic to "In Progress" too (if not already).
- Move an issue to "In Review" when its PR is created.
- After merge, verify the project board moves the issue to "Done".

## Naming Conventions

### Commits

Follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/):
`<type>[optional scope]: <description>`. Common types: `feat`, `fix`, `docs`,
`refactor`, `test`, `chore`, `infra`. Use a scope when the change targets a
specific layer or area, e.g. `feat(study):`, `fix(api):`.

### Branches

Use the conventional type as prefix, then the issue number, then a short
kebab-case description: `<type>/<number>-<description>`.

- `docs/42-enrichment-guidance`
- `infra/55-docker-compose-pg`
- `feat/72-update-frontend-new-api`
- `fix/30-cancellation-tokens`
- `chore/99-dependency-updates`

### PR Titles

Use the same conventional type prefix as commits:
`<type>[optional scope]: <description> (#<number>)`.

Do not add AI attribution lines (Co-Authored-By, "Generated with Claude Code")
to commits or PRs.

## Git Hygiene

- Always use `git add -A` when staging. Never cherry-pick files by name — all
  changed files are part of the commit, including `.claude/settings.local.json`.
- Before creating a PR, check `gh pr list --head <branch>` first. The branch
  may already have a PR from a previous session.
- When creating a PR, set assignee, labels, and milestone to match the linked
  issue.
- Use squash merge into `main`.
- Start new work from the latest `main`.
- Use one focused branch per issue or tightly related change whenever practical.
- When creating sub-issues for an epic, link them as real GitHub sub-issues
  using the `addSubIssue` GraphQL mutation — not just checklist text in the
  epic body.
- Assign the repo owner to every new issue and epic.
