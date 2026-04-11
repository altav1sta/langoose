# Langoose Roadmap

This document is the readable roadmap overview for the project. The live execution board stays in the GitHub
`Langoose MVP` project, while detailed implementation work lives in GitHub issues and pull requests.

## Product Goal

Ship a deployable MVP for Russian speakers learning English that supports:

- real user accounts with social login
- an auth foundation that does not block future native/mobile clients
- durable persisted data
- dictionary management with AI-powered enrichment
- study sessions with tolerant grading and spaced repetition
- progress tracking and daily motivation
- a repeatable deployment path

## MVP Definition

The MVP is done when:

- the app is deployable outside local development
- data persists in PostgreSQL across restarts and redeploys
- users can sign in (email/password or Google), sign out, and keep their own data
- the backend auth model does not depend on a web-only dead end
- dictionary and study flows work end to end with enriched content
- custom words are enriched asynchronously and reusable across users
- study sessions have structure, goals, and visible progress
- staging deployment exists before production launch
- core changes are validated through GitHub pull requests and CI

## Roadmap Phases

### M1 Foundation (done)

Build the technical base required for a deployable MVP.

- Add a Docker-based local development stack
- Add CI checks for backend and frontend
- Add pull request workflow hygiene
- Replace the JSON store with PostgreSQL-backed persistence
- Establish an OAuth/OIDC-capable authentication foundation for the web app that will not block later native/mobile support
- Deploy the first staging environment

### M2 Core Learning

Complete the core product loop for a returning learner.

**Dictionary and content**

- Expand the base dictionary to 3,000-5,000 entries (A1 through B2, frequency-ordered) via one-time bulk LLM generation
- Each base entry includes: English term, Russian glosses, example sentences with cloze gaps, difficulty, part of speech, accepted variants
- Iterate on base content quality through the existing content flagging system

**Domain model and architecture (#68)**

- Restructure the solution into onion architecture (Domain, Core, Data, Api, Worker)
- Replace the flat DictionaryItem model with SharedItem (enriched content), UserItem (per-user entries), Gloss/GlossSurfaceForm (language-agnostic morphological normalization)
- Sentence-based study cards with per-sentence difficulty, grammar hints, and expected answer forms
- Services use domain models only; DTOs map in the presentation layer

**AI-powered enrichment (#56, depends on #68)**

- Async background enrichment for custom words added by users
- SharedItem is the shared content layer: only enriched/validated content lives there, reusable across users
- UserItem owns the enrichment lifecycle (pending/failed state); linked to SharedItem once enriched
- GlossSurfaceForm dedup: morphological variants resolved to canonical form by LLM, cached for instant lookup
- Provider: best available free-tier LLM (currently Gemini Flash), abstracted behind IEnrichmentProvider
- Batch processing within external API limits for CSV imports and bulk operations
- Rate limiting to stay within API quotas and prevent abuse

**Study sessions**

- Structured sessions with defined batch size and end-of-session summary (cards reviewed, accuracy, new words)
- Option to extend/continue a session beyond the default batch
- Study sessions backed by persisted progress
- User-scoped vocabulary and study data

**Progress and motivation**

- Streak counter (consecutive days studied)
- Daily word goal with progress indicator
- Progress dashboard with due/new/studied counts

**Auth and access**

- Google OAuth as an external identity provider on top of the existing OpenIddict substrate
- Web auth and user flows solid in staging and real usage

**Frontend**

- Responsive layout for mobile browsers
- Dictionary search, browse, and quick add

### M3 Deployable Beta

Harden the app for broader real-world use.

**Scheduling**

- Adopt FSRS (Free Spaced Repetition Scheduler) to replace the current fixed-interval algorithm
- Document all implementation decisions and algorithm parameters thoroughly
- Error analytics: frequently missed words feed back into scheduling and surface more often

**Onboarding**

- Placement test on first login: 20-30 words of increasing difficulty, user marks known ones
- Known words start with high stability; study begins at the user's actual level

**Content and data**

- CSV import and export on top of the database-backed model
- Duplicate-handling rules preserved in the new persistence model
- Study history export (beyond dictionary CSV)

**Operations**

- Basic admin tooling for reviewing flagged content and managing the base dictionary
- Improve application logging for staging and production diagnostics
- Propagate cancellation tokens consistently through API and backend flows
- Staging stability and deployment confidence
- Unresolved MVP gaps closed from testing and user feedback

### M4 Launch

Prepare the app for a first public release.

- Production deployment readiness
- Bug fixing and polish
- Basic operational runbooks
- Backup and recovery confidence for persisted data

### Post-MVP

Features to explore after launch, driven by user feedback and usage patterns.

- Audio playback / text-to-speech for pronunciation
- PWA capabilities (offline study, installable app)
- Advanced learning analytics (retention graphs, learning curves)
- Additional identity providers

## Guiding Decisions

These decisions currently shape the roadmap:

- GitHub Projects is the source of truth for roadmap execution.
- This document is the one-page roadmap summary.
- Docker is the local and deployment packaging layer.
- PostgreSQL replaces the JSON file store for deployable persistence.
- The auth foundation should support the web app now without blocking native/mobile later.
- First-party email/password is the initial login method, with Google OAuth added in M2.
- Managed hosting is preferred over self-hosting the production database in Docker.
- Enrichment is a shared content layer: AI-generated content is reusable across users, only user customizations are private.
- The content flagging system is the primary feedback mechanism for base dictionary quality.
- All implementation decisions for algorithms (grading, scheduling, enrichment) must be documented.

## Working Model

- Use GitHub issues for executable work.
- Use epics for roadmap-level outcomes.
- Use pull requests to merge small, reviewable changes into `main`.
- Keep `main` deployable.
- Record deeper architectural choices separately as decision notes and implementation blueprints, such as:
  - `docs/auth-mvp-decision.md`
  - `docs/auth-m1-implementation-blueprint.md`
