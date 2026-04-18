#!/usr/bin/env bash
# Download a Kaikki Wiktionary JSONL extract for a single language.
#
# Usage:
#   scripts/download-kaikki.sh <iso-code> [out-dir]
#
# Examples:
#   scripts/download-kaikki.sh en ./data/corpus
#
# Takes an ISO 639 code (not a human-readable name). The code→Kaikki-
# URL-segment mapping lives in this script; extend it when you need a
# new language. Codes are the same thing passed to
# `import-wiktionary --lang`, so upstream callers don't need to care
# about Kaikki's naming conventions.
#
# Notes:
#   - Output filename uses the ISO code verbatim:
#     wiktionary-<code>.jsonl.gz. Downstream build scripts expect that
#     exact pattern.
#   - Compressed sizes vary per language: English ~450 MB, Russian ~75 MB.
#     Uncompressed is roughly 3-4x larger. Make sure the destination has
#     enough space.

set -euo pipefail

# Map ISO 639 codes → Kaikki URL segment. Values are per-language path
# segments on kaikki.org (may contain spaces — URL-encoded below).
# Extend as needed; unknown codes fail fast with a pointer here.
declare -A KAIKKI_NAMES=(
    [ang]="Old English"
    [ar]="Arabic"
    [bg]="Bulgarian"
    [ca]="Catalan"
    [cs]="Czech"
    [da]="Danish"
    [de]="German"
    [el]="Greek"
    [en]="English"
    [enm]="Middle English"
    [es]="Spanish"
    [fi]="Finnish"
    [fr]="French"
    [grc]="Ancient Greek"
    [he]="Hebrew"
    [hi]="Hindi"
    [hu]="Hungarian"
    [id]="Indonesian"
    [it]="Italian"
    [ja]="Japanese"
    [ko]="Korean"
    [la]="Latin"
    [nl]="Dutch"
    [no]="Norwegian"
    [pl]="Polish"
    [pt]="Portuguese"
    [ro]="Romanian"
    [ru]="Russian"
    [sv]="Swedish"
    [tr]="Turkish"
    [uk]="Ukrainian"
    [vi]="Vietnamese"
    [zh]="Chinese"
)

if [[ $# -lt 1 ]]; then
    echo "Usage: $0 <iso-code> [out-dir]" >&2
    echo "Example: $0 en ./data/corpus" >&2
    exit 1
fi

LANG_CODE="$1"
OUT_DIR="${2:-./data/corpus}"

if [[ -z "${KAIKKI_NAMES[$LANG_CODE]+_}" ]]; then
    echo "Unknown ISO code '$LANG_CODE'." >&2
    echo "Add it to the KAIKKI_NAMES map in scripts/download-kaikki.sh" >&2
    echo "(value = the per-language URL segment on kaikki.org)." >&2
    exit 1
fi

LANG_NAME="${KAIKKI_NAMES[$LANG_CODE]}"

mkdir -p "$OUT_DIR"

# Kaikki's URL uses the language name (URL-encoded if it contains
# spaces, e.g. "Old English" → "Old%20English"). Our local filename
# uses the ISO code directly so build scripts can compute the path
# deterministically from the same code.
LANG_URL_SEGMENT="${LANG_NAME// /%20}"
OUT_FILE="$OUT_DIR/wiktionary-${LANG_CODE}.jsonl.gz"
URL="https://kaikki.org/dictionary/${LANG_URL_SEGMENT}/kaikki.org-dictionary-${LANG_URL_SEGMENT}.jsonl.gz"

echo "Downloading $URL"
echo "    → $OUT_FILE"

curl -L --fail --progress-bar -o "$OUT_FILE" "$URL"

SIZE=$(du -h "$OUT_FILE" | cut -f1)
echo "Done. $OUT_FILE ($SIZE)"
echo ""
echo "Next step:"
echo "  dotnet run --project apps/api/src/Langoose.Corpus.DbTool -- \\"
echo "    import-wiktionary --lang $LANG_CODE --source $OUT_FILE"
