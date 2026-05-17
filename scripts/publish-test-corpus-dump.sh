#!/usr/bin/env bash
# Publish the test corpus dump (data/dump/test-corpus.dump, built by
# scripts/rebuild-test-corpus-dump.sh) to the corpus object store (any
# S3-compatible bucket). Rolling key — each publish overwrites the
# previous one.
#
# Object key: dump/test-corpus.dump.
#
# Usage:
#   scripts/publish-test-corpus-dump.sh
#
# Required env vars (source from repo-root .env or your shell):
#   AWS_ACCESS_KEY_ID
#   AWS_SECRET_ACCESS_KEY
#   AWS_ENDPOINT_URL_S3       full https endpoint of the bucket provider
#   CORPUS_BUCKET             bucket name
#
# After publishing, restore by running scripts/restore-corpus.sh on the
# host that runs the corpus DB.

set -euo pipefail

# Load env vars from local .env if present (gitignored).
ENV_FILE="${ENV_FILE:-.env}"
if [[ -f "$ENV_FILE" ]]; then
    set -a
    # shellcheck disable=SC1090
    source "$ENV_FILE"
    set +a
fi

DUMP_FILE="data/dump/test-corpus.dump"
OBJECT_KEY="dump/test-corpus.dump"

if [[ ! -f "$DUMP_FILE" ]]; then
    echo "Dump file not found: $DUMP_FILE" >&2
    echo "Build it first with scripts/rebuild-test-corpus-dump.sh." >&2
    exit 1
fi

: "${AWS_ACCESS_KEY_ID:?AWS_ACCESS_KEY_ID env var required (set in $ENV_FILE or your shell)}"
: "${AWS_SECRET_ACCESS_KEY:?AWS_SECRET_ACCESS_KEY env var required}"
: "${AWS_ENDPOINT_URL_S3:?AWS_ENDPOINT_URL_S3 env var required}"
: "${CORPUS_BUCKET:?CORPUS_BUCKET env var required}"

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

echo "Uploading $DUMP_FILE ($SIZE) → s3://$CORPUS_BUCKET/$OBJECT_KEY ..."

# MSYS_NO_PATHCONV=1: stops Git Bash from rewriting the in-container
# /dumps/... arg to a host-style C:\... path before docker sees it.
MSYS_NO_PATHCONV=1 docker run --rm \
    -e AWS_ACCESS_KEY_ID \
    -e AWS_SECRET_ACCESS_KEY \
    -e AWS_ENDPOINT_URL_S3 \
    -v "$DUMP_ABS_HOST:/dumps/$DUMP_FILENAME:ro" \
    amazon/aws-cli \
    s3 cp "/dumps/$DUMP_FILENAME" "s3://$CORPUS_BUCKET/$OBJECT_KEY"

echo ""
echo "Done. To restore to the staging corpus database, run on the host that owns it:"
echo "  scripts/restore-corpus.sh $OBJECT_KEY"
