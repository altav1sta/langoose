# Langoose.Corpus.DbTool

CLI for initialising the `langoose_corpus` database schema and running
per-source importers (Wiktionary today; wordfreq, CEFR-J, Tatoeba etc. as
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
specific drive (e.g. `D:` on Windows when `C:` is full), create a `.env`
file at the repo root:

```
POSTGRES_DATA_PATH=D:/langoose-data/postgres
```

then `docker compose up -d postgres` again. The bind mount path will be
created if it doesn't exist.

### 2. Apply the corpus schema

```bash
dotnet run --project apps/api/src/Langoose.Corpus.DbTool -- init
```

Idempotent — re-run safely after pulling schema changes.

### 3. Download a Kaikki extract

```bash
scripts/download-kaikki.sh English ./data/corpus
scripts/download-kaikki.sh Russian ./data/corpus
```

Compressed sizes vary per language: English ~450 MB, Russian ~75 MB.
Uncompressed is roughly 3-4x larger. Choose a destination with enough
free space (e.g. on the same drive you bind-mounted Postgres data to).

### 4. Import

```bash
dotnet run --project apps/api/src/Langoose.Corpus.DbTool -- \
    import-wiktionary --lang en --source ./data/corpus/wiktionary-english.jsonl.gz

dotnet run --project apps/api/src/Langoose.Corpus.DbTool -- \
    import-wiktionary --lang ru --source ./data/corpus/wiktionary-russian.jsonl.gz
```

The importer prints progress every 50k entries during COPY plus a per-step
breakdown on finish (`delete / drop indexes / copy / build indexes /
metadata / commit`). Ballpark timing on a local dev machine (Docker
Postgres on a modern SSD, no competing load):

| Language | Entries | COPY | Index rebuild | Total |
|----------|---------|-----:|--------------:|------:|
| English  | ~900k   | 3–5 min | 1–3 min    | 5–8 min |
| Russian  | ~150k   | 30–60 s | 30–60 s    | 1–2 min |

The importer drops the JSONB GIN index and the `(lang_code, word, pos)`
btree before COPY and rebuilds them afterwards, inside the same
transaction. Per-row GIN maintenance on Wiktionary's nested JSONB is very
slow; building in bulk is typically 10-30× faster. The build uses
`SET LOCAL maintenance_work_mem = '1GB'` and
`max_parallel_maintenance_workers = 4` to keep posting-list sorting in
memory and parallelise the merge — otherwise it spills to disk, which is
punishing on Docker Desktop Windows. Adjust those two knobs in
`WiktionaryIndexMaintenance.cs` if your Postgres container has
significantly less than 2 GB available. See #97 for the per-language
partitioning follow-up that would localise the rebuild cost when many
languages are loaded.

**Multi-language bulk builds**: pass `--defer-indexes` to each
`import-wiktionary` call, then run `rebuild-indexes` once at the end.
This avoids N-1 intermediate rebuilds across the growing corpus. The
`build-full-corpus-dump.sh` / `build-test-corpus-dump.sh` scripts do
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

Two local steps (build, then publish) plus one GitHub Actions step
(restore). The build and publish are separate so you can inspect the
local DB before shipping the dump anywhere.

### Step 1 — Build the dump locally

> **What "build" actually does.** The build script is an end-to-end
> pipeline, not just a `pg_dump` call. It runs, in order:
>
> 1. Starts local Postgres (`docker compose up -d postgres`)
> 2. Applies the corpus schema (`init`, idempotent)
> 3. Resets wiktionary data (`reset-wiktionary`): TRUNCATEs
>    `wiktionary_entries`, clears `source_version_wiktionary_*` metadata
>    rows, drops the JSONB GIN and lookup indexes. The dump is thereby
>    deterministic from the `LANGUAGES` list — languages removed from
>    the list compared to a previous build are gone from the new dump.
> 4. Downloads the Kaikki extracts for each language in `LANGUAGES`
>    (skipped if already cached under `data/corpus/`)
> 5. Imports each language with `--defer-indexes` (COPY + metadata only;
>    indexes stay dropped)
> 6. `rebuild-indexes` once, over all imported data at once
> 7. `pg_dump -Fc` of the whole `langoose_corpus` DB → `data/dump/…`
>
> So the dump contains exactly `LANGUAGES` — nothing more, nothing
> less — regardless of what was in the DB before. The reset step makes
> the build idempotent from the user's perspective.

Full (production): everything imported, no limit.

```bash
scripts/build-full-corpus-dump.sh
# → data/dump/corpus-full-YYYY-MM-DD.dump
# Runtime: ~5-10 min for EN+RU on a fast SSD; longer on slower disks or
# under Docker Desktop overhead. Size: ~600-800 MB.
```

Test (staging / iterative): first N entries per language imported.

```bash
scripts/build-test-corpus-dump.sh
# → data/dump/test-corpus.dump
# Runtime: ~1-3 min. Size: ~5-15 MB. Filename has no date — test dumps
# are rolling and overwritten on each build.
```

Both scripts prompt for confirmation before touching the DB. Skip the
prompt with `FORCE=1` (for scripting or CI).

Env overrides:
```bash
LANGUAGES="English Russian German" scripts/build-full-corpus-dump.sh
LIMIT=2000 LANGUAGES="English" scripts/build-test-corpus-dump.sh
DATE_STAMP=2026-04-15 scripts/build-full-corpus-dump.sh
FORCE=1 scripts/build-test-corpus-dump.sh   # no confirmation prompt
```

After the build, the local `langoose_corpus` database holds exactly
what the dump contains. Inspect it (`docker compose exec -it postgres psql -U langoose -d langoose_corpus`)
before shipping.

### Step 2 — Publish when ready

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
even if you were on a feature branch while running the build. Make sure
`main` is up to date on the remote before publishing.

The test script deletes and recreates its release each time so the
rolling tag actually moves to current main — `gh release edit --target`
is a no-op on already-published tags.

### Step 3 — Restore to an environment → GitHub UI → Actions → "Corpus Restore" → Run workflow

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
| `import-wiktionary --lang <code> --source <jsonl> [--source-version <ver>]` | Bulk-load a Kaikki Wiktionary JSONL extract. Replaces existing rows for that language. |

Future commands tracked under #92: `import-wordfreq`, `import-cefrj`,
`import-tatoeba`, `dump`, `restore`.

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
