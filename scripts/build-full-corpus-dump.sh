#!/usr/bin/env bash
# Build the FULL corpus dump locally. Does NOT publish — inspect the
# resulting file, verify the DB looks right, then run
# scripts/publish-full-corpus-dump.sh when ready.
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
#   5. Import each language (no --limit) with --defer-indexes — just COPY,
#      no per-lang rebuild
#   6. rebuild-indexes once over all imported data
#   7. pg_dump the whole langoose_corpus DB → data/dump/corpus-full-YYYY-MM-DD.dump
#
# Usage:
#   scripts/build-full-corpus-dump.sh
#
# Env overrides:
#   LANGUAGES="en,ru,de"                 Comma-separated ISO 639 codes.
#                                        Default: "en,ru". Codes are
#                                        resolved to Kaikki URL segments
#                                        by scripts/download-kaikki.sh;
#                                        add new codes there.
#   DATE_STAMP="2026-04-15"              Override the date in the output filename
#   FORCE=1                              Skip the interactive confirmation prompt

set -euo pipefail

DATE_STAMP="${DATE_STAMP:-$(date -u +%Y-%m-%d)}"
LANGUAGES="${LANGUAGES:-en,ru}"
DUMP_FILE="data/dump/corpus-full-${DATE_STAMP}.dump"

# Parse LANGUAGES into LANG_CODES. Strip whitespace, drop blanks.
IFS=',' read -ra LANG_CODES <<< "${LANGUAGES// /}"
LANG_CODES=($(printf '%s\n' "${LANG_CODES[@]}" | grep -v '^$' || true))
if [[ ${#LANG_CODES[@]} -eq 0 ]]; then
    echo "LANGUAGES is empty or missing. Example: LANGUAGES='en,ru'." >&2
    exit 1
fi

mkdir -p data/corpus data/dump

echo ""
echo "==================================================================="
echo " FULL CORPUS DUMP BUILD"
echo "==================================================================="
echo " Languages to (re-)import : ${LANG_CODES[*]}"
echo " Output dump file         : $DUMP_FILE"
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

for LANG_CODE in "${LANG_CODES[@]}"; do
    SRC_FILE="data/corpus/wiktionary-${LANG_CODE}.jsonl.gz"

    if [[ ! -f "$SRC_FILE" ]]; then
        echo "==> Downloading $LANG_CODE"
        scripts/download-kaikki.sh "$LANG_CODE" data/corpus
    else
        echo "==> Reusing cached $SRC_FILE"
    fi

    echo "==> Importing $LANG_CODE — indexes deferred"
    dotnet run --project apps/api/src/Langoose.Corpus.DbTool --configuration Release -- \
        import-wiktionary --lang "$LANG_CODE" --source "$SRC_FILE" --defer-indexes
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
echo "  scripts/publish-full-corpus-dump.sh $DUMP_FILE"
