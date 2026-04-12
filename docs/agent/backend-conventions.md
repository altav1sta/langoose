# Backend Conventions

## C# Style

- Prefer one top-level type per file.
- Prefer primary constructors when they simplify the type.
- Prefer `record` or `record struct` for DTOs and other value-like data carriers when identity semantics are unnecessary.
- Keep namespaces aligned with the project and folder structure.
- Keep public and protected members before private helpers.
- Prefer names that are clear in local scope without adding unnecessary noise.
- Prefer arrays for immutable collection properties and return values unless a more generic interface provides a concrete benefit.
- Avoid redundant materialization, qualification, imports, and DI registrations.
- Replace non-obvious magic numbers with named constants or configuration.
- Use `x` and `y` as default lambda parameter names. Use a descriptive name only when the body is complex or the type isn't obvious from context.
- Avoid the null-forgiving operator (`!`) when the type system or control flow already guarantees non-null. Prefer restructuring so the compiler can see it.
- Use `AsNoTracking()` for read-only queries that do not need change tracking.
- Prefer batch queries over per-item loops to minimize database round-trips.
- Do not split a statement across lines when it fits on one line and reads clearly. Only break for genuinely long lines.

## Related Canonical Docs

- For EF structure, migrations, and seeding, read [efcore-structure.md](efcore-structure.md).
- For backend test boundaries and organization, read [dotnet-testing.md](dotnet-testing.md).
