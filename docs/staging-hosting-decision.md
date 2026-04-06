# Staging Hosting Decision

This note documents the current comparison for the first Langoose staging environment.

Use this file for:

- provider and stack comparison
- staging-specific tradeoff analysis
- the recommended first staging direction
- follow-up implementation issue shaping
- links to the concrete staging browser and database operation notes

This note is intentionally about staging, not final production architecture.

Related implementation notes:

- [staging-db-operations.md](staging-db-operations.md)
- [staging-api-railway.md](staging-api-railway.md)

## Table Of Contents

- [Snapshot](#snapshot)
- [Langoose Constraints](#langoose-constraints)
- [Decision Criteria](#decision-criteria)
- [Roadmap And Lifecycle Considerations](#roadmap-and-lifecycle-considerations)
- [Provider Shortlist](#provider-shortlist)
- [Provider Notes](#provider-notes)
- [Stack Variants Compared](#stack-variants-compared)
- [Recommended Direction](#recommended-direction)
- [Why This Wins](#why-this-wins)
- [Broader Roadmap Fit](#broader-roadmap-fit)
- [Environment Drift And Switching Cost](#environment-drift-and-switching-cost)
- [Uniformity Versus Managed Specialization](#uniformity-versus-managed-specialization)
- [Recommended Staging Browser Integration Model](#recommended-staging-browser-integration-model)
- [Concrete Outcome For Issue 36](#concrete-outcome-for-issue-36)
- [Implementation Checklist For Issue 36](#implementation-checklist-for-issue-36)
- [Operational Database Work](#operational-database-work)
- [Production Evolution And Orchestration](#production-evolution-and-orchestration)
- [Variants Not Chosen](#variants-not-chosen)
- [Follow-up Work After The Decision](#follow-up-work-after-the-decision)
- [Execution Sequence And Likely Risks](#execution-sequence-and-likely-risks)
- [Sources](#sources)

## Snapshot

| Topic | Current recommendation |
| --- | --- |
| Frontend host | Vercel |
| API host | Railway |
| PostgreSQL host | Neon |
| Recommended browser integration model | One staging browser origin using `/api` proxying or rewrites if feasible |
| Staging priority | Cheap but genuinely usable |
| Operational model | Managed services, GitHub-driven deploys, no self-managed database |
| Main reason | Best balance of cost, simplicity, and low-friction staging UX |

## Langoose Constraints

Langoose is not choosing a generic hosting stack in the abstract. The staging choice needs to fit the repo as it
actually exists now:

- React and Vite SPA under `apps/web`
- ASP.NET Core API under `apps/api/src/Langoose.Api`
- PostgreSQL-backed persistence
- separate app and auth databases on the same PostgreSQL server
- Docker already used as the local packaging and verification path
- GitHub-based workflow expected for deployment updates
- staging should be good enough to validate real browser-facing behavior, not just prove that a container starts

That makes some hosting shapes a better fit than others:

- frontend-only platforms are fine for the SPA, but not sufficient for the API
- free database offers that expire or lack persistence guarantees are a weak fit for a reusable staging environment
- platforms that are technically flexible but highly ops-shaped may be a poor tradeoff for early MVP staging

## Decision Criteria

The comparison below optimizes for these criteria, in this order:

1. The environment should be cheap enough to keep around during MVP development.
2. It should be manageable by one person without bespoke infrastructure work.
3. GitHub-driven deploys should be straightforward.
4. Logs, runtime status, and environment variables should be easy to inspect.
5. The stack should support a real ASP.NET API and a real PostgreSQL database cleanly.
6. Cold-start pain and free-tier instability should be acceptable for staging use.
7. The choice should not create unnecessary migration pain for later production work.

## Roadmap And Lifecycle Considerations

The first staging choice should not be treated as an isolated M1 decision. It affects later phases too:

- M1 Foundation needs a staging environment that is cheap and fast to stand up.
- M2 Core Learning needs that environment to support repeated real-browser validation of auth, user-scoped data, and
  everyday learner flows.
- M3 Deployable Beta needs staging stability, confidence in updates, and enough operational visibility to diagnose real
  user-facing failures.
- M4 Launch will likely require a more production-shaped hosting story, stronger operational runbooks, and clearer
  rollback and recovery expectations.

That means the best M1 staging option is not automatically the best forever option. The right question is:

- which option gives the best first staging environment
- while minimizing painful migration or environment drift later

The decision also needs to account for how expensive it is to:

- keep staging and production on different providers
- keep frontend and backend on different providers
- move the API later without rewriting deployment assumptions
- move the database later without excessive operational risk

## Provider Shortlist

These are the realistic providers for the first staging environment:

- Vercel for the frontend
- Netlify for the frontend
- Railway for app hosting
- Render for app hosting
- Neon for PostgreSQL
- Azure App Service and Azure Database for PostgreSQL
- Fly.io
- self-managed VPS plus Docker

## Provider Notes

### Vercel

Vercel is a strong frontend host for a React/Vite app with GitHub deploys and good preview workflow. As of April 5,
2026, the Hobby plan is still free and includes automatic CI/CD, CDN delivery, firewall features, and usage dashboards.

Strengths:

- excellent GitHub-to-preview-to-production workflow
- strong frontend developer experience
- generous enough free entry point for staging web hosting
- simple custom domain and HTTPS path

Weaknesses:

- not the natural place to host the current ASP.NET API
- a split-platform setup is still required for the backend and database

Best fit in this repo:

- frontend host only

### Netlify

Netlify is also a strong frontend choice. Its current Free plan is framed as `Build and deploy free forever`, with a
`300 credit limit / month`, deploy previews, custom domains with SSL, functions, file hosting, and observability. It
is a valid alternative to Vercel for the SPA.

Strengths:

- very good frontend hosting and preview workflow
- free tier remains attractive for the web app
- useful built-in observability and deploy controls

Weaknesses:

- Netlify Functions are positioned around `JavaScript, TypeScript, or Go`, which is not a natural fit for the current
  ASP.NET API
- pushes the stack toward a frontend-only use of Netlify unless the backend is rewritten or hosted elsewhere

Best fit in this repo:

- frontend host only

### Railway

Railway is a strong fit for the ASP.NET API and can also host the frontend if desired. It directly documents support
for `ASP.NET Core`. As of April 1, 2026, the Free plan is described as `small apps with $1 of free credit per month`,
while Hobby is `$5 / month` and includes `$5 of resource usage per month`.

Strengths:

- natural app host for the current ASP.NET API
- simple environment variable and deployment workflow
- GitHub autodeploy support
- can host multiple services in one project
- easier day-2 operations than a VPS or Fly.io-first setup

Weaknesses:

- not truly free for a comfortable always-available staging setup
- post-paid card required for paid plans
- still another provider if the frontend lives on Vercel or Netlify

Best fit in this repo:

- API host
- possible single-platform app host if simplicity is valued over best-of-breed split hosting

### Render

Render can host both static sites and web services, and it still offers free services for previews and hobby use. Its
docs explicitly note the free-tier limitations: free web services spin down after `15 minutes` idle and take about
`one minute` to spin back up. Free Render Postgres databases expire after `30 days`.

Strengths:

- supports both web services and static sites
- free entry point exists for trying the platform
- in-dashboard logs, metrics, health checks, and operational controls are available

Weaknesses:

- free API cold starts are noticeable and frustrating for real staging use
- free Render Postgres expires after 30 days and does not fit a persistent staging environment
- free database limits make it a weak database choice for this epic

Best fit in this repo:

- cheap trial backend host
- not the preferred long-lived database host

### Neon

Neon is the strongest database candidate for cheap staging. As of April 5, 2026, the Free plan includes `100
CU-hours per project`, `0.5 GB` storage, and scale-to-zero `after 5 minutes when inactive`. It also has built-in
dashboarding and a credible path to light paid usage later.

Strengths:

- very attractive free plan for intermittent staging use
- managed PostgreSQL with low ops overhead
- scale-to-zero behavior makes low-traffic staging practical
- better fit than expiring free Postgres offers

Weaknesses:

- still a separate provider from the app host
- always-on or heavier staging usage can push the project toward paid usage

Best fit in this repo:

- default PostgreSQL choice for staging

### Azure

Azure is the most natural enterprise-style fit for the current .NET backend. Azure App Service is a fully managed PaaS
for web and API applications, and Azure Database for PostgreSQL is the matching managed database. Azure App Service
does expose a free plan, but Microsoft describes the free and shared plans as for `trials, experimentation, and
learning`, with `no SLA`, and the Free `F1` tier is limited to `60 CPU minutes / day`.

Strengths:

- excellent long-term fit for ASP.NET
- very standard managed app plus managed PostgreSQL shape
- strong long-term production story

Weaknesses:

- more expensive and heavier to reason about for a first MVP staging environment
- free tier is too limited to be a comfortable real staging target
- more platform surface area than needed right now

Best fit in this repo:

- a later-stage hosted path if the project chooses to lean into Azure and the .NET ecosystem

### Fly.io

Fly.io is flexible and powerful, but the operational feel is closer to infrastructure ownership than to the simplest
possible MVP staging path. Official pricing is usage-based, requires a credit card on file, and managed Postgres is not
positioned as a cheap default staging option.

Strengths:

- strong flexibility
- container-friendly
- capable of running both app and database variants

Weaknesses:

- more ops-shaped than the current need
- more complex cost and topology reasoning than Railway or Vercel plus Railway
- weaker fit for the "cheap, easy, low-drama staging" goal

Best fit in this repo:

- only if infrastructure control becomes more important than ease of use

### Self-managed VPS plus Docker

This variant keeps control high and recurring cost potentially low, but it pushes infrastructure burden onto the repo
owner immediately.

Strengths:

- maximum flexibility
- one machine can host everything
- can be cheap in raw dollar terms

Weaknesses:

- manual patching, secrets, backups, ingress, SSL, uptime, and database durability become your job
- works directly against the MVP goal of using managed services where practical
- not the best first staging step unless cost absolutely dominates every other concern

Best fit in this repo:

- not recommended for the first staging environment

## Stack Variants Compared

### Option A: Vercel + Railway + Neon

Frontend on Vercel, API on Railway, PostgreSQL on Neon.

Pros:

- strongest balance of low cost and usable staging UX
- each piece is hosted where it fits naturally
- Vercel is excellent for the SPA
- Railway is a straightforward host for ASP.NET Core
- Neon is the strongest cheap managed Postgres candidate in the current shortlist

Cons:

- three dashboards instead of one
- env var wiring spans multiple providers
- a little more setup coordination up front

Verdict:

- current recommendation

### Option B: Netlify + Railway + Neon

Frontend on Netlify, API on Railway, PostgreSQL on Neon.

Pros:

- similar to Option A in overall shape
- Netlify remains a strong frontend platform
- still keeps the backend and database on better-fitting services

Cons:

- slightly weaker fit than Vercel for the current React/Vite workflow
- no meaningful simplification compared with Option A

Verdict:

- valid alternative if Netlify is preferred personally

### Option C: Vercel + Render + Neon

Frontend on Vercel, API on Render, PostgreSQL on Neon.

Pros:

- can be very cheap to start
- still uses Neon for the database instead of Render's expiring free Postgres

Cons:

- Render free cold starts make staging less pleasant
- likely to become annoying once staging is used for repeated auth and browser testing

Verdict:

- acceptable cost-minimizing fallback, but not preferred

### Option D: Railway only

Frontend, API, and PostgreSQL all on Railway.

Pros:

- simplest operational story
- fewer providers, fewer dashboards
- easier to describe and document

Cons:

- usually not the cheapest route
- gives up Vercel or Netlify's stronger frontend hosting ergonomics
- makes the database choice less independently optimizable

Verdict:

- strong simplicity option if one-platform management matters more than lowest cost

Lifecycle note:

- this option may reduce staging-to-production switching pain because the app and database can stay on one platform
- it also reduces the chance that different environments end up with materially different deployment mechanics
- the tradeoff is giving up the stronger frontend-specific ergonomics of Vercel or Netlify

### Option E: Azure App Service + Azure Database for PostgreSQL

Frontend and API hosted on Azure App Service, PostgreSQL on Azure Database for PostgreSQL.

Pros:

- very coherent .NET ecosystem fit
- clear path from staging toward more formal production hosting
- less architectural drift later if Azure becomes the long-term home

Cons:

- heavier and more expensive than the MVP staging likely needs
- free tier is not a comfortable real staging solution

Verdict:

- better later than now

Lifecycle note:

- this is the strongest option in the shortlist if the project expects to standardize on a .NET-friendly managed stack
  later
- the downside is that it is likely overkill for the first staging environment and may slow the M1 path unnecessarily

### Option F: Fly.io

Use Fly.io for the app layer and possibly the database layer.

Pros:

- flexible and powerful
- container-friendly

Cons:

- more infrastructure-shaped than the current need
- less obvious "cheap and easy MVP staging" choice than Railway plus Neon

Verdict:

- not preferred for the first staging deployment

### Option G: VPS + Docker + managed or self-hosted Postgres

Pros:

- full control
- potential raw-cost savings

Cons:

- more ops burden immediately
- weak match for current MVP priorities

Verdict:

- not recommended for the first staging environment

## Recommended Direction

Choose:

- frontend: Vercel
- API: Railway
- database: Neon
- browser-facing staging model: prefer one browser origin with `/api` forwarding to the hosted API if the platform
  setup remains clean and reliable

This is the best current staging recommendation for Langoose unless one of these priorities changes materially:

- minimize dashboards at all costs
- avoid all paid usage, even if staging becomes noticeably worse to use
- maximize exact runtime and topology uniformity across local, staging, and production
- optimize for long-term Azure alignment instead of short-term MVP staging efficiency

## Why This Wins

This direction wins because it keeps staging cheap without making it annoying.

It avoids the biggest downsides of the alternatives:

- avoids forcing the ASP.NET API into a frontend-first platform
- avoids Render free-tier cold-start frustration as the default API path
- avoids Render free Postgres expiry
- avoids the heavier cost and platform surface of Azure too early
- avoids the operational burden of a VPS-first setup

It also fits how the repo is already shaped:

- React SPA can deploy independently
- ASP.NET API can deploy independently
- PostgreSQL remains managed
- GitHub-triggered deploy flows stay straightforward

The recommendation is intentionally optimized for the current roadmap stage:

- fast enough to stand up in M1
- usable enough for repeated M2 browser validation
- not so opinionated that it blocks a more production-shaped M3 or M4 move later

It also intentionally separates two ideas:

- Docker should remain an important packaging and local-validation boundary
- but the first staging environment does not need to reproduce the exact same full container topology as local Docker

## Uniformity Versus Managed Specialization

One of the central tradeoffs in this decision is:

- do we want stronger environment uniformity through a more container-first stack
- or do we want a more specialized managed staging stack that is cheaper and easier in M1

### What uniformity would mean

A stronger uniformity bias would usually imply:

- backend deploys from the same Docker image used locally
- a hosting platform that is container-first for the app runtime
- potentially one platform for web, API, and database or at least one for the app layer
- fewer differences between local Docker topology and hosted staging topology

This reduces some classes of drift, but usually at the cost of:

- giving up the strongest frontend-specific hosting ergonomics
- more infrastructure-like platform choices
- more immediate setup overhead

### What the current recommendation means

The current recommendation keeps Docker important, but not in the strongest possible sense.

It assumes:

- local whole-app verification remains Docker-first through `compose.yml`
- the API should stay portable and container-friendly
- the browser-facing contract should stay stable across environments
- managed staging may still use platform-native routing or rewrites rather than reproducing the exact local proxy stack

That is a compromise:

- not maximum topology uniformity
- but still portable enough to avoid painting the app into a provider-specific corner

### Why this trade is acceptable now

For Langoose at the current roadmap stage, the biggest risk is not lack of Kubernetes-readiness. The biggest risk is
spending too much effort on infrastructure shape before the MVP staging environment even exists.

So the current recommendation favors:

- good enough portability
- better M1 and M2 speed
- explicit tracking of the integration-risk areas, especially cookies, CSRF, CORS, and routing

## Recommended Staging Browser Integration Model

The current recommendation is not only about providers. It also recommends a browser integration model.

Preferred model:

- browser entrypoint on one staging web origin
- frontend calls relative `/api/...`
- that path forwards or rewrites to the hosted API service
- the hosted API still has its own direct URL for health checks, debugging, and platform validation

### Why this is preferred

The current repo already uses this shape locally:

- the web container serves the SPA
- Nginx proxies `/api/` to the API container

That means a one-origin browser model keeps staging closer to the local user-facing behavior than a split-origin model.

This is especially valuable because the current auth flow depends on:

- cookies
- CSRF token fetches
- credentialed browser requests

A one-origin browser model reduces the chance of staging-only failures caused by:

- credentialed CORS
- subtle `SameSite` behavior
- CSRF flow differences
- browser preflight and origin mismatch issues

### Important nuance

This does **not** mean the API loses its own host.

The expected shape is:

- browser-facing access through the staging site origin, such as `/api/...`
- direct API host still exists separately for health checks, manual validation, and debugging

### Fallback if one-origin forwarding is awkward

If clean one-origin forwarding or rewrites turn out to be awkward on the chosen platforms, the fallback is:

- split web and API origins
- with an explicit hardening task for cookie, CSRF, CORS, and browser credential behavior

That fallback is acceptable, but it is not the preferred first choice.

## Concrete Outcome For Issue 36

The current recommended outcome for `#36` is:

- browser-facing staging uses one web origin
- the SPA continues to call relative `/api/...`
- Vercel handles `/api/:path*` as an external rewrite to the Railway API host
- the Railway API keeps its own direct host for health checks, debugging, and non-browser validation
- the API's current permissive CORS behavior is treated as a development placeholder and should not remain the staging
  policy

### Why this outcome fits the current codebase

The current repo already behaves this way locally:

- the browser talks to one origin
- the web container proxies `/api/` to the API container

The auth flow also currently depends on:

- cookie auth
- antiforgery token fetches
- credentialed browser requests

That makes one-origin browser behavior the lowest-risk hosted staging model.

### Practical staging request flow

Expected browser-facing flow:

```text
Browser
  -> https://staging-web.example.com/
  -> https://staging-web.example.com/api/auth/me
       Vercel external rewrite
       -> https://staging-api-host/... on Railway
```

Expected non-browser direct API flow:

```text
Operator or health check
  -> https://staging-api-host/health
```

This preserves both:

- one-origin browser behavior for the app
- a separate direct API host for diagnostics and operational checks

### Cookie and CSRF expectations

For this model:

- the auth cookie should remain host-scoped and `Secure` in staging
- the antiforgery cookie should remain host-scoped and `Secure` in staging
- `SameSite=Lax` remains reasonable for the preferred one-origin browser model
- the SPA should continue to fetch `/auth/antiforgery` through the proxied `/api` path before unsafe authenticated
  requests

The main goal is to keep the browser-facing semantics close to the local Docker path rather than introducing split-origin
rules too early.

### CORS expectations

The current API code uses an effectively permissive development placeholder:

- credentials allowed
- origin accepted broadly

That should not be treated as the staging design.

For the preferred one-origin browser model:

- normal browser traffic should not need cross-origin CORS for the app itself
- the API should move toward an explicit staging-safe policy rather than a broad permissive one
- any remaining direct-origin access requirements should be deliberate and narrowly configured

### Forwarding and proxy expectations

Because the browser-facing route depends on proxying or rewrites:

- the API should be ready for trusted forwarded headers in hosted environments
- runtime configuration should not assume local direct-host behavior
- the browser contract should stay as relative `/api`

This keeps the browser contract stable even if the underlying hosting implementation changes later.

### What remains deferred after issue 36

Issue `#36` should settle the design choice clearly enough that later issues can implement it, but it does not need to
complete the whole staging rollout.

These implementation steps remain intentionally deferred:

- concrete Vercel rewrite configuration
- API forwarded-header and CORS code changes
- Railway deployment wiring
- final hosted smoke validation

## Implementation Checklist For Issue 36

The purpose of this section is to make the decision implementable without re-discovering the same risks later.

### API changes to prepare

The API should move from a development-placeholder browser integration model to an explicit staging-safe one.

Planned changes:

- replace the current permissive CORS setup with an explicit policy model
- make it possible to disable or narrowly scope cross-origin credentialed access in staging
- add trusted forwarded-header handling for the hosted proxy path
- keep cookie security explicit for hosted HTTPS environments
- keep auth and antiforgery cookie behavior aligned with the chosen one-origin browser model

Practical notes:

- the current `AllowCredentials()` plus broadly accepted origins should not survive as the staging design
- if browser traffic stays one-origin through `/api`, the app should not depend on broad browser CORS at all
- direct API access for health checks and operator validation should not drive the browser policy design

### Frontend and web-host changes to prepare

The frontend should keep a stable browser-facing contract.

Planned changes:

- keep the browser-facing API base path as relative `/api`
- avoid baking the Railway host into the normal browser runtime path
- add Vercel rewrite configuration for `/api/:path*` to the hosted API
- keep local Docker behavior aligned conceptually with staging by preserving the same relative `/api` browser contract

Practical notes:

- the current local Nginx config is the reference shape for the browser contract, not necessarily the deployed staging
  proxy implementation
- staging should preserve the contract even if the underlying proxy technology changes from Nginx to platform rewrites

### Runtime configuration to define

The staging model should define these values explicitly:

- the browser-facing staging URL
- the direct hosted API URL
- the exact forwarded-host and forwarded-proto expectations
- whether any non-browser direct-origin requests require separate allowance
- the environment-variable names and ownership for web and API configuration

For the API, the current repo direction is to keep cross-origin browser access and forwarded-header trust as ordinary
host configuration:

- `Cors` for allowed browser origins
- `ForwardedHeaders` for trusted proxy/header processing

Current configuration contract:

- `Cors:AllowedOrigins`
- `ForwardedHeaders:Enabled`
- `ForwardedHeaders:TrustAllProxies`
- `ForwardedHeaders:KnownProxies`
- `ForwardedHeaders:KnownNetworks`

Practical intent:

- local defaults can stay safe and minimal
- staging can inject its real browser origin allowlist and forwarding trust settings through environment variables
- later deployment issues can wire real values without changing the API policy shape again
- direct Railway-hosted staging can use `ForwardedHeaders:TrustAllProxies=true` so forwarded HTTPS is honored for secure
  antiforgery and auth-cookie behavior

### Validation expectations after the decision

When later issues implement this design, they should prove at least:

- browser requests succeed through the staging site origin using `/api`
- auth bootstrap works through the proxied path
- sign-in and sign-out work in the browser-facing staged app
- authenticated write requests succeed with the expected CSRF flow
- direct API health checks still work through the API host

### What issue 36 should deliver

At the end of `#36`, the repo and issue should make these points unambiguous:

- one-origin browser staging is the preferred path
- the frontend should keep relative `/api`
- Vercel rewrites are the intended browser-facing forwarding mechanism if feasible
- the current API CORS setup is temporary and should be replaced by an explicit staging-safe policy
- later implementation issues know exactly what they are expected to wire and validate

## Broader Roadmap Fit

### Fit for M1 Foundation

Strong.

This stack gets staging online quickly without requiring deep infrastructure work. That matches the immediate goal of
epic `#3`.

### Fit for M2 Core Learning

Strong.

The stack should comfortably support the likely MVP user-facing checks:

- sign-in and sign-out
- protected writes
- dictionary changes
- study flow checks
- browser-based validation through a real hosted frontend and API

### Fit for M3 Deployable Beta

Medium to strong.

The main concern is not raw capability but operational fragmentation:

- frontend, API, and database are managed separately
- logs and metrics live in multiple dashboards
- debugging cross-service incidents is slightly more manual

This is acceptable in M3 if the team is still small and the app is low traffic, but it is less elegant than a more
unified long-term platform.

### Fit for M4 Launch

Medium.

It can still work, but by M4 the project may reasonably prefer one of these adjustments:

- keep the frontend on Vercel and move the API and database to a more production-shaped platform
- keep the API on Railway and move only the database or observability layer
- move both API and database into a more consolidated hosting environment

This is why the current recommendation should be treated as a staging-first choice, not a permanent final hosting
commitment.

## Environment Drift And Switching Cost

### Different hosting by environment

Running local Docker, staging on managed services, and production later on a somewhat different managed stack is
acceptable, but only if the repo keeps deployment assumptions narrow:

- app config should remain environment-variable driven
- the Docker image should stay the packaging boundary for the API where practical
- the frontend should not assume one provider-specific runtime model
- deployment docs should clearly separate generic app requirements from provider-specific setup

If those boundaries stay clean, the switching cost later is manageable.

This also means the repo should prefer a stable browser contract across environments where practical:

- browser uses relative `/api`
- local Docker can satisfy that via Nginx
- hosted staging can satisfy that via platform rewrites or proxying
- later production can keep the same browser contract even if the underlying implementation changes

### Different providers for frontend and backend

This is not a problem by itself. It becomes a problem only when:

- authentication or cookie policy becomes provider-sensitive
- CORS and origin configuration drift between environments
- environment variables are handled differently enough to cause contract mismatches

For Langoose, this is a manageable risk, not a reason to reject split hosting outright.

### Switching cost from Vercel plus Railway plus Neon later

Frontend:

- low switching cost
- the SPA is relatively portable

API:

- medium switching cost
- the main work is deployment pipeline and environment wiring, not code rewrite

Database:

- medium to high switching cost
- database moves are always more sensitive than app-host moves
- this argues for choosing the best cheap managed Postgres candidate early instead of taking an expiring or weak free
  tier

### Switching cost from Railway only later

- lower platform fragmentation
- simpler environment story
- potentially lower operational drift between staging and production
- but weaker frontend specialization and possibly higher cost earlier

### Switching cost from Azure later

- low switching cost if Azure becomes the long-term destination
- higher immediate implementation and ops cost now

### Practical conclusion

The current recommendation accepts a small amount of future switching cost in exchange for a better M1 and M2
experience. That is a reasonable trade for an MVP unless the project already knows it wants a single long-term platform.

## Operational Database Work

The staging choice should not be judged only by deploy convenience. Day-2 database operations matter too.

For Langoose, the likely staging database operations include:

- wiping staging user data safely
- resetting the database to a known-good state
- reseeding base content correctly
- inspecting and fixing bad rows after test or migration mistakes
- validating both app and auth database changes
- running migrations repeatedly during M1 and M2 work
- recovering after a broken deploy or a bad manual test

That means the database host should be judged partly by how easy it is to:

- connect with normal PostgreSQL tools
- run ad hoc SQL safely
- inspect records without bespoke infrastructure
- restore or recreate a clean environment
- document predictable operational procedures

### Neon for operational staging work

Neon is strong here.

Why:

- normal PostgreSQL access remains available
- the platform is built around managed Postgres rather than bundling the database into a generic app host
- branching and restore-oriented features make staging reset and investigation workflows more attractive than on weaker
  free database offerings
- it is a better fit for "we need to repair, inspect, reset, and continue" than a disposable free Postgres tier

Operationally, Neon makes these workflows more realistic:

- keep a known-good baseline
- test a migration or risky data change
- reset staging without rebuilding all hosting from scratch
- inspect and correct data problems during MVP iteration

### Railway Postgres for operational staging work

Railway-hosted Postgres is workable, but less compelling than Neon for reset- and restore-oriented staging workflows.

It is still acceptable if the priority is platform consolidation, but it is not the strongest choice if staging data
operations are expected to happen regularly.

### Render free Postgres for operational staging work

This is a poor fit.

The 30-day expiry, weaker guarantees, and lack of backup support make it a bad default for a staging environment where
database reset and recovery should be routine and reliable.

### Azure Database for PostgreSQL for operational staging work

Azure is capable, but heavier.

It is better suited to a later, more production-shaped operating model than to the fastest and least-drama MVP staging
workflow.

### Practical conclusion

If we expect staging to be actively used for auth work, CSV import checks, dictionary fixes, migration validation, and
general MVP iteration, database operability is a real decision factor.

That strengthens the case for:

- using managed PostgreSQL
- preferring Neon over weaker free-tier Postgres options
- documenting reset and reseed procedures early in the staging rollout

## Production Evolution And Orchestration

Moving to Kubernetes or another container orchestrator later is possible, but it is not inevitable.

For a project like Langoose, the more likely progression is:

1. managed staging and early production on hosted platforms
2. modest scaling and more operational visibility on the same or adjacent managed platforms
3. only later, if real complexity appears, a move toward a more container-orchestrated or infrastructure-heavy model

The current decision should therefore optimize for:

- not blocking a later move
- but not prematurely optimizing for Kubernetes either

### What will matter if the app grows

The parts that should stay portable now are:

- API runtime configuration
- browser contract
- Docker packaging for local and backend portability
- database access patterns that do not assume one provider's quirks

If those remain clean, a later move to:

- a more consolidated managed platform
- a container-first platform
- or an orchestrated environment

should be a migration project, not a rewrite.

### What should not be overfit now

The repo should not overfit the first staging decision to:

- Kubernetes assumptions
- complex multi-service orchestration
- production-scale infrastructure needs that the MVP does not yet have

The goal is to keep the app portable enough to move later, not to build the final platform shape prematurely.

## Variants Not Chosen

### Why not Netlify first?

Netlify is credible for the frontend, but it does not create a meaningfully better overall stack for this repo than
Vercel plus Railway plus Neon. It is a legitimate alternative, not a bad option.

### Why not Render first?

Render is more attractive on paper than in repeated staging use. A staging environment that goes idle, then takes about
one minute to wake up, is workable but not pleasant. The free Postgres expiry is a harder blocker.

### Why not Azure first?

Azure is a serious candidate for a later production-aligned direction, but it is too heavy for the first staging
deployment if the primary goal is to get a usable MVP environment online quickly and cheaply.

### Why not choose one platform for every environment immediately?

That can reduce future switching cost, but it also risks optimizing for a later operational shape before the MVP has
proven what it actually needs. For Langoose right now, that trade does not look favorable enough to justify giving up a
better M1 staging experience.

### Why not require the exact same container topology everywhere?

Because exact topology parity is only one form of uniformity. For Langoose right now, keeping these things stable
matters more:

- a portable backend
- a stable browser-facing `/api` contract
- Docker-based local validation
- explicit handling of the browser integration risks

That is enough uniformity for MVP staging without forcing every environment to run the exact same web proxy stack.

### Why not self-host everything?

Because the first staging environment should reduce uncertainty, not create a second project in infrastructure
operations.

## Follow-up Work After The Decision

If this recommendation is accepted, the next staging child issues should likely be:

1. define the staging origin, auth-cookie, and routing model
2. provision Neon PostgreSQL and wire app and auth databases
3. deploy the API to Railway with staging-safe runtime configuration
4. deploy the web app to Vercel against the chosen origin model
5. add GitHub-driven staging deployment flow
6. document staging setup, database operations, and smoke-test runbook

## Execution Sequence And Likely Risks

The staging rollout should follow the real dependency order, not the cleanest abstract issue split.

### Step 1: define the staging origin, auth-cookie, and routing model

This is the first implementation issue after the hosting decision.

Why it must come first:

- the current local stack hides complexity behind an Nginx same-origin `/api` proxy
- the API currently uses cookie auth plus antiforgery
- the API's current CORS policy is a development placeholder and is not an acceptable hosted policy
- the frontend and backend hosting choice changes whether cookies, CSRF, and browser requests stay same-origin,
  same-site, or fully cross-origin

Questions this step must settle:

- should staging use one origin with a reverse proxy path such as `/api`
- or two origins such as `staging-web` and `staging-api`
- what exact cookie and CORS policy will be valid for that model
- what domains or subdomains should be used in staging

Likely issues:

- browser rejection of credentialed requests if CORS remains permissive
- cookie behavior differences between same-origin and split-origin staging
- CSRF token flow breaking if the routing model is not explicit
- mismatch between local Docker behavior and hosted staging behavior

### Step 2: provision Neon PostgreSQL and wire app and auth databases

This is the next foundation step because the API cannot work without real database connectivity.

Why:

- the API requires both `AppDatabase` and `AuthDatabase`
- startup currently runs migrations for both databases and seeds app data
- staging needs a repeatable database naming and secret model before the hosted API is configured

Likely issues:

- deciding whether to use one Neon project with two databases or another topology
- making sure both databases are reachable from the API host
- deciding how staging reset, wipe, and reseed operations should work
- avoiding accidental coupling to local Docker assumptions

Current concrete recommendation for issue `#37`:

- keep the existing Neon project and create a long-lived `staging` branch
- two databases inside that staging branch: `langoose_app` and `langoose_auth`
- staging should follow the same explicit migration/seed process as production instead of adding a staging-only runtime toggle
- prefer full database recreation or Neon branch recreation over ad hoc partial wipes when staging becomes unreliable

### Step 3: deploy the API to Railway with staging-safe runtime configuration

This step depends on the database and the chosen origin model.

Why:

- the API needs real app and auth connection strings
- it also needs staging-safe cookie security, CORS, forwarded headers, and environment config
- the release flow needs a separate migration step before deploy when schema changes are present

Likely issues:

- cookie security or proxy header handling behind the host
- runtime URL and HTTPS assumptions
- over-permissive development CORS leaking into staging
- migration sequencing failures before first deploy
- auth endpoints working locally but failing in hosted browser flows

### Step 4: deploy the web app to Vercel against the chosen origin model

This step depends on the API deployment and the cross-origin decision.

Why:

- the SPA needs the real hosted API path or reverse-proxy path
- auth and CSRF behavior need to work from the actual browser origin

Likely issues:

- wrong API base URL
- Vercel rewrites or proxy behavior if the same-origin route is chosen
- browser credential behavior if split origins are chosen
- environment drift between local Docker and hosted staging

### Step 5: add GitHub-driven staging deployment flow

This should follow the first successful manual end-to-end deployment.

Why:

- the deployment automation should encode a working setup, not guess at one
- secrets, domains, and deployment targets need to be known first

Likely issues:

- secrets split across providers
- deployment order between API, web, and database changes
- making sure GitHub-driven updates do not bypass required migrations or runtime config

### Step 6: document staging setup, database operations, and smoke-test runbook

This can start earlier but should finalize after the first successful end-to-end path.

Why:

- the runbook must reflect the actual chosen routing, database, and deployment model

Likely issues:

- documenting the happy path but forgetting reset/recovery operations
- leaving cross-origin and cookie assumptions implicit
- failing to document how to restore staging to a known-good state

### Practical issue map

A good issue decomposition for the implementation phase is:

1. define staging origin, auth-cookie, CORS, and routing strategy
2. provision Neon PostgreSQL and define staging DB operations
3. deploy API to Railway with staging runtime configuration
4. deploy web to Vercel with the chosen staging routing model
5. automate GitHub-driven staging deploys
6. document staging setup, smoke tests, and database operations

## Sources

Checked against official provider pages on April 5, 2026:

- [Vercel pricing](https://vercel.com/pricing)
- [Netlify pricing](https://www.netlify.com/pricing/)
- [Netlify Functions](https://www.netlify.com/platform/core/functions/)
- [Railway pricing plans](https://docs.railway.com/pricing/plans)
- [Render free tier docs](https://render.com/docs/free)
- [Neon pricing](https://neon.com/pricing)
- [Azure App Service pricing](https://azure.microsoft.com/en-us/pricing/details/app-service/windows/)
- [Azure Database for PostgreSQL pricing](https://azure.microsoft.com/pricing/details/postgresql/flexible-server/)
- [Fly.io pricing](https://fly.io/docs/about/pricing/)
