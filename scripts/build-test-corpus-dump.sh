#!/usr/bin/env bash
# Build a TEST corpus dump locally (small subset for staging / iterative
# verification). Does NOT publish — inspect the resulting file, verify
# the DB looks right, then run scripts/publish-test-corpus-dump.sh when
# ready.
#
# NOTE: This is an end-to-end pipeline, not just `pg_dump`. It WIPES and
# rebuilds wiktionary_entries so the dump is deterministic from the
# LANGUAGES list: languages removed from LANGUAGES vs. a previous build
# are gone from the new dump.
#
# Steps:
#   1. Ensure local Postgres is running
#   2. Apply schema (idempotent)
#   3. Reset wiktionary data (TRUNCATE + clear source_version metadata)
#   4. Download Kaikki extracts (skipped if already present)
#   5. Import each language with --limit N (default 2000 entries/lang)
#      and --defer-indexes — just COPY
#   6. rebuild-indexes once over all imported data
#   7. pg_dump the whole langoose_corpus DB → data/dump/test-corpus.dump
#
# Test dumps are rolling — same filename every time, overwritten on each
# build. No date in the filename because the test release tag is also
# rolling (a single `test-corpus` tag on GitHub). For dated history,
# build a full dump with `build-full-corpus-dump.sh`.
#
# Usage:
#   scripts/build-test-corpus-dump.sh
#
# Env overrides:
#   LANGUAGES="English Russian"          Kaikki language names (default: English Russian)
#   LIMIT=2000                           Entries per language (default: 2000)
#   FORCE=1                              Skip the interactive confirmation prompt
#
# LIMIT currently takes the first N entries from the JSONL stream, which
# is roughly alphabetical — not a representative vocabulary sample.
# Tracked by #96: switch to a frequency-ranked filter once the wordfreq
# importer lands.

set -euo pipefail

LANGUAGES="${LANGUAGES:-English Russian}"
LIMIT="${LIMIT:-2000}"
DUMP_FILE="data/dump/test-corpus.dump"

mkdir -p data/corpus data/dump

echo ""
echo "==================================================================="
echo " TEST CORPUS DUMP BUILD"
echo "==================================================================="
echo " Languages to (re-)import : $LANGUAGES"
echo " Entries per language     : $LIMIT (first N from JSONL stream)"
echo " Output dump file         : $DUMP_FILE (overwritten each build)"
echo ""
echo " This will MUTATE your local langoose_corpus DB:"
echo "   - wiktionary_entries will be TRUNCATED (all languages wiped)"
echo "   - the listed languages are then freshly imported"
echo "   - the resulting dump contains exactly the listed languages"
echo "==================================================================="
echo ""

if [[ "${FORCE:-0}" != "1" ]]; then
    read -r -p "Proceed? [y/N] " ANSWER
    if [[ ! "$ANSWER" =~ ^[Yy]([Ee][Ss])?$ ]]; then
        echo "Aborted." >&2
        exit 1
    fi
fi

echo "==> Ensuring local Postgres is running"
docker compose up -d postgres
docker compose exec -T postgres bash -c 'until pg_isready -U langoose -d langoose_corpus; do sleep 1; done'

echo "==> Applying corpus schema"
dotnet run --project apps/api/src/Langoose.Corpus.DbTool --configuration Release -- init

echo "==> Resetting Wiktionary data so the dump matches LANGUAGES exactly"
dotnet run --project apps/api/src/Langoose.Corpus.DbTool --configuration Release -- \
    reset-wiktionary

for LANG_NAME in $LANGUAGES; do
    LANG_LOWER=$(echo "$LANG_NAME" | tr '[:upper:]' '[:lower:]')
    LANG_CODE=$(echo "$LANG_LOWER" | cut -c1-2)
    SRC_FILE="data/corpus/wiktionary-${LANG_LOWER}.jsonl.gz"

    if [[ ! -f "$SRC_FILE" ]]; then
        echo "==> Downloading $LANG_NAME"
        scripts/download-kaikki.sh "$LANG_NAME" data/corpus
    else
        echo "==> Reusing cached $SRC_FILE"
    fi

    echo "==> Importing $LANG_NAME ($LANG_CODE), first $LIMIT entries — indexes deferred"
    dotnet run --project apps/api/src/Langoose.Corpus.DbTool --configuration Release -- \
        import-wiktionary --lang "$LANG_CODE" --source "$SRC_FILE" --limit "$LIMIT" --defer-indexes
done

echo "==> Rebuilding indexes (one-time, covers all imported languages)"
dotnet run --project apps/api/src/Langoose.Corpus.DbTool --configuration Release -- \
    rebuild-indexes

echo "==> Dumping to $DUMP_FILE"
docker compose exec -T postgres \
    pg_dump -Fc --no-owner --no-acl -U langoose -d langoose_corpus \
    > "$DUMP_FILE"

du -h "$DUMP_FILE"

echo ""
echo "Dump ready at $DUMP_FILE."
echo "Inspect the local langoose_corpus DB if you want, then publish with:"
echo "  scripts/publish-test-corpus-dump.sh"
