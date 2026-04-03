# MVP Authentication Decision

This note explains the auth direction for the deployable MVP.

Use this file for:

- the product-level decision
- option comparison
- roadmap sequencing
- backend technology choice

Use [`auth-m1-implementation-blueprint.md`](auth-m1-implementation-blueprint.md) for the concrete M1
implementation plan.

## Table Of Contents

- [Snapshot](#snapshot)
- [Decision Map](#decision-map)
- [Options Compared](#options-compared)
- [Chosen Direction](#chosen-direction)
- [Why This Wins](#why-this-wins)
- [Recommended Backend Approach](#recommended-backend-approach)
- [MVP Scope](#mvp-scope)
- [Roadmap](#roadmap)
- [What Option 2 Would Really Require](#what-option-2-would-really-require)
- [Effect Of AI-Assisted Implementation](#effect-of-ai-assisted-implementation)
- [Relationship To OAuth](#relationship-to-oauth)

## Snapshot

| Topic | Decision |
| --- | --- |
| Initial login method | First-party email/password |
| Web auth style | Backend-managed secure web session |
| Long-term backend direction | OAuth/OIDC-capable foundation |
| Backend stack | ASP.NET Core Identity + OpenIddict |
| External providers in MVP | No |
| Native/mobile in MVP | No |
| Native/mobile compatibility later | Yes |
| Auth persistence boundary | Separate auth database on the same PostgreSQL server |

## Decision Map

```text
Now:
  Web-first product
  -> first-party email/password
  -> secure web session for browser
  -> Identity + OpenIddict under the hood

Later:
  Add Google or another provider
  -> same auth foundation

Later if needed:
  Add native/mobile token flows
  -> same auth foundation
```

## Options Compared

| Option | What it means | Complexity | MVP fit | Verdict |
| --- | --- | --- | --- | --- |
| Option 1 | First-party email/password on an OAuth/OIDC-capable backend | Medium-high | Strong | Chosen |
| Option 2 | External provider login through OAuth 2.0 Authorization Code + PKCE | Medium-high to high | Medium | Tempting, but too much operational and account-linking risk for MVP |
| Option 3 | Support both first-party and provider login from the start | High | Weak | Too much surface area for first deployable version |

### Option 1

Users create accounts directly in Langoose. The backend owns credential verification, local user identity, and the
browser session, but it is built on a foundation that can later support token-based clients.

| Pros | Cons |
| --- | --- |
| Simplest path that still avoids a web-only dead end | Password handling becomes an app responsibility |
| Full control over the user model | Requires sign-up, sign-in, sign-out, and password lifecycle work |
| Good fit for web-first delivery | More work than a throwaway browser-only flow |
| Clean path to providers and native/mobile later |  |

### Option 2

Users sign in with a provider such as Google. Langoose still needs a local user model and a local session after the
provider finishes the external login flow.

| Pros | Cons |
| --- | --- |
| Avoids local password storage | Higher setup and deployment complexity |
| Familiar user experience when a supported provider exists | Requires provider registration, callback handling, and environment-specific setup |
| Good long-term fit if social login becomes important | Still needs local user creation, linking rules, and session handling |

### Option 3

Users can sign in either with local credentials or with a provider. That sounds flexible, but it forces account
linking, recovery, and cross-flow UX decisions into the MVP.

| Pros | Cons |
| --- | --- |
| Most flexible user experience | Highest implementation and support burden |
| Leaves no login method gap | Requires account linking and conflict handling immediately |
|  | Expands backend, frontend, testing, and support surface too early |

## Chosen Direction

Choose Option 1 for the deployable MVP:

- first-party email/password
- backend-managed secure web session for the browser
- ASP.NET Core Identity + OpenIddict as the backend auth foundation
- no external identity providers in the initial deployable MVP

## Why This Wins

- It is the smallest path that gives the product real user accounts without painting the backend into a browser-only corner.
- It lets the team make the web product solid first instead of splitting attention across provider setup, callback flows,
  and account linking.
- It keeps the auth stack in the existing ASP.NET Core ecosystem.
- It leaves a clean path to add Google or another provider later on the same foundation.
- It leaves a clean path to add native/mobile clients later without replacing the auth core.

## Recommended Backend Approach

### Recommended stack

Use:

- ASP.NET Core Identity for local users and password management
- OpenIddict as the OAuth/OIDC-capable foundation
- secure web cookies for the current browser client
- standards-based token flows later for native/mobile clients

### Why this is the best fit

| Choice | Why it fits Langoose |
| --- | --- |
| ASP.NET Core Identity | Handles local accounts, password hashing, and web session behavior cleanly |
| OpenIddict | Best open-source fit if the project wants a real OAuth/OIDC-capable backend in .NET |
| Separate auth database | Keeps auth isolated from app-domain data without introducing a separate identity platform |

### Why not the main alternatives

| Alternative | Why it is not the default recommendation |
| --- | --- |
| Cookie-only long-term auth design | Too web-specific if native/mobile or provider login arrives later |
| Duende IdentityServer | Technically strong, but weaker fit for an open-source project because production use requires a license |
| Keycloak | Strong identity platform, but too much operational and architectural weight for this repo unless the project explicitly wants a separate identity system |

## MVP Scope

### In scope now

- sign-up with email and password
- sign-in with email and password
- sign-out
- password hashing and verification
- authenticated API access
- user-scoped dictionary and study data
- an auth foundation that can evolve later without a backend rewrite

### Explicitly later

- Google or other social sign-in
- provider-login flows through OAuth 2.0 Authorization Code + PKCE
- native/mobile client delivery
- email verification
- password reset and recovery
- multi-factor authentication
- role-based authorization
- account linking across multiple login methods
- advanced device/session management

## Roadmap

| Phase | Auth focus |
| --- | --- |
| M1 Foundation | Local accounts, secure web session, Identity + OpenIddict foundation, separate auth persistence |
| M2 Core Learning | Make web auth and user flows solid in staging and real usage |
| M3 Deployable Beta | Add one external provider if it is still valuable after the web product is stable |
| Later if needed | Add native/mobile clients and the token flows they need |

Practical takeaway:

- web-first delivery does not justify a cookie-only long-term auth boundary
- native/mobile compatibility matters at the architecture level
- native/mobile delivery does not need to become an immediate milestone

## What Option 2 Would Really Require

Option 2 is still a valid future step, but it is not "just add a Google button."

### Main work areas

| Area | What it really means | Main risk |
| --- | --- | --- |
| Provider registration | Separate or carefully managed local, staging, and production redirect setup | Environment mismatch |
| OAuth flow security | `state`, PKCE, callback validation, token exchange | Security correctness |
| Local user handling | Create or load a local user after provider login | Identity model complexity |
| Account linking | Decide what happens when the same email already exists locally | Account takeover or duplicate-account mistakes |
| Session handling | Create a local Langoose session after provider login | Confusing provider vs app auth boundaries |
| Frontend callback UX | Redirects, failure states, and auth recovery | Poor user experience |
| Testing and validation | Real local and staging callback checks | Deployment surprises |

### Minimum practical future shape

If this becomes the next auth expansion, the narrowest sensible version is:

- one provider first, most likely Google
- verified email required
- local user creation on first successful login
- local server-managed session after callback
- no simultaneous rollout of both provider login and first-party login changes unless clearly needed

## Effect Of AI-Assisted Implementation

AI changes the effort profile, but it does not remove the hardest auth decisions.

### What AI helps with

- scaffolding backend auth endpoints and service code
- wiring frontend auth screens and callback handling
- generating tests and validation checklists
- producing environment and config templates

### What still remains hard

- choosing safe account-linking rules
- validating callback, cookie, and redirect behavior in real environments
- handling provider-specific edge cases
- operating and supporting a real auth system after launch

### Practical conclusion

- AI makes Option 2 more feasible
- AI does not make Option 2 lower-risk than Option 1
- AI does not make Option 3 a good first-MVP choice

## Relationship To OAuth

OAuth 2.0 Authorization Code + PKCE remains a valid future path for social sign-in and native/mobile support.

The key point is sequencing:

- do not force provider-login complexity into the first deployable MVP
- do not build a browser-only auth foundation that must be thrown away later
- do build the MVP on a backend that can grow into provider login and token-based clients when the product actually
  needs them
