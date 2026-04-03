# Langoose .NET Test Layout

## Official Guidance Applied Here

- Microsoft Learn's .NET CLI testing tutorial recommends placing the application project and test project in separate folders and demonstrates a parallel `src` and `test` structure.
- Microsoft Learn's ASP.NET Core testing guidance says a common approach is to place application projects under `src` and test projects under a parallel `tests` folder.
- Microsoft Learn's unit-testing guidance recommends keeping unit tests separate from integration tests so the unit-test project avoids infrastructure dependencies.

## What That Means For Langoose

- This repo already uses `apps/` rather than `src/`, so mirror the same idea with a root-level `tests/` folder.
- Prefer this repository shape:
  - `apps/api/src/Langoose.Api/Langoose.Api.csproj`
  - `apps/api/tests/Langoose.Api.Tests/Langoose.Api.Tests.csproj`
- If the suite grows enough to justify separate runs or dependencies, split by test type:
  - `tests/Langoose.Api.UnitTests`
  - `tests/Langoose.Api.IntegrationTests`
  - `tests/Langoose.Api.FunctionalTests`

## How To Classify Existing Langoose Tests

- The current `apps/api/tests/Langoose.Api.Tests` suite exercises multiple services together against the current backend stack and in-memory test doubles.
- That makes it closer to an integration or behavioral xUnit suite than a pure unit-test suite.
- Keep that distinction in mind when naming or relocating the project.

## Practical Guidance For Future Refactors

- When adding pure unit tests, target methods or classes that can run entirely in memory without touching EF Core or external services.
- When adding functional API tests, consider `WebApplicationFactory<Program>` and make `Program` public partial if the test project needs access to it.
- When a single test project becomes too broad, split by test type first, then by application area inside each project.
- Keep application references one-way: test projects reference the app project; app projects do not know about test projects.`n- Prefer discoverable xUnit test projects over executable harnesses so `dotnet test` and Visual Studio Test Explorer can both discover the suite.
- Respect `.gitattributes` so moving or splitting test files does not introduce line-ending-only diffs.
- Prefer the repository line-length standard of 120 characters in test code too, except where test readability is better with a small local exception.
- In C# projects, prefer one top-level type per file for both production code and tests. If a test helper, stub, or fixture grows beyond a tiny private helper, move it into its own file.
- Prefer primary constructors in test fixtures and services when they reduce boilerplate without hiding setup intent.
- Prefer records for simple test data objects and immutable request/response-style models when value semantics are more useful than mutable identity.
