#!/usr/bin/env bash
# Publish a full corpus dump to the corpus object store (any
# S3-compatible bucket). Intended for production dumps — every published
# object is immutable so prod restores can roll back to specific builds.
#
# Object key: dump/corpus-full-<stamp>.dump (mirrors the local
# data/dump/corpus-full-<stamp>.dump path). The rebuild script defaults
# the stamp to a UTC date+time, so multiple builds per day naturally get
# distinct keys. The script still refuses to overwrite an existing object
# as a backstop against accidental clobbers — if you ever do need to
# republish a key, delete the existing object explicitly first.
#
# Usage:
#   scripts/publish-full-corpus-dump.sh <dump-file>
#
# Example:
#   scripts/publish-full-corpus-dump.sh data/dump/corpus-full-20260415143022.dump
#
# Required env vars (any S3-compatible provider). Source them from the
# repo-root .env file (auto-loaded if present) or your shell:
#   AWS_ACCESS_KEY_ID
#   AWS_SECRET_ACCESS_KEY
#   AWS_ENDPOINT_URL_S3       full https endpoint of the bucket provider
#   CORPUS_BUCKET             bucket name
#
# After publishing, restore by running scripts/restore-corpus.sh on the
# host that runs the corpus DB.

set -euo pipefail

# Load env vars from local .env if present (gitignored).
# set -a exports everything sourced so subsequent commands see it;
# set +a restores normal scoping. Same pattern in all task scripts.
ENV_FILE="${ENV_FILE:-.env}"
if [[ -f "$ENV_FILE" ]]; then
    set -a
    # shellcheck disable=SC1090
    source "$ENV_FILE"
    set +a
fi

DUMP_FILE="${1:-}"

if [[ -z "$DUMP_FILE" ]]; then
    echo "Usage: $0 <dump-file>" >&2
    exit 1
fi

if [[ ! -f "$DUMP_FILE" ]]; then
    echo "Dump file not found: $DUMP_FILE" >&2
    exit 1
fi

: "${AWS_ACCESS_KEY_ID:?AWS_ACCESS_KEY_ID env var required (set in $ENV_FILE or your shell)}"
: "${AWS_SECRET_ACCESS_KEY:?AWS_SECRET_ACCESS_KEY env var required}"
: "${AWS_ENDPOINT_URL_S3:?AWS_ENDPOINT_URL_S3 env var required}"
: "${CORPUS_BUCKET:?CORPUS_BUCKET env var required}"

DUMP_BASENAME=$(basename "$DUMP_FILE" .dump)

if [[ ! "$DUMP_BASENAME" =~ ^corpus-full- ]]; then
    echo "Expected filename corpus-full-<date>.dump, got: $DUMP_FILE" >&2
    echo "For test dumps use scripts/publish-test-corpus-dump.sh instead." >&2
    exit 1
fi

OBJECT_KEY="dump/${DUMP_BASENAME}.dump"
SIZE=$(du -h "$DUMP_FILE" | cut -f1)
DUMP_FILENAME=$(basename "$DUMP_FILE")
DUMP_ABS=$(cd "$(dirname "$DUMP_FILE")" && pwd)/$DUMP_FILENAME

# Translate posix paths to native Windows form when running under Git
# Bash, since Docker Desktop on Windows doesn't recognise MSYS /tmp/...
# style. Plain pass-through on Linux/macOS where cygpath isn't present.
to_host_path() {
    if command -v cygpath >/dev/null 2>&1; then
        cygpath -w "$1"
    else
        echo "$1"
    fi
}
DUMP_ABS_HOST=$(to_host_path "$DUMP_ABS")

# Run aws CLI via docker so the host doesn't need it installed.
# Bind-mount the dump at /dumps/<basename> inside the container so error
# messages reference the real filename rather than a synthetic path.
# MSYS_NO_PATHCONV=1 stops Git Bash from rewriting the in-container
# /dumps/... arg to a host-style C:\... path before docker sees it.
aws_cli() {
    MSYS_NO_PATHCONV=1 docker run --rm \
        -e AWS_ACCESS_KEY_ID \
        -e AWS_SECRET_ACCESS_KEY \
        -e AWS_ENDPOINT_URL_S3 \
        -v "$DUMP_ABS_HOST:/dumps/$DUMP_FILENAME:ro" \
        amazon/aws-cli "$@"
}

# Full dumps are append-only. Each dated key is a historical anchor and
# must not move silently.
if aws_cli s3api head-object \
    --bucket "$CORPUS_BUCKET" \
    --key "$OBJECT_KEY" \
    >/dev/null 2>&1; then
    echo "Object $OBJECT_KEY already exists." >&2
    echo "Published dumps are immutable. To publish a new build, rerun the" >&2
    echo "rebuild script (its default UTC timestamp suffix yields a fresh" >&2
    echo "filename automatically). To genuinely re-publish this exact key," >&2
    echo "delete the existing object explicitly first." >&2
    exit 1
fi

echo "Uploading $DUMP_FILE ($SIZE) → s3://$CORPUS_BUCKET/$OBJECT_KEY ..."

aws_cli s3 cp "/dumps/$DUMP_FILENAME" "s3://$CORPUS_BUCKET/$OBJECT_KEY"

echo ""
echo "Done. To restore to the production corpus database, run on the host that owns it:"
echo "  scripts/restore-corpus.sh $OBJECT_KEY"
