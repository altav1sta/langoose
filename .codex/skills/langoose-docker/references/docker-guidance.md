# Langoose Docker Guidance

## Official Guidance Applied Here

- Docker recommends multi-stage builds to reduce final image size and separate build-time tooling from runtime artifacts.
- Docker recommends using small trusted base images, excluding unnecessary files with `.dockerignore`, and keeping containers as ephemeral as possible.
- Docker Compose supports `depends_on` conditions such as `service_healthy`, which is better than assuming dependency readiness from startup order alone.
- Microsoft Learn's ASP.NET Core Docker guidance uses multi-stage builds for ASP.NET Core apps.

## What That Means For Langoose

- Prefer separate images for:
  - `apps/api`
  - `apps/web`
- For the API, use an SDK image to build/publish and a runtime image to run.
- For the frontend, decide explicitly between:
  - a dev-oriented Vite container, or
  - a production-style static-serving image
- Do not copy mutable runtime data or local database state into an image as if they were source artifacts.
- If the API uses PostgreSQL, keep the database as a separate container or external service and persist its data through a
  volume on the database service rather than the API container filesystem.
- If Compose is used, expose the API and frontend ports explicitly and pass the frontend API base URL through env configuration.

## Recommended Defaults For This Repo

- Use Linux containers unless there is a Windows-specific requirement.
- Use multi-stage Dockerfiles.
- Add `.dockerignore` early.
- Keep one primary concern per container.
- Prefer explicit healthchecks for the API if another service depends on it.
- For local development, prioritize bind-mounted source and fast feedback loops only if the workflow stays reliable on Windows.

## Practical Review Checklist

- Does the Dockerfile copy only what it needs, in a cache-friendly order?
- Is mutable runtime data excluded from the image and persisted by the database service volume or another external path?
- Is the frontend API URL explicit and correct for the container network?
- If Compose is used, are dependencies health-aware instead of only startup-ordered?
- Are image contexts excluding `node_modules`, `bin`, `obj`, `.git`, `.vs`, `.local`, and other irrelevant files?

## Official Sources

- Docker: [Building best practices](https://docs.docker.com/develop/develop-images/dockerfile_best-practices/)
- Docker: [Multi-stage builds](https://docs.docker.com/build/building/multi-stage/)
- Docker: [Control startup order in Compose](https://docs.docker.com/compose/how-tos/startup-order/)
- Microsoft Learn: [Run an ASP.NET Core app in Docker containers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-10.0)
