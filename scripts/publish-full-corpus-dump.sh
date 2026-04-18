#!/usr/bin/env bash
# Publish a full corpus dump to GitHub Releases as a permanent, dated
# release. Intended for production dumps — one release per build date
# so prod restores can roll back.
#
# Tag scheme: corpus-full-<date>, mirrors the dump filename.
# Target: always `main` so the tag anchors to preserved history.
#
# Usage:
#   scripts/publish-full-corpus-dump.sh <dump-file>
#
# Example:
#   scripts/publish-full-corpus-dump.sh data/dump/corpus-full-2026-04-15.dump
#
# After publishing, restore via GitHub Actions → "Corpus Restore" workflow
# with release_tag=corpus-full-<date>.

set -euo pipefail

DUMP_FILE="${1:-}"

if [[ -z "$DUMP_FILE" ]]; then
    echo "Usage: $0 <dump-file>" >&2
    exit 1
fi

if [[ ! -f "$DUMP_FILE" ]]; then
    echo "Dump file not found: $DUMP_FILE" >&2
    exit 1
fi

DUMP_BASENAME=$(basename "$DUMP_FILE" .dump)

if [[ ! "$DUMP_BASENAME" =~ ^corpus-full- ]]; then
    echo "Expected filename corpus-full-<date>.dump, got: $DUMP_FILE" >&2
    echo "For test dumps use scripts/publish-test-corpus-dump.sh instead." >&2
    exit 1
fi

RELEASE_TAG="$DUMP_BASENAME"
ATTRIBUTION_FILE="ATTRIBUTION.md"

if [[ ! -f "$ATTRIBUTION_FILE" ]]; then
    echo "Expected $ATTRIBUTION_FILE at the repo root; aborting so we don't" >&2
    echo "publish a dump without its required CC-BY-SA attribution notice." >&2
    exit 1
fi

SIZE=$(du -h "$DUMP_FILE" | cut -f1)

echo "Publishing $DUMP_FILE ($SIZE) as release $RELEASE_TAG (targeting main)..."

# Single-line paragraphs — GitHub Releases renders a single newline as a
# hard line break, so hard-wrapped paragraphs look broken. Blank lines
# still separate paragraphs as expected.
RELEASE_NOTES=$(cat <<EOF
Built locally on $(date -u +%Y-%m-%dT%H:%M:%SZ).

**Data sources & licensing.** This dump contains data derived from [Wiktionary](https://www.wiktionary.org/) via [Kaikki.org](https://kaikki.org/), both distributed under [CC-BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/). The dump is therefore also available under CC-BY-SA 4.0. See the \`ATTRIBUTION.md\` asset attached to this release for the full notice and list of all sources evaluated for the Langoose corpus pipeline.
EOF
)

# Full dumps are append-only. Each dated tag is a historical anchor and
# must not move silently — if you need to republish the same date (e.g.
# the previous dump was broken), delete the existing release + tag by
# hand first, then rerun this script. Policy:
#
#   release exists         → refuse
#   tag exists, no release → attach new release to existing tag (tag
#                            stays where it was — we never move an
#                            already-published tag)
#   neither exists         → create both at current main

REPO=$(gh repo view --json nameWithOwner --jq .nameWithOwner)

if gh release view "$RELEASE_TAG" >/dev/null 2>&1; then
    echo "Release $RELEASE_TAG already exists." >&2
    echo "Full dumps are append-only. To republish this date:" >&2
    echo "  1. Pick a different DATE_STAMP (e.g. 2026-04-18-fix) on build, or" >&2
    echo "  2. Explicitly delete the existing release:" >&2
    echo "       gh release delete $RELEASE_TAG --cleanup-tag" >&2
    exit 1
fi

if gh api "repos/$REPO/git/refs/tags/$RELEASE_TAG" >/dev/null 2>&1; then
    echo "Tag $RELEASE_TAG already exists without a release — attaching new release to the existing tag. The tag will NOT move."
    gh release create "$RELEASE_TAG" "$DUMP_FILE" "$ATTRIBUTION_FILE" \
        --notes "$RELEASE_NOTES"
else
    gh release create "$RELEASE_TAG" "$DUMP_FILE" "$ATTRIBUTION_FILE" \
        --notes "$RELEASE_NOTES" \
        --target main
fi

echo ""
echo "Done. Restore to production:"
echo "  GitHub → Actions → Corpus Restore → Run workflow"
echo "    release_tag: $RELEASE_TAG"
