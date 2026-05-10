#!/usr/bin/env bash
# Download Tatoeba sentence dumps and the global translation-links file
# into a shared directory. Sibling of download-kaikki.sh /
# download-wordfreq.sh; same role (fetch source data and put it on disk
# under data/corpus/) but produces a directory rather than a single
# file because import-tatoeba consumes three files together (per-lang
# sentences for the chosen pair + the one shared links file).
#
# Why a shared directory: links.tsv is the global Tatoeba links file —
# it covers every language pair, not just the one passed to this
# script. Per-language sentence files are also independent of any
# specific pair (eng_sentences.tsv is the same file regardless of
# whether you're pairing English with Russian or German). Putting them
# all under data/corpus/tatoeba/ lets multiple pair imports share one
# downloaded copy of links.tsv (~1 GB) and skips re-downloading
# sentence files for languages that are already on disk.
#
# Usage:
#   scripts/download-tatoeba.sh <lang> <pair-lang> [out-dir]
#
# Examples:
#   scripts/download-tatoeba.sh en ru
#   scripts/download-tatoeba.sh en de              # adds de_sentences.tsv alongside existing en_sentences.tsv
#   scripts/download-tatoeba.sh en ru ./somewhere  # custom out-dir
#
# Defaults:
#   out-dir   ./data/corpus/tatoeba
#
# Layout produced (importer expects exactly these filenames in <out-dir>):
#   <out-dir>/<lang>_sentences.tsv      e.g. en_sentences.tsv
#   <out-dir>/<pair-lang>_sentences.tsv e.g. ru_sentences.tsv
#   <out-dir>/links.tsv
#
# Sentence files and links.tsv are skipped if already present. Re-run
# with FORCE_REDOWNLOAD=1 to re-fetch.
#
# License: CC-BY 2.0 (text). Audio is CC-BY-NC and is *not* downloaded.
# Every dump shipped from this repo must keep ATTRIBUTION.md in lockstep.
#
# Sizes vary per language. eng_sentences.tsv ~= 200 MB compressed,
# rus_sentences.tsv ~= 50 MB. links.tar.bz2 is the global file (~150 MB
# compressed, ~1 GB uncompressed) — the importer filters offline to the
# pair, so this script always pulls the global one regardless of the
# requested pair.
#
# Why bzip2? Tatoeba publishes only .bz2. Decompression happens in this
# script (via `bzcat`/`bzip2 -d`) so the importer has no bzip2
# dependency — it only needs to read plain or .gz files. macOS,
# Linux, and Git Bash on Windows all ship `bzip2`.

set -euo pipefail

# Map ISO 639-1 → Tatoeba's three-letter code. Tatoeba uses ISO 639-3
# (or 639-2/B for some older entries). Extend as needed; unknown codes
# fail fast with a pointer back to this script.
tatoeba_code_for() {
    case "$1" in
        ar)  echo "ara" ;;
        bg)  echo "bul" ;;
        ca)  echo "cat" ;;
        cs)  echo "ces" ;;
        da)  echo "dan" ;;
        de)  echo "deu" ;;
        el)  echo "ell" ;;
        en)  echo "eng" ;;
        es)  echo "spa" ;;
        fi)  echo "fin" ;;
        fr)  echo "fra" ;;
        he)  echo "heb" ;;
        hi)  echo "hin" ;;
        hu)  echo "hun" ;;
        id)  echo "ind" ;;
        it)  echo "ita" ;;
        ja)  echo "jpn" ;;
        ko)  echo "kor" ;;
        nl)  echo "nld" ;;
        no)  echo "nor" ;;
        pl)  echo "pol" ;;
        pt)  echo "por" ;;
        ro)  echo "ron" ;;
        ru)  echo "rus" ;;
        sv)  echo "swe" ;;
        tr)  echo "tur" ;;
        uk)  echo "ukr" ;;
        vi)  echo "vie" ;;
        zh)  echo "cmn" ;;
        *)   return 1 ;;
    esac
}

