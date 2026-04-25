# Langoose.Corpus.DbTool

CLI for initialising the `langoose_corpus` database schema and running
per-source importers (Wiktionary, wordfreq today; CEFR-J, Tatoeba etc. as
they're added in follow-up sub-issues of #92).

> **Data comes from upstream projects under copyleft licenses.** Every
> source the importer consumes is documented in
> [`ATTRIBUTION.md`](../../../../ATTRIBUTION.md) at the repo root. If
> you publish a corpus dump from this tool, the `publish-*-corpus-dump.sh`
> scripts automatically ship the attribution file as a release asset —
> required by CC-BY-SA 4.0.

## Local workflow

Postgres comes from Docker Compose at the repo root. The init script under
`infra/postgres/init/` provisions `langoose_app`, `langoose_auth`, and
`langoose_corpus` databases on first startup.

### 1. Bring up Postgres

```bash
docker compose up -d postgres
```

By default the data lives in a Docker-managed volume. To put it on a
specific path (e.g. a different drive on Windows when the system drive
is full, or an external SSD on Linux/macOS), create a `.env` file at
the repo root:

```
POSTGRES_DATA_PATH=./data/postgres
```

then `docker compose up -d postgres` again. The bind mount path will be
created if it doesn't exist.

### 2. Apply the corpus schema

```bash
dotnet run --project apps/api/src/Langoose.Corpus.DbTool -- init
```

Idempotent — re-run safely after pulling schema changes.

### 3. Download source data per language

Two parallel sources, one pair per language. Both are cached locally
under `data/corpus/`; later steps assume the files are present.

```bash
# Kaikki Wiktionary JSONL extracts. Compressed sizes: English ~450 MB,
# Russian ~75 MB. Uncompressed is 3–4x larger — make sure the
# destination has free space.
scripts/download-kaikki.sh en
scripts/download-kaikki.sh ru

# wordfreq frequency rankings. Runs the upstream Python package inside
# python:3-slim via Docker by default, so no local Python install is
# required. Output is small (~5–10 MB per language). Set PYTHON=python3
# to use a local interpreter instead (must have `wordfreq` installed).
scripts/download-wordfreq.sh en
scripts/download-wordfreq.sh ru
```

### 4. Import

Import wordfreq first so `import-wiktionary` can filter against it
(`--frequency-filter-top <N>` is optional — omit it for a full import):

```bash
# Frequency rankings (small, fast).
dotnet run --project apps/api/src/Langoose.Corpus.DbTool -- \
    import-wordfreq --lang en --source ./data/corpus/wordfreq-en.tsv
dotnet run --project apps/api/src/Langoose.Corpus.DbTool -- \
    import-wordfreq --lang ru --source ./data/corpus/wordfreq-ru.tsv

# Wiktionary entries — full import, no filter.
dotnet run --project apps/api/src/Langoose.Corpus.DbTool -- \
    import-wiktionary --lang en --source ./data/corpus/wiktionary-en.jsonl.gz
dotnet run --project apps/api/src/Langoose.Corpus.DbTool -- \
    import-wiktionary --lang ru --source ./data/corpus/wiktionary-ru.jsonl.gz
```

For an iterative/staging slice, add `--frequency-filter-top <N>` to the
`import-wiktionary` step — it keeps only headwords whose word is in the
top N of `wordfreq_rankings` for that language:

```bash
dotnet run --project apps/api/src/Langoose.Corpus.DbTool -- \
    import-wiktionary --lang en --source ./data/corpus/wiktionary-en.jsonl.gz \
    --frequency-filter-top 2000
```

The importer prints progress every 50k entries during COPY plus a per-step
breakdown on finish (`drop indexes / truncate / copy / build indexes /
metadata / commit`). Ballpark timing on a local dev machine (Docker
Postgres on a modern SSD, no competing load):

| Language | Entries | COPY | Index rebuild | Total |
|----------|---------|-----:|--------------:|------:|
| English  | ~900k   | 3–5 min | 1–3 min    | 5–8 min |
| Russian  | ~150k   | 30–60 s | 30–60 s    | 1–2 min |

`wiktionary_entries` is LIST-partitioned by `lang_code` (#97). Each
language gets its own partition `wiktionary_entries_<lang>` with its
own pair of indexes — a `(lang_code, word, pos)` btree and a JSONB GIN
on `data jsonb_path_ops`. The importer drops the partition's two
indexes before COPY, TRUNCATEs the partition, COPYs new rows, and
rebuilds those two indexes — all inside one transaction, only touching
this language. Per-row GIN maintenance on Wiktionary's nested JSONB is
very slow; building in bulk is typically 10-30× faster. The build uses
`SET LOCAL maintenance_work_mem = '1GB'` and
`max_parallel_maintenance_workers = 4` to keep posting-list sorting in
memory and parallelise the merge — otherwise it spills to disk, which
is punishing on Docker Desktop Windows. Adjust those two knobs in
`WiktionaryIndexMaintenance.cs` if your Postgres container has
significantly less than 2 GB available.

**Multi-language bulk builds**: pass `--defer-indexes` to each
`import-wiktionary` call, then run `rebuild-indexes` once at the end.
This avoids N-1 intermediate rebuilds across the growing corpus. The
`rebuild-full-corpus-dump.sh` / `rebuild-test-corpus-dump.sh` scripts do
this automatically. For an incremental re-import of one language on a
corpus that's already indexed, just run `import-wiktionary` without the
flag — the default behaviour drops/rebuilds around the COPY atomically.

Hardware variance is large — slower disks, Docker Desktop overhead, or
contention from other Postgres workloads can easily double these. The
mini dump (2000 entries/lang) finishes in seconds regardless.

### Inspecting the result

```bash
docker exec -it langoose-postgres psql -U langoose -d langoose_corpus

langoose_corpus=> SELECT lang_code, word, pos FROM wiktionary_entries
                  WHERE word = 'book' AND lang_code = 'en';
langoose_corpus=> SELECT * FROM corpus_metadata;
```

## Producing and deploying dump artifacts

Three local steps (download, rebuild, publish) plus one GitHub Actions
step (restore). Each step is separate and idempotent: download is
network-bound, rebuild is DB-bound and re-runnable without network,
publish ships the dump to GitHub Releases.

### Step 1 — Download source data per language

```bash
# One pair per language. Skips if files already cached under data/corpus/.
scripts/download-kaikki.sh    en
scripts/download-wordfreq.sh  en   # uses Docker by default (PYTHON=python3 to opt out)

scripts/download-kaikki.sh    ru
scripts/download-wordfreq.sh  ru
```

The rebuild scripts in step 2 fail fast if either file is missing for
any language in `LANGUAGES`, with a pointer to the right download
command per missing file.

### Step 2 — Rebuild the dump locally

> **What "rebuild" actually does.** The rebuild script is an end-to-end
> pipeline, not just a `pg_dump` call. It runs, in order:
>
> 1. Starts local Postgres (`docker compose up -d postgres`)
> 2. Applies the corpus schema (`init`, idempotent)
> 3. Resets both source tables (`reset-wiktionary` + `reset-wordfreq`):
>    drops every `wiktionary_entries_<lang>` partition, TRUNCATEs
>    `wordfreq_rankings`, and clears `source_version_*` metadata rows.
>    Both resets are required: `import-wordfreq` only deletes per
>    `(lang, source)`, so a cross-date rebuild or a language dropped
>    from `LANGUAGES` would otherwise leave stale rankings behind, and
>    a partition for a removed language would linger as an empty
>    partition in the dump. The dump is thereby deterministic from the
>    `LANGUAGES` list — anything removed from the list since a previous
>    rebuild is gone from the new dump.
> 4. Imports the wordfreq TSV per language via `import-wordfreq`.
>    Required by the test rebuild (drives the frequency filter);
>    included in the full rebuild so the published dump ships rankings.
> 5. Imports each language's Kaikki extract with `--defer-indexes`
>    (COPY + metadata only; indexes stay dropped). The test rebuild
>    adds `--frequency-filter-top $LIMIT` so only headwords in the top
>    N of wordfreq for that language land. The full rebuild imports
>    everything.
> 6. `rebuild-indexes` once, over all imported data at once
> 7. `pg_dump -Fc` of the whole `langoose_corpus` DB → `data/dump/…`
>
> So the dump contains exactly `LANGUAGES` — nothing more, nothing
> less — regardless of what was in the DB before. The reset step makes
> the rebuild idempotent from the user's perspective.

Full (production): everything imported, no filter.

```bash
scripts/rebuild-full-corpus-dump.sh
# → data/dump/corpus-full-YYYY-MM-DD.dump
# Runtime: ~5-10 min for EN+RU on a fast SSD; longer on slower disks or
# under Docker Desktop overhead. Size: ~600-800 MB.
```

Test (staging / iterative): top-N-by-wordfreq entries per language imported.

```bash
scripts/rebuild-test-corpus-dump.sh
# → data/dump/test-corpus.dump
# Runtime: ~1-3 min. Size: ~5-15 MB. Filename has no date — test dumps
# are rolling and overwritten on each rebuild.
```

Both scripts prompt for confirmation before touching the DB. Skip the
prompt with `FORCE=1` (for scripting or CI).

Env overrides:
```bash
# LANGUAGES is a comma-separated list of ISO 639 codes.
# Unsupported codes fail fast with a pointer to that file. Default: "en,ru".
LANGUAGES="ang,enm,de" scripts/rebuild-full-corpus-dump.sh
LIMIT=2000 LANGUAGES="en" scripts/rebuild-test-corpus-dump.sh
DATE_STAMP=2026-04-15 scripts/rebuild-full-corpus-dump.sh
FORCE=1 scripts/rebuild-test-corpus-dump.sh   # no confirmation prompt
```

After the rebuild, the local `langoose_corpus` database holds exactly
what the dump contains. Inspect it (`docker compose exec -it postgres psql -U langoose -d langoose_corpus`)
before shipping.

### Step 3 — Publish when ready

Flavour-specific scripts — each one knows its own tagging policy and
target:

```bash
# Full (permanent dated release, for production)
scripts/publish-full-corpus-dump.sh data/dump/corpus-full-2026-04-15.dump
#   → release tag: corpus-full-2026-04-15 (unique, preserved)

# Test (rolling release, for staging) — publishes data/dump/test-corpus.dump
scripts/publish-test-corpus-dump.sh
#   → release tag: test-corpus (single, overwritten on each publish)
```

Both scripts `--target main`, so tags always land on preserved history
even if you were on a feature branch while running the rebuild. Make sure
`main` is up to date on the remote before publishing.

The test script deletes and recreates its release each time so the
rolling tag actually moves to current main — `gh release edit --target`
is a no-op on already-published tags.

### Step 4 — Restore to an environment → GitHub UI → Actions → "Corpus Restore" → Run workflow

Inputs:

| Input | Description |
|-------|-------------|
| `target_environment` | `staging` or `production` |
| `release_tag` | `test-corpus` for staging, `corpus-full-<date>` for prod. |

The workflow downloads the single `.dump` asset attached to the release
(filename-agnostic, tag-agnostic) and runs `pg_restore --clean --if-exists`
against the target's corpus database. **This drops and recreates every
object in the target DB** — the restored state is exactly the snapshot
the dump was built from. There's no additive/delta mode; to add a
language to production, rebuild the dump with the full language list
and restore again.

### One-time setup per environment

In GitHub → **Settings → Environments → {staging|production} → Secrets**,
add:

| Secret | Value |
|--------|-------|
| `CORPUS_DATABASE` | Full Postgres connection string for that env's corpus database (Neon) |

That's it. From then on, "build dump" and "restore corpus" are two clicks
each.

## Subcommands

| Command | Purpose |
|---------|---------|
| `init` | Apply embedded SQL schema files in order. Idempotent. |
| `import-wiktionary --lang <code> --source <jsonl> [--source-version <ver>] [--limit <n>] [--frequency-filter-top <n>] [--defer-indexes]` | Bulk-load a Kaikki Wiktionary JSONL extract. Replaces existing rows for that language. `--frequency-filter-top` keeps only headwords in the top N of `wordfreq_rankings` (run `import-wordfreq` first). |
| `import-wordfreq --lang <code> --source <tsv> [--source-version <ver>]` | Bulk-load a wordfreq frequency-ranking TSV (`word\trank\tzipf_score`, `.gz` allowed). Replaces existing rows for that (lang, source). Fetch the TSV with `scripts/download-wordfreq.sh`. |
| `reset-wiktionary` | Drop every `wiktionary_entries_<lang>` partition (and its indexes) and clear `source_version_wiktionary_*` metadata. Use at the start of a bulk multi-language rebuild — partitions for languages no longer in `LANGUAGES` are removed, not just emptied. |
| `reset-wordfreq` | Truncate `wordfreq_rankings`, clear `source_version_wordfreq_*` metadata. Required at the start of a rebuild — `import-wordfreq` only deletes per `(lang, source)`, so cross-date rebuilds or dropped languages would otherwise accumulate stale rankings. |
| `rebuild-indexes` | Iterate every `wiktionary_entries_<lang>` partition and drop+recreate its two indexes. Idempotent. Used as the final step of a multi-language `--defer-indexes` build. |

Future commands tracked under #92: `import-cefrj`, `import-tatoeba`,
`dump`, `restore`.

## Configuration

Connection string is read from `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "CorpusDatabase": "Host=localhost;Port=5432;Database=langoose_corpus;Username=langoose;Password=langoose"
  }
}
```

Environment variable override: `ConnectionStrings__CorpusDatabase=...`.

### Testing

Run `dotnet test apps/api/tests/Langoose.Corpus.IntegrationTests`. Each
test class spins up its own Postgres container via Testcontainers and
disposes it afterwards, so nothing on your local Postgres is touched.
This covers the importer end-to-end (schema, COPY, index drop/rebuild,
metadata, `reset-wiktionary`, `rebuild-indexes`). `Program.cs` is thin
argument-parsing plumbing — the logic it dispatches to is fully covered
by these tests.
