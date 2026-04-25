#!/usr/bin/env bash
# Rebuild the FULL corpus dump locally. "Rebuild", not "build", because
# this script WIPES wiktionary_entries and re-imports from scratch — the
# dump is deterministic from the LANGUAGES list, not additive over
# previous runs. Does NOT publish; inspect the resulting file first,
# then run scripts/publish-full-corpus-dump.sh when ready.
#
# Steps:
#   1. Ensure local Postgres is running
#   2. Apply schema (idempotent)
#   3. Reset wiktionary AND wordfreq data (TRUNCATE both tables +
#      clear source_version metadata). Both resets are required:
#      import-wordfreq deletes per-(lang, source), so a cross-date
#      rebuild or a language dropped from LANGUAGES would leave stale
#      rankings in the published dump.
#   4. Import wordfreq TSVs so the published dump ships frequency
#      rankings alongside dictionary data. Provider code can rank
#      multi-candidate translations by frequency without re-fetching
#      wordfreq at runtime.
#   5. Import each language's Kaikki extract (no filter, no --limit)
#      with --defer-indexes — just COPY, no per-lang rebuild
#   6. rebuild-indexes once over all imported data
#   7. pg_dump the whole langoose_corpus DB → data/dump/corpus-full-YYYY-MM-DD.dump
#
# This script does NOT download anything. Source data must already be
# on disk before running it. Fetch first with:
#   scripts/download-kaikki.sh    <code>            # one per language
#   scripts/download-wordfreq.sh  <code>            # one per language
# Caching them under data/corpus/ is intentional — the rebuild step is
# deterministic and re-runnable without network.
#
# Usage:
#   scripts/rebuild-full-corpus-dump.sh
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

# Pre-flight: every language needs both a Kaikki extract and a wordfreq
# TSV on disk before we touch the DB. Fail fast with a per-file pointer
# rather than a half-built database.
MISSING=()
for LANG_CODE in "${LANG_CODES[@]}"; do
    KAIKKI_FILE="data/corpus/wiktionary-${LANG_CODE}.jsonl.gz"
    WORDFREQ_FILE="data/corpus/wordfreq-${LANG_CODE}.tsv"
    if [[ ! -f "$KAIKKI_FILE" ]]; then
        MISSING+=("$KAIKKI_FILE  →  scripts/download-kaikki.sh $LANG_CODE")
    fi
    if [[ ! -f "$WORDFREQ_FILE" ]]; then
        MISSING+=("$WORDFREQ_FILE  →  scripts/download-wordfreq.sh $LANG_CODE")
    fi
done
if (( ${#MISSING[@]} > 0 )); then
    echo "Missing source files. Fetch them first, then re-run:" >&2
    for line in "${MISSING[@]}"; do
        echo "  - $line" >&2
    done
    exit 1
fi

mkdir -p data/dump

echo ""
echo "==================================================================="
echo " FULL CORPUS DUMP REBUILD"
echo "==================================================================="
echo " Languages to (re-)import : ${LANG_CODES[*]}"
echo " Output dump file         : $DUMP_FILE"
echo ""
echo " This will MUTATE your local langoose_corpus DB:"
echo "   - wiktionary_entries AND wordfreq_rankings will be TRUNCATED"
echo "     (all languages and sources wiped)"
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

echo "==> Resetting wordfreq data so prior dates / dropped languages don't linger"
dotnet run --project apps/api/src/Langoose.Corpus.DbTool --configuration Release -- \
    reset-wordfreq

# Import wordfreq so the published dump carries frequency rankings
# (used by the corpus-backed enrichment provider to rank multi-candidate
# translations).
for LANG_CODE in "${LANG_CODES[@]}"; do
    WORDFREQ_FILE="data/corpus/wordfreq-${LANG_CODE}.tsv"
    echo "==> Importing wordfreq for $LANG_CODE"
    dotnet run --project apps/api/src/Langoose.Corpus.DbTool --configuration Release -- \
        import-wordfreq --lang "$LANG_CODE" --source "$WORDFREQ_FILE"
done

for LANG_CODE in "${LANG_CODES[@]}"; do
    SRC_FILE="data/corpus/wiktionary-${LANG_CODE}.jsonl.gz"
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
