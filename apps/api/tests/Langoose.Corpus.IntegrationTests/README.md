# Langoose.Corpus.IntegrationTests

Integration tests for the corpus database — schema initialiser and importer.

## Running locally

These tests use [Testcontainers](https://dotnet.testcontainers.org/) to spin
up an isolated PostgreSQL container per test class. They require **Docker
Desktop running** on the host machine.

```
dotnet test apps/api/tests/Langoose.Corpus.IntegrationTests
```

Each test class boots its own Postgres 17 container, applies the corpus
schema, and runs assertions. Containers are torn down at the end of the
class. Total runtime: ~5-10 seconds per class with a warm Docker daemon.

## CI

GitHub Actions runners have Docker preinstalled. No additional setup needed
beyond the standard `docker.io` service step.

## Test fixtures

`fixtures/wiktionary-{lang}-sample.jsonl` are committed alongside the tests.
They contain ~3-4 hand-picked entries per language exercising the importer's
main paths: forms, senses, translations, POS allow-listing, and JSONB
containment lookups.
