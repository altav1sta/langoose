#!/usr/bin/env bash
# Rebuild the TEST corpus dump locally (small subset for staging /
# iterative verification). "Rebuild", not "build", because this script
# WIPES wiktionary_entries and re-imports from scratch — the dump is
# deterministic from the LANGUAGES list, not additive over previous
# runs. Does NOT publish; inspect the resulting file first, then run
# scripts/publish-test-corpus-dump.sh when ready.
#
# Steps:
#   1. Ensure local Postgres is running
#   2. Apply schema (idempotent)
#   3. Reset wiktionary data (TRUNCATE + clear source_version metadata)
#   4. Import wordfreq TSVs so wiktionary_entries can then be filtered
#      by frequency. --frequency-filter-top N keeps only entries whose
#      headword is in the top N of wordfreq for that language,
#      producing a representative everyday-vocabulary snapshot. (The
#      previous --limit-N behaviour took the first N JSONL entries,
#      which Kaikki publishes roughly alphabetically — heavy on
#      `a-`/`ab-`, missing common words.)
#   5. Import each language's Kaikki extract with --frequency-filter-top
#      $LIMIT and --defer-indexes — just COPY, no per-lang rebuild
#   6. rebuild-indexes once over all imported data
#   7. pg_dump the whole langoose_corpus DB → data/dump/test-corpus.dump
#
# Test dumps are rolling — same filename every time, overwritten on
# each rebuild. No date in the filename because the test release tag
# is also rolling (a single `test-corpus` tag on GitHub). For dated
# history, use scripts/rebuild-full-corpus-dump.sh.
#
# This script does NOT download anything. Source data must already be
# on disk before running it. Fetch first with:
#   scripts/download-kaikki.sh    <code>            # one per language
#   scripts/download-wordfreq.sh  <code>            # one per language
# Caching them under data/corpus/ is intentional — the rebuild step is
# deterministic and re-runnable without network.
#
# Usage:
#   scripts/rebuild-test-corpus-dump.sh
#
# Env overrides:
#   LANGUAGES="en,ru,de"                 Comma-separated ISO 639 codes.
#                                        Default: "en,ru". The
#                                        code→Kaikki-name map lives in
#                                        scripts/download-kaikki.sh.
#   LIMIT=2000                           Top-N words per language to keep
#                                        (default: 2000). Same value is
#                                        passed to --frequency-filter-top
#                                        on import-wiktionary, so the
#                                        wiktionary row count per language
#                                        approaches LIMIT (lower if a
#                                        word in the top N has no Kaikki
#                                        entry, higher if a word has
#                                        multiple etymology splits).
#   FORCE=1                              Skip the interactive confirmation prompt

set -euo pipefail

LANGUAGES="${LANGUAGES:-en,ru}"
LIMIT="${LIMIT:-2000}"
DUMP_FILE="data/dump/test-corpus.dump"

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
echo " TEST CORPUS DUMP REBUILD"
echo "==================================================================="
echo " Languages to (re-)import : ${LANG_CODES[*]}"
echo " Top-N per language       : $LIMIT (frequency-filtered via wordfreq)"
echo " Output dump file         : $DUMP_FILE (overwritten each rebuild)"
echo ""
echo " This will MUTATE your local langoose_corpus DB:"
echo "   - wiktionary_entries will be TRUNCATED (all languages wiped)"
echo "   - the listed languages are then freshly imported"
echo "   - wordfreq_rankings will gain/refresh per-language rows"
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

# Import wordfreq BEFORE wiktionary so the --frequency-filter-top step
# has rankings to consult.
for LANG_CODE in "${LANG_CODES[@]}"; do
    WORDFREQ_FILE="data/corpus/wordfreq-${LANG_CODE}.tsv"
    echo "==> Importing wordfreq for $LANG_CODE"
    dotnet run --project apps/api/src/Langoose.Corpus.DbTool --configuration Release -- \
        import-wordfreq --lang "$LANG_CODE" --source "$WORDFREQ_FILE"
done

for LANG_CODE in "${LANG_CODES[@]}"; do
    SRC_FILE="data/corpus/wiktionary-${LANG_CODE}.jsonl.gz"
    echo "==> Importing $LANG_CODE, top $LIMIT by frequency — indexes deferred"
    dotnet run --project apps/api/src/Langoose.Corpus.DbTool --configuration Release -- \
        import-wiktionary --lang "$LANG_CODE" --source "$SRC_FILE" \
        --frequency-filter-top "$LIMIT" --defer-indexes
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