if [[ $# -lt 2 ]]; then
    echo "Usage: $0 <lang> <pair-lang> [out-dir]" >&2
    echo "Example: $0 en ru" >&2
    exit 1
fi

LANG_CODE="$1"
PAIR_LANG_CODE="$2"
OUT_DIR="${3:-./data/corpus/tatoeba}"
FORCE_REDOWNLOAD="${FORCE_REDOWNLOAD:-0}"

if [[ "$LANG_CODE" == "$PAIR_LANG_CODE" ]]; then
    echo "<lang> and <pair-lang> must differ; both were '$LANG_CODE'." >&2
    exit 1
fi

if ! command -v bzip2 >/dev/null 2>&1; then
    echo "bzip2 not found on PATH." >&2
    echo "Tatoeba publishes only .bz2 archives — install bzip2 (Linux/macOS:" >&2
    echo "  ships by default; Windows: included with Git Bash) and re-run." >&2
    exit 1
fi

if ! LANG_TATOEBA=$(tatoeba_code_for "$LANG_CODE"); then
    echo "Unknown ISO 639-1 code '$LANG_CODE'." >&2
    echo "Add it to the tatoeba_code_for function in scripts/download-tatoeba.sh" >&2
    echo "(value = the three-letter code Tatoeba uses for that language)." >&2
    exit 1
fi

if ! PAIR_TATOEBA=$(tatoeba_code_for "$PAIR_LANG_CODE"); then
    echo "Unknown ISO 639-1 code '$PAIR_LANG_CODE'." >&2
    echo "Add it to the tatoeba_code_for function in scripts/download-tatoeba.sh" >&2
    echo "(value = the three-letter code Tatoeba uses for that language)." >&2
    exit 1
fi

mkdir -p "$OUT_DIR"

# Fetch one per-language sentence file. Tatoeba names per-language
# exports <3code>_sentences.tsv.bz2; we rename to <2code>_sentences.tsv
# locally so the importer's filename convention is the same 2-letter
# code passed via --lang.
fetch_sentences() {
    local two_code="$1"
    local three_code="$2"
    local archive="$OUT_DIR/${three_code}_sentences.tsv.bz2"
    local out_file="$OUT_DIR/${two_code}_sentences.tsv"
    local url="https://downloads.tatoeba.org/exports/per_language/${three_code}/${three_code}_sentences.tsv.bz2"

    if [[ -f "$out_file" && "$FORCE_REDOWNLOAD" != "1" ]]; then
        echo "Skipping $out_file (already on disk; FORCE_REDOWNLOAD=1 to re-fetch)"
        return
    fi

    echo "Downloading $url"
    echo "    → $archive"
    curl -L --fail --progress-bar -o "$archive" "$url"

    echo "Decompressing → $out_file"
    bzcat "$archive" > "$out_file"
    rm -f "$archive"
}

fetch_sentences "$LANG_CODE"      "$LANG_TATOEBA"
fetch_sentences "$PAIR_LANG_CODE" "$PAIR_TATOEBA"

# Global links file. Tatoeba ships it as a tar.bz2 containing a single
# `links.csv` (TSV-formatted despite the .csv extension). Rename to
# links.tsv on disk to match the importer's expectation. Shared across
# every pair import — once on disk, subsequent script invocations
# skip re-downloading.
LINKS_ARCHIVE="$OUT_DIR/links.tar.bz2"
LINKS_OUT="$OUT_DIR/links.tsv"
LINKS_URL="https://downloads.tatoeba.org/exports/links.tar.bz2"

if [[ -f "$LINKS_OUT" && "$FORCE_REDOWNLOAD" != "1" ]]; then
    echo "Skipping $LINKS_OUT (already on disk; FORCE_REDOWNLOAD=1 to re-fetch)"
else
    echo "Downloading $LINKS_URL"
    echo "    → $LINKS_ARCHIVE"
    curl -L --fail --progress-bar -o "$LINKS_ARCHIVE" "$LINKS_URL"

    echo "Extracting → $LINKS_OUT"
    # tar -xOf streams the archive's single member to stdout so we get
    # to rename and avoid littering the directory with the upstream
    # `links.csv` filename.
    tar -xOf "$LINKS_ARCHIVE" --bzip2 > "$LINKS_OUT"
    rm -f "$LINKS_ARCHIVE"
fi

SIZE=$(du -sh "$OUT_DIR" | cut -f1)
echo "Done. $OUT_DIR ($SIZE)"
echo ""
echo "Next step:"
echo "  dotnet run --project apps/api/src/Langoose.Corpus.DbTool -- \\"
echo "    import-tatoeba --lang $LANG_CODE --pair-lang $PAIR_LANG_CODE --source $OUT_DIR"
