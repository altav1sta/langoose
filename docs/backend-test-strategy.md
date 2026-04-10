# Backend Test Strategy

## Why The Backend Uses Two Test Projects

Langoose separates backend tests into:

- `apps/api/tests/Langoose.Api.UnitTests`
- `apps/api/tests/Langoose.Api.IntegrationTests`

This split is intentional.

Unit tests are for isolated logic that can run without meaningful infrastructure setup. They should stay fast,
focused, and easy to debug.

Integration tests are for real backend behavior that crosses component boundaries. They can use EF Core, seeding,
ASP.NET Core hosts, authentication, antiforgery, and other runtime pieces together.

## What Belongs In Unit Tests

Use unit tests when the behavior can be proved without the HTTP pipeline, seeding, or cross-component persistence.

Examples:

- text normalization
- answer grading helpers
- enrichment warnings
- other pure or nearly pure service logic

If a test mostly exercises one method with in-memory objects and no meaningful app runtime, it is probably a unit test.

## EF Core InMemory Usually Is Not A Unit-Test Boundary

In this repo, tests that use a real `DbContext`, even with `UseInMemoryDatabase`, usually count as integration tests
rather than unit tests.

That is because they still rely on:

- EF Core change tracking
- persistence-shaped state
- seeded data
- query behavior
- cross-component application wiring

`UseInMemoryDatabase` is a lower-fidelity integration tool, not a pure mock boundary.

If a test depends on EF behavior, seeded state, tracking, or persistence semantics, it belongs in integration.

If a test only constructs a lightweight `DbContext` incidentally while still proving isolated logic, it can stay in
unit tests, but that exception should be rare and obvious from the test body.

## What Belongs In Integration Tests

Use integration tests when the behavior depends on more than one backend layer working together.

Examples:

- service behavior backed by seeded EF Core state
- dictionary import and merge behavior that depends on persistence state
- study flow behavior that depends on review-state records
- auth contract behavior through the ASP.NET Core host
- authorization and antiforgery behavior through the request pipeline

If a test needs real app wiring, seeded data, multiple contexts, or a host/client boundary, it belongs in integration
tests.

## Why Integration Tests Still Use Internal Folders

The integration project is still split internally into folders such as:

- `Api`
- `Services`
- `Infrastructure`

This does not mean those tests are different test types. They are all integration tests.

The folder split exists because not every integration test should pay the same cost or prove the same boundary.

`Api` tests prove:

- routing
- middleware
- auth
- antiforgery
- serialization
- controller wiring

`Services` tests prove:

- backend business behavior
- EF-backed state changes
- seeded data interactions

without adding HTTP-specific noise when the HTTP pipeline is not the thing at risk.

`Infrastructure` contains shared test hosts, sessions, and helpers used by the integration suite.

## Why There Are Two Integration Test Hosts

The integration suite currently uses two different hosts:

- `ApiTestHost`
- `AuthApiTestHost`

That split is intentional.

`ApiTestHost` is for protected application flows such as dictionary, study, and content behavior. It uses a lightweight
test authentication scheme that injects a known authenticated user through headers. That keeps protected-endpoint tests
fast and focused on application behavior rather than on sign-in mechanics.

`AuthApiTestHost` is for auth contract tests. It uses the real Identity cookie flow, antiforgery setup, and
sign-in/sign-out behavior so the auth endpoints are tested through the same mechanism the application actually uses.

The point of the split is to avoid forcing every protected-endpoint integration test through full sign-up/sign-in
session setup when the thing at risk is not auth itself.

These hosts could be unified later behind one configurable host, but keeping them separate is reasonable as long as:

- each host has a clear purpose
- shared wiring stays minimal
- duplication does not start to drift

## Why Not Convert Everything Into Whole-Chain API Tests

Making every integration test a full HTTP-chain test would increase:

- runtime cost
- setup cost
- brittleness
- debugging difficulty

Service-level integration tests are still valuable because they prove real backend behavior with persistence while
keeping failures easier to localize.

The intended layering is:

- unit tests for isolated logic
- integration `Services` tests for backend behavior with real persistence and seeding
- integration `Api` tests for public contract, security, and full request-pipeline behavior
- repo-level e2e tests for whole-system browser proof

This gives overlap where it helps, but avoids duplicating the same scenario at every layer without a reason.

## Persistence Tests

Persistence behavior is already covered indirectly by many service and API integration tests because those tests:

- write state through real services or hosts
- open new scopes or contexts
- verify the resulting persisted behavior

Standalone persistence-only tests should only exist when they prove something uniquely persistence-specific, such as:

- relationship mapping behavior
- delete behavior
- query semantics
- a bug that is specifically about EF configuration

If a persistence-only test is just re-proving what service or API integration tests already cover, it should usually be
removed.

## Practical Rule Of Thumb

When adding a backend test, first ask:

1. Can this be proved as isolated logic?
2. If not, do I need the HTTP pipeline, or only real backend state and services?
3. If I need the browser or whole stack, should this move to repo-level e2e instead?

Choose the shallowest layer that still proves the real risk.

## When To Escalate To Testcontainers

The current default is:

- unit tests for isolated logic
- integration tests with `TestServer` and EF Core InMemory
- repo-level e2e for browser/system proof

Switch or add coverage with real PostgreSQL via Testcontainers when implementation changes start depending on behavior
that EF Core InMemory does not model faithfully enough.

Treat the following as concrete triggers:

- a bug reproduces only against real PostgreSQL
- migrations need to be verified as part of the change
- the change depends on database constraints, indexes, defaults, or delete behavior
- transaction behavior matters
- collation, ordering, or case-sensitivity semantics matter
- query translation or provider-specific SQL matters
- the code starts using PostgreSQL-specific EF/provider features
- confidence in the change depends on how the app behaves with a real PostgreSQL server, not just a persistence-shaped fake

If one of those triggers appears, do not rely only on EF InMemory integration tests. Add or expand a Testcontainers-backed
integration path and call that out explicitly in validation.
