#!/usr/bin/env bash
# Download a Kaikki Wiktionary JSONL extract for a single language.
#
# Usage:
#   scripts/download-kaikki.sh <language-name> [out-dir]
#
# Examples:
#   scripts/download-kaikki.sh English ./data/corpus
#   scripts/download-kaikki.sh Russian D:/langoose-data/sources
#
# Notes:
#   - <language-name> must match Kaikki's URL format (English, Russian, etc.)
#   - Output is the raw .jsonl.gz file. The DbTool reads it directly.
#   - Compressed sizes vary per language: English ~450 MB, Russian ~75 MB.
#     Uncompressed is roughly 3-4x larger. Make sure the destination has
#     enough space.

set -euo pipefail

if [[ $# -lt 1 ]]; then
    echo "Usage: $0 <language-name> [out-dir]" >&2
    echo "Example: $0 English ./data/corpus" >&2
    exit 1
fi

LANG_NAME="$1"
OUT_DIR="${2:-./data/corpus}"

mkdir -p "$OUT_DIR"

OUT_FILE="$OUT_DIR/wiktionary-$(echo "$LANG_NAME" | tr '[:upper:]' '[:lower:]').jsonl.gz"
URL="https://kaikki.org/dictionary/$LANG_NAME/kaikki.org-dictionary-$LANG_NAME.jsonl.gz"

echo "Downloading $URL"
echo "    → $OUT_FILE"

curl -L --fail --progress-bar -o "$OUT_FILE" "$URL"

SIZE=$(du -h "$OUT_FILE" | cut -f1)
echo "Done. $OUT_FILE ($SIZE)"
echo ""
echo "Next step:"
echo "  dotnet run --project apps/api/src/Langoose.Corpus.DbTool -- \\"
echo "    import-wiktionary --lang <code> --source $OUT_FILE"
