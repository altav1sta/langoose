#!/usr/bin/env bash
# Publish the test corpus dump (data/dump/test-corpus.dump, built by
# scripts/build-test-corpus-dump.sh) to GitHub Releases under a rolling
# tag. Each publish replaces the previous one so git history doesn't
# accumulate a tag per staging rebuild.
#
# Tag scheme: test-corpus (single, rolling — no date, no -latest suffix
# because there's only ever one).
# Target: always `main`. The existing release + tag are deleted and
# recreated on every publish so the tag actually moves to current main
# — `gh release edit --target` is a no-op on already-published tags.
#
# Usage:
#   scripts/publish-test-corpus-dump.sh
#
# After publishing, restore via GitHub Actions → "Corpus Restore" workflow
# with release_tag=test-corpus.

set -euo pipefail

DUMP_FILE="data/dump/test-corpus.dump"
RELEASE_TAG="test-corpus"
ATTRIBUTION_FILE="ATTRIBUTION.md"

if [[ ! -f "$DUMP_FILE" ]]; then
    echo "Dump file not found: $DUMP_FILE" >&2
    echo "Build it first with scripts/build-test-corpus-dump.sh." >&2
    exit 1
fi

if [[ ! -f "$ATTRIBUTION_FILE" ]]; then
    echo "Expected $ATTRIBUTION_FILE at the repo root; aborting so we don't" >&2
    echo "publish a dump without its required CC-BY-SA attribution notice." >&2
    exit 1
fi

SIZE=$(du -h "$DUMP_FILE" | cut -f1)

echo "Publishing $DUMP_FILE ($SIZE) as rolling release $RELEASE_TAG (targeting main)..."

# Concise body — test releases are overwritten constantly, so no point in
# a long notice block. Single logical line per paragraph; GitHub Releases
# turns bare newlines into hard breaks.
RELEASE_NOTES=$(cat <<EOF
Rolling staging dump, rebuilt and overwritten on every publish. Built on $(date -u +%Y-%m-%dT%H:%M:%SZ). Use \`corpus-full-*\` releases for production.

Derived from [Wiktionary](https://www.wiktionary.org/) via [Kaikki.org](https://kaikki.org/), subset of ~2000 entries/language. Available under [CC-BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/); see the \`ATTRIBUTION.md\` asset for the full notice.
EOF
)

# Delete + recreate so the tag actually moves forward. `gh release edit
# --target` is ignored for already-published tags, so moving the tag
# needs a full drop-and-recreate.
if gh release view "$RELEASE_TAG" >/dev/null 2>&1; then
    echo "Deleting existing $RELEASE_TAG release + tag so it can be recreated at current main..."
    gh release delete "$RELEASE_TAG" --cleanup-tag --yes
fi

gh release create "$RELEASE_TAG" "$DUMP_FILE" "$ATTRIBUTION_FILE" \
    --notes "$RELEASE_NOTES" \
    --target main

echo ""
echo "Done. Restore to staging:"
echo "  GitHub → Actions → Corpus Restore → Run workflow"
echo "    release_tag: $RELEASE_TAG"
