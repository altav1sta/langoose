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
#   3. Reset wiktionary, wordfreq, AND tatoeba data (TRUNCATE tables +
#      clear source metadata). All three resets are required:
#      import-wordfreq deletes per-(lang, source), so a cross-date
#      rebuild or a language dropped from LANGUAGES would leave stale
#      rankings behind and pollute the dump + --frequency-filter-top.
#   4. Import wordfreq TSVs so wiktionary_entries can then be filtered
#      by frequency. --frequency-filter-top N keeps only entries whose
#      headword is in the top N of wordfreq for that language,
#      producing a representative everyday-vocabulary snapshot. (The
#      previous --limit-N behaviour took the first N JSONL entries,
#      which Kaikki publishes roughly alphabetically — heavy on
#      `a-`/`ab-`, missing common words.)
#   5. Import each language's Kaikki extract with --frequency-filter-top
#      $LIMIT and --defer-indexes — just COPY, no per-lang rebuild
#   6. Import the Tatoeba bilingual pair with --max-pairs $LIMIT —
#      keeps up to $LIMIT cross-language sentence pairs (with up to 2
#      pairs per sentence for diversity), each contributing both link
#      directions, and drops orphan sentences. Guarantees every
#      sentence participates in at least one pair AND every pair has
#      both directions. Naive ordering; to be replaced by a lemma-
#      frequency filter once #114 lands. See
#      docs/agent/parallel-corpora.md.
#   7. rebuild-indexes once over all imported data
#   8. pg_dump the whole langoose_corpus DB → data/dump/test-corpus.dump
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
#   scripts/download-tatoeba.sh   <lang> <pair>     # the bilingual pair
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
#   TATOEBA_PAIR="en-ru"                 Hyphen-separated pair to import.
#                                        Default: first two languages in
#                                        LANGUAGES if 2+ entries; empty
#                                        otherwise. Set to "" to skip.
#                                        The Tatoeba `--max-links` budget
#                                        reuses $LIMIT — same dial sizes
#                                        both the wiktionary slice and
#                                        the Tatoeba slice.
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

# Default Tatoeba pair = first two languages in LANGUAGES. Operator can
# override via TATOEBA_PAIR or set it empty to skip the Tatoeba step.
if [[ -z "${TATOEBA_PAIR+x}" ]]; then
    if (( ${#LANG_CODES[@]} >= 2 )); then
        TATOEBA_PAIR="${LANG_CODES[0]}-${LANG_CODES[1]}"
    else
        TATOEBA_PAIR=""
    fi
fi

if [[ -n "$TATOEBA_PAIR" ]]; then
    IFS='-' read -r TATOEBA_LANG TATOEBA_PAIR_LANG <<< "$TATOEBA_PAIR"
    if [[ -z "${TATOEBA_LANG:-}" || -z "${TATOEBA_PAIR_LANG:-}" ]]; then
        echo "TATOEBA_PAIR='$TATOEBA_PAIR' is malformed. Expected two hyphen-separated codes (e.g. 'en-ru')." >&2
        exit 1
    fi
    TATOEBA_DIR="data/corpus/tatoeba"
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
if [[ -n "$TATOEBA_PAIR" ]]; then
    for f in "${TATOEBA_LANG}_sentences.tsv" "${TATOEBA_PAIR_LANG}_sentences.tsv" "links.tsv"; do
        if [[ ! -f "$TATOEBA_DIR/$f" ]]; then
            MISSING+=("$TATOEBA_DIR/$f  →  scripts/download-tatoeba.sh $TATOEBA_LANG $TATOEBA_PAIR_LANG")
        fi
    done
fi
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
if [[ -n "$TATOEBA_PAIR" ]]; then
    echo " Tatoeba pair             : $TATOEBA_PAIR (max $LIMIT cross-language sentence pairs)"
else
    echo " Tatoeba pair             : (skipped — TATOEBA_PAIR is empty)"
fi
echo " Output dump file         : $DUMP_FILE (overwritten each rebuild)"
echo ""
echo " This will MUTATE your local langoose_corpus DB:"
echo "   - wiktionary_entries, wordfreq_rankings, tatoeba_sentences,"
echo "     and tatoeba_links will be TRUNCATED (all languages /"
echo "     sources / pairs wiped)"
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

echo "==> Resetting Tatoeba data so the dump matches TATOEBA_PAIR exactly"
dotnet run --project apps/api/src/Langoose.Corpus.DbTool --configuration Release -- \
    reset-tatoeba

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

if [[ -n "$TATOEBA_PAIR" ]]; then
    echo "==> Importing Tatoeba pair $TATOEBA_LANG-$TATOEBA_PAIR_LANG, capped at $LIMIT cross-language sentence pairs"
    dotnet run --project apps/api/src/Langoose.Corpus.DbTool --configuration Release -- \
        import-tatoeba --lang "$TATOEBA_LANG" --pair-lang "$TATOEBA_PAIR_LANG" \
        --source "$TATOEBA_DIR" --max-pairs "$LIMIT"
fi

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
