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
- Preserve the local JSON-backed store by using a mounted volume or explicit data path, not by baking mutable runtime data into the image.

## Langoose-Specific Guidance

- The API currently persists to `App_Data/store.json`. Container setups must treat that data as runtime state.
- The frontend talks to the API over HTTP, so Compose or env wiring must make the API base URL explicit.
- Vite dev servers in containers usually need host binding that works outside the container.
- Keep local-dev ergonomics in mind: fast rebuilds, readable logs, and simple startup commands matter more than premature production complexity.

## Workflow

- Decide whether the task is dev-only containers, production-style images, or both.
- Inspect `apps/api` and `apps/web` build/run commands before writing Dockerfiles.
- Add `.dockerignore` alongside Docker support so image contexts do not include `node_modules`, `bin`, `obj`, `.git`, or runtime data.
- If Compose is used, prefer health-aware dependency wiring instead of blind startup ordering.

## Load Additional Detail Only When Needed

- For Docker and Compose best practices mapped to this repo, read [D:\Projects\langoose\.codex\skills\langoose-docker\references\docker-guidance.md](D:\Projects\langoose\.codex\skills\langoose-docker\references\docker-guidance.md).
