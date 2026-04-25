#!/usr/bin/env bash
# Download a wordfreq frequency-ranking TSV for a single language.
# Sibling of `download-kaikki.sh`; same role (fetch source data and
# put it on disk under data/corpus/) even though the underlying
# mechanism is a Python call rather than `curl`. Output is the input
# format expected by `import-wordfreq`:
#
#   <word>\t<rank>\t<zipf_score>
#
# rank starts at 1 (most frequent). zipf_score is wordfreq's Zipf
# scale (~0–8); see https://github.com/rspeer/wordfreq#appendix-zipf-values.
#
# Why a wrapper script: wordfreq is a Python package, not a
# downloadable file. By default this script runs it inside a
# python:3-slim Docker container (Docker is already a project
# dependency for Postgres / e2e), so you don't need a local Python
# install. If you'd rather use a local interpreter — faster after the
# first run, since it skips the per-invocation pip install — set
# PYTHON=python3 (or any interpreter that has `pip install wordfreq`)
# and the script uses that instead.
#
# The full per-language wordlist is dumped — no top-N knob here,
# because the actual filter knob is `--frequency-filter-top` on
# `import-wiktionary` (and `WHERE rank <= N` at query time for the
# future provider). wordfreq's full per-language list is ~150k–250k
# words / ~5–10 MB on disk — trivial next to the Kaikki extracts.
#
# Usage:
#   scripts/download-wordfreq.sh <iso-code> [out-path]
#
# Example:
#   scripts/download-wordfreq.sh en
#   scripts/download-wordfreq.sh en ./data/corpus/wordfreq-en.tsv
#
# Defaults:
#   out-path  ./data/corpus/wordfreq-<code>.tsv
#
# Env overrides:
#   PYTHON=python3     Use a local Python interpreter (must have
#                      wordfreq installed). Skips Docker entirely.
#   DOCKER_IMAGE=...   Override the Docker image (default: python:3-slim).

set -euo pipefail

if [[ $# -lt 1 ]]; then
    echo "Usage: $0 <iso-code> [out-path]" >&2
    echo "Example: $0 en ./data/corpus/wordfreq-en.tsv" >&2
    exit 1
fi

LANG_CODE="$1"
OUT_PATH="${2:-./data/corpus/wordfreq-${LANG_CODE}.tsv}"
PYTHON="${PYTHON:-}"
DOCKER_IMAGE="${DOCKER_IMAGE:-python:3-slim}"

mkdir -p "$(dirname "$OUT_PATH")"
OUT_DIR="$(cd "$(dirname "$OUT_PATH")" && pwd)"
OUT_BASE="$(basename "$OUT_PATH")"

# Two transports for the Python below: a local interpreter, or a
# python:3-slim container with wordfreq pip-installed inline. Both
# read the program from stdin (`python -`), so a single heredoc at
# the bottom of this script feeds whichever transport is selected.
run_with_local_python() {
    if ! command -v "$PYTHON" >/dev/null 2>&1; then
        echo "PYTHON='$PYTHON' is not on PATH." >&2
        exit 1
    fi
    if ! "$PYTHON" -c 'import wordfreq' >/dev/null 2>&1; then
        echo "wordfreq is not installed for '$PYTHON'." >&2
        echo "Install it with: $PYTHON -m pip install wordfreq" >&2
        echo "Or unset PYTHON to use the Docker fallback." >&2
        exit 1
    fi
    echo "Dumping wordfreq for '$LANG_CODE' via local $PYTHON → $OUT_PATH"
    "$PYTHON" - "$LANG_CODE" "$OUT_PATH"
}

run_with_docker() {
    if ! command -v docker >/dev/null 2>&1; then
        echo "Docker not found and PYTHON not set." >&2
        echo "Either install Docker Desktop, or set PYTHON to an interpreter" >&2
        echo "that has wordfreq installed (\`pip install wordfreq\`)." >&2
        exit 1
    fi
    echo "Dumping wordfreq for '$LANG_CODE' via Docker $DOCKER_IMAGE → $OUT_PATH"
    echo "(first run pulls $DOCKER_IMAGE; each run pip-installs wordfreq inside the container, ~10s)"
    # `bash -c` reads the Python program from stdin (this script's
    # trailing heredoc) and pipes it to `python -`. pip's stdin is
    # redirected to /dev/null so it can't accidentally consume the
    # heredoc before python sees it.
    #
    # MSYS_NO_PATHCONV=1 is needed under Git Bash on Windows so MSYS
    # doesn't rewrite the in-container path `/out` to a Windows path
    # before docker sees it. Harmless no-op on Linux/macOS.
    MSYS_NO_PATHCONV=1 docker run --rm -i \
        -v "$OUT_DIR:/out" \
        "$DOCKER_IMAGE" \
        bash -c "pip install --quiet --no-cache-dir --root-user-action=ignore wordfreq </dev/null && python - '$LANG_CODE' '/out/$OUT_BASE'"
}

if [[ -n "$PYTHON" ]]; then
    runner=run_with_local_python
else
    runner=run_with_docker
fi

"$runner" <<'PY'
import sys
from wordfreq import iter_wordlist, zipf_frequency

lang, out_path = sys.argv[1], sys.argv[2]
count = 0
with open(out_path, "w", encoding="utf-8") as f:
    for rank, word in enumerate(iter_wordlist(lang), start=1):
        zipf = zipf_frequency(word, lang)
        f.write(f"{word}\t{rank}\t{zipf:.2f}\n")
        count += 1
print(f"Wrote {count} rows to {out_path}.")
PY

SIZE=$(du -h "$OUT_PATH" | cut -f1)
echo "Done. $OUT_PATH ($SIZE)"
echo ""
echo "Next step:"
echo "  dotnet run --project apps/api/src/Langoose.Corpus.DbTool -- \\"
echo "    import-wordfreq --lang $LANG_CODE --source $OUT_PATH"
