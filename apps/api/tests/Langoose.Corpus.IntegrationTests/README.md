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
They contain ~3-4 hand-picked entries per language exercising the
Wiktionary importer's main paths: forms, senses, translations, POS
allow-listing, and JSONB containment lookups.

`fixtures/wordfreq-{lang}-sample.tsv` are short (~10-row) TSV fixtures that
exercise the wordfreq importer's parse + replace path, and drive the
`--frequency-filter-top` test on the Wiktionary importer.

`fixtures/tatoeba/` mirrors the layout `scripts/download-tatoeba.sh`
produces: `{en,ru}_sentences.tsv` (~5 sentences each, `id\tlang\ttext`)
plus `links.tsv` with cross-language pairs and a couple of orphan rows
the importer must filter. Drives the AC round-trip test (lookup by
`(lang_code, sentence_id)` then follow a link to the paired sentence)
and the orphan-filtering test.
