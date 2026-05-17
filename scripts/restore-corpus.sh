#!/usr/bin/env bash
# Restore a corpus dump from the object store into the local corpus
# database. Designed to run on the host that owns the corpus DB
# (production: the server that runs the corpus Postgres container;
# local dev: your machine running the compose stack). pg_restore runs
# inside the corpus DB's own container so the connection stays on the
# loopback Unix socket — no network exposure required.
#
# Usage:
#   scripts/restore-corpus.sh <object-key>
#
# Example:
#   scripts/restore-corpus.sh dump/test-corpus.dump
#   scripts/restore-corpus.sh dump/corpus-full-20260415143022.dump
#
# Required env vars (source from repo-root .env or your shell):
#   AWS_ACCESS_KEY_ID
#   AWS_SECRET_ACCESS_KEY
#   AWS_ENDPOINT_URL_S3        full https endpoint of the bucket provider
#   CORPUS_BUCKET              bucket name
#   CORPUS_DB_CONTAINER        docker container name/id of the corpus Postgres
#   CORPUS_DB_NAME             database name inside that container
#   CORPUS_DB_USER             role used by pg_restore (typically the owner)
#
# The script:
#   1. Pulls the dump from the object store to a host temp dir.
#   2. Streams it into pg_restore via `docker exec -i` so the file never
#      has to be copied inside the container.
#   3. Cleans up the host temp dir on exit.
#
# pg_restore is invoked with --clean --if-exists, so the target database
# is wiped and rebuilt from the dump. There is no additive/delta mode —
# the restored state IS the dump.

set -euo pipefail

# Load env vars from local .env if present (gitignored).
ENV_FILE="${ENV_FILE:-.env}"
if [[ -f "$ENV_FILE" ]]; then
    set -a
    # shellcheck disable=SC1090
    source "$ENV_FILE"
    set +a
fi

OBJECT_KEY="${1:-}"

if [[ -z "$OBJECT_KEY" ]]; then
    echo "Usage: $0 <object-key>" >&2
    echo "Example: $0 dump/test-corpus.dump" >&2
    exit 1
fi

: "${AWS_ACCESS_KEY_ID:?AWS_ACCESS_KEY_ID env var required (set in $ENV_FILE or your shell)}"
: "${AWS_SECRET_ACCESS_KEY:?AWS_SECRET_ACCESS_KEY env var required}"
: "${AWS_ENDPOINT_URL_S3:?AWS_ENDPOINT_URL_S3 env var required}"
: "${CORPUS_BUCKET:?CORPUS_BUCKET env var required}"
: "${CORPUS_DB_CONTAINER:?CORPUS_DB_CONTAINER env var required}"
: "${CORPUS_DB_NAME:?CORPUS_DB_NAME env var required}"
: "${CORPUS_DB_USER:?CORPUS_DB_USER env var required}"

DUMP_FILENAME=$(basename "$OBJECT_KEY")
TMPDIR=$(mktemp -d)
trap 'rm -rf "$TMPDIR"' EXIT
DUMP_PATH="$TMPDIR/$DUMP_FILENAME"

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
TMPDIR_HOST=$(to_host_path "$TMPDIR")

echo "Pulling s3://$CORPUS_BUCKET/$OBJECT_KEY ..."
# MSYS_NO_PATHCONV=1: stops Git Bash from rewriting the in-container
# /out/... path to a host-style C:\... path before docker sees it.
MSYS_NO_PATHCONV=1 docker run --rm \
    -e AWS_ACCESS_KEY_ID \
    -e AWS_SECRET_ACCESS_KEY \
    -e AWS_ENDPOINT_URL_S3 \
    -v "$TMPDIR_HOST:/out" \
    amazon/aws-cli \
    s3 cp "s3://$CORPUS_BUCKET/$OBJECT_KEY" "/out/$DUMP_FILENAME"

SIZE=$(du -h "$DUMP_PATH" | cut -f1)
echo "Pulled $SIZE. Restoring into $CORPUS_DB_NAME via container $CORPUS_DB_CONTAINER ..."

# pg_restore --jobs N (parallel) requires seekable input, so we can't
# pipe via stdin. Copy the dump into the container's /tmp and pass the
# file path. Clean it up after restore regardless of exit status.
CONTAINER_DUMP_PATH="/tmp/$DUMP_FILENAME"
DUMP_PATH_HOST=$(to_host_path "$DUMP_PATH")
trap 'rm -rf "$TMPDIR"; docker exec "$CORPUS_DB_CONTAINER" rm -f "$CONTAINER_DUMP_PATH" 2>/dev/null || true' EXIT

MSYS_NO_PATHCONV=1 docker cp "$DUMP_PATH_HOST" "$CORPUS_DB_CONTAINER:$CONTAINER_DUMP_PATH"

MSYS_NO_PATHCONV=1 docker exec "$CORPUS_DB_CONTAINER" pg_restore \
    --clean --if-exists --no-owner --no-acl \
    --jobs 4 \
    --username "$CORPUS_DB_USER" \
    --dbname "$CORPUS_DB_NAME" \
    "$CONTAINER_DUMP_PATH"

echo "Done."
