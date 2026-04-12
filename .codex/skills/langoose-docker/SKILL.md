---
name: langoose-docker
description: Add or maintain Docker support for the Langoose repo. Use when creating Dockerfiles, .dockerignore, Docker Compose files, containerized local dev workflows, image build strategy, healthchecks, or persistence mounts for the ASP.NET Core API and React/Vite frontend.
---

# Langoose Docker

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill when the task involves Docker, Compose, or containerized local development.

## Use When

- The task adds or edits Dockerfiles, Compose, or containerized dev flow.
- The task needs container validation for startup, persistence, auth, or cross-app wiring.

## Primary Doc

- [docker-guidance.md](../../../docs/agent/docker-guidance.md)

## Related Docs

- [workflows.md](../../../docs/agent/workflows.md)
- [quality-gates.md](../../../docs/agent/quality-gates.md)

## Critical Reminders

- Containerize the API and web app as separate concerns.
- Use multi-stage builds for production-style images.
- Add `.dockerignore` files or root exclusions to keep build contexts small.
- Keep Dockerfiles environment-agnostic. Put runtime-specific values in Compose or external env configuration.
- Decide whether the task is dev-only containers, production-style images, or both.
- Inspect `apps/api` and `apps/web` build/run commands before writing Dockerfiles.
- Add `.dockerignore` alongside Docker support so image contexts do not include `node_modules`, `bin`, `obj`, `.git`, or runtime data.
- If Compose is used, prefer health-aware dependency wiring instead of blind startup ordering.
- When changing persistence or startup wiring, verify the full container path: database healthy, API starts, migrations or schema creation actually run, and the key user flow succeeds through HTTP.
