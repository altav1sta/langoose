# Langoose Roadmap

This document is the readable roadmap overview for the project. The live execution board stays in the GitHub
`Langoose MVP` project, while detailed implementation work lives in GitHub issues and pull requests.

## Product Goal

Ship a deployable MVP for Russian speakers learning English that supports:

- real user accounts
- an auth foundation that does not block future native/mobile clients
- durable persisted data
- dictionary management
- study sessions with tolerant grading
- a repeatable deployment path

## MVP Definition

The MVP is done when:

- the app is deployable outside local development
- data persists in PostgreSQL across restarts and redeploys
- users can sign in, sign out, and keep their own data
- the backend auth model does not depend on a web-only dead end
- dictionary and study flows work end to end
- staging deployment exists before production launch
- core changes are validated through GitHub pull requests and CI

## Roadmap Phases

### M1 Foundation

Build the technical base required for a deployable MVP.

- Add a Docker-based local development stack
- Add CI checks for backend and frontend
- Add pull request workflow hygiene
- Replace the JSON store with PostgreSQL-backed persistence
- Establish an OAuth/OIDC-capable authentication foundation for the web app that will not block later native/mobile support
- Deploy the first staging environment

### M2 Core Learning

Complete the core product loop for a returning learner.

- dictionary search, browse, and quick add
- study sessions backed by persisted progress
- user-scoped vocabulary and study data
- progress visibility for daily use
- web auth and user flows solid in staging and real usage

### M3 Deployable Beta

Harden the app for broader real-world use.

- CSV import and export on top of the database-backed model
- duplicate-handling rules preserved in the new persistence model
- one external identity provider, such as Google, on top of the shared auth substrate
- staging stability and deployment confidence
- unresolved MVP gaps closed from testing and user feedback

### M4 Launch

Prepare the app for a first public release.

- production deployment readiness
- bug fixing and polish
- basic operational runbooks
- backup and recovery confidence for persisted data

## Current Priorities

The current implementation order is:

1. Docker-based local development stack
2. CI checks for backend and frontend
3. Pull request workflow notes and template
4. PostgreSQL-backed persistence
5. OAuth/OIDC-capable authentication foundation
6. First staging deployment

## Guiding Decisions

These decisions currently shape the roadmap:

- GitHub Projects is the source of truth for roadmap execution.
- This document is the one-page roadmap summary.
- Docker is the local and deployment packaging layer.
- PostgreSQL replaces the JSON file store for deployable persistence.
- The auth foundation should support the web app now without blocking native/mobile later.
- First-party email/password is the initial login method, but not the final architecture boundary.
- Managed hosting is preferred over self-hosting the production database in Docker.

## Working Model

- Use GitHub issues for executable work.
- Use epics for roadmap-level outcomes.
- Use pull requests to merge small, reviewable changes into `main`.
- Keep `main` deployable.
- Record deeper architectural choices separately as decision notes and implementation blueprints, such as:
  - `docs/auth-mvp-decision.md`
  - `docs/auth-m1-implementation-blueprint.md`
