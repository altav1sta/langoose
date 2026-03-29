---
name: langoose-docker
description: Add or maintain Docker support for the Langoose repo. Use when creating Dockerfiles, .dockerignore, Docker Compose files, containerized local dev workflows, image build strategy, healthchecks, or persistence mounts for the ASP.NET Core API and React/Vite frontend.
---

# Langoose Docker

Read [D:\Projects\langoose\AGENTS.md](D:\Projects\langoose\AGENTS.md) first.

Use this skill when the task involves Docker, Compose, or containerized local development.

## Main Rules

- Containerize the API and web app as separate concerns.
- Use multi-stage builds for production-style images.
- Add `.dockerignore` files or root exclusions to keep build contexts small.
- Keep Dockerfiles environment-agnostic. Put local-dev connection strings, hostnames, ports, and other environment-
  specific runtime values in Compose or external env configuration, not in the image itself.
- Do not leave generated build output or IDE cache artifacts in the repo after Docker-related validation.

## Langoose-Specific Guidance

- Follow the persistence approach currently present in the repo. If the API uses PostgreSQL, wire containers around the
  database service and verify the actual connection path instead of preserving older JSON-store assumptions.
- The frontend talks to the API over HTTP, so Compose or env wiring must make the API base URL explicit.
- Vite dev servers in containers usually need host binding that works outside the container.
- Keep local-dev ergonomics in mind: fast rebuilds, readable logs, and simple startup commands matter more than premature production complexity.

## Workflow

- Decide whether the task is dev-only containers, production-style images, or both.
- Inspect `apps/api` and `apps/web` build/run commands before writing Dockerfiles.
- Add `.dockerignore` alongside Docker support so image contexts do not include `node_modules`, `bin`, `obj`, `.git`, or runtime data.
- If Compose is used, prefer health-aware dependency wiring instead of blind startup ordering.
- When changing persistence or startup wiring, verify the full container path: database healthy, API starts, migrations or
  schema creation actually run, and the key user flow succeeds through HTTP.
- If live verification fails, debug the running container logs first and fix the real startup issue before declaring the
  Docker work complete.
- If a Docker verification step fails, is blocked by the environment, or does not complete, do not describe it as
  passing from memory or inference. Call out the gap, rerun it if possible, and only report success after the real check
  succeeds.

## Load Additional Detail Only When Needed

- For Docker and Compose best practices mapped to this repo, read [D:\Projects\langoose\.codex\skills\langoose-docker\references\docker-guidance.md](D:\Projects\langoose\.codex\skills\langoose-docker\references\docker-guidance.md).
