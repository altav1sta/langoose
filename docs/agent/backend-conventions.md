# Backend Conventions

## C# Style

- Prefer one top-level type per file.
- Prefer primary constructors when they simplify the type.
- Prefer `record` or `record struct` for DTOs and other value-like data carriers when identity semantics are unnecessary.
- Keep namespaces aligned with the project and folder structure.
- Keep public and protected members before private helpers.
- Prefer names that are clear in local scope without adding unnecessary noise.
- Avoid redundant materialization, qualification, imports, and DI registrations.
- Replace non-obvious magic numbers with named constants or configuration.

## Related Canonical Docs

- For EF structure, migrations, and seeding, read [efcore-structure.md](efcore-structure.md).
- For backend test boundaries and organization, read [dotnet-testing.md](dotnet-testing.md).
