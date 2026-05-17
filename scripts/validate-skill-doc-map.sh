#!/usr/bin/env bash
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

agents_path="AGENTS.md"
skills_root=".codex/skills"
docs_root="docs/agent"

fail() {
  echo "$1" >&2
  exit 2
}

# Resolve a path that's relative to another file. Returns the absolute
# path of (dirname(base_file) + relative_path), with . and .. segments
# collapsed via `cd && pwd`. Caller validates existence downstream.
resolve_from_file() {
  local base_file="$1"
  local relative_path="$2"
  local combined combined_dir combined_base
  combined="$(dirname "$base_file")/$relative_path"
  combined_dir=$(dirname "$combined")
  combined_base=$(basename "$combined")
  if [[ -d "$combined_dir" ]]; then
    echo "$(cd "$combined_dir" && pwd)/$combined_base"
  else
    # Directory part doesn't exist; return as-is and let the downstream
    # `-f` check produce a clean "doc does not exist" error.
    echo "$combined"
  fi
}

# Convert an absolute path to a repo-root-relative one. The script cd's
# to the repo root at the top, so $PWD is the repo root here.
to_repo_relative() {
  local absolute_path="$1"
  local repo_root
  repo_root=$(pwd)
  if [[ "$absolute_path" == "$repo_root/"* ]]; then
    echo "${absolute_path#"$repo_root"/}"
  elif [[ "$absolute_path" == "$repo_root" ]]; then
    echo "."
  else
    # Path is outside the repo — shouldn't happen for our inputs.
    echo "$absolute_path"
  fi
}

mapfile -t doc_files < <(find "${docs_root}" -maxdepth 1 -type f -name '*.md' -printf '%P\n' | sort)

ownership_section=$(awk '
  /^## Skill Mapping$/ { in_section=1; next }
  /^## / && in_section { exit }
  in_section { print }
' "${agents_path}")

[ -n "${ownership_section}" ] || fail "AGENTS.md is missing the '## Skill Mapping' section."

declare -A owner_by_doc
while IFS= read -r line; do
  [[ "${line}" == "| Doc | Owner | Notes |" ]] && continue
  [[ "${line}" =~ ^\|[-\ \|]+\|$ ]] && continue
  [[ "${line}" != \|* ]] && continue

  trimmed="${line#|}"
  trimmed="${trimmed%|}"
  IFS='|' read -r raw_doc raw_owner raw_notes <<< "${trimmed}"
  doc=$(echo "${raw_doc}" | xargs)
  owner=$(echo "${raw_owner}" | xargs)
  notes=$(echo "${raw_notes}" | xargs)
  doc="${doc//\`/}"
  owner="${owner//\`/}"

  [[ -n "${doc}" ]] || continue
  [[ -z "${owner_by_doc[$doc]:-}" ]] || fail "Duplicate doc in AGENTS.md ownership table: ${doc}"
  owner_by_doc["${doc}"]="${owner}|${notes}"
done <<< "${ownership_section}"

for doc_file in "${doc_files[@]}"; do
  doc="docs/agent/${doc_file}"
  [[ -n "${owner_by_doc[$doc]:-}" ]] || fail "Doc missing from AGENTS.md ownership table: ${doc}"
done

declare -A primary_owner_by_doc
while IFS= read -r skill_file; do
  skill_dir=$(basename "$(dirname "${skill_file}")")
  primary_doc=$(sed -n '/^## Primary Doc$/,/^## /{ /\](/{s/.*](\([^)]*\)).*/\1/p;}; }' "${skill_file}" | head -1)

  [[ -n "${primary_doc}" ]] || fail "${skill_file} is missing a primary doc."
  resolved_primary_doc=$(resolve_from_file "${skill_file}" "${primary_doc}")
  [[ -f "${resolved_primary_doc}" ]] || fail "${skill_dir} primary doc does not exist: ${primary_doc}"
  repo_relative_primary_doc=$(to_repo_relative "${resolved_primary_doc}")
  if [[ -n "${primary_owner_by_doc[$repo_relative_primary_doc]:-}" ]]; then
    existing_owner="${primary_owner_by_doc[$repo_relative_primary_doc]}"
    if [[ "${existing_owner}" != "langoose-dev" && "${skill_dir}" != "langoose-dev" ]]; then
      fail "Primary doc '${repo_relative_primary_doc}' is owned by multiple skills: ${existing_owner}, ${skill_dir}"
    fi
  fi

  if [[ "${skill_dir}" != "langoose-dev" ]]; then
    primary_owner_by_doc["${repo_relative_primary_doc}"]="${skill_dir}"
  fi

  if [[ "${skill_dir}" != "langoose-dev" ]]; then
    entry="${owner_by_doc[$repo_relative_primary_doc]:-}"
    [[ -n "${entry}" ]] || fail "${skill_dir} primary doc is not listed in AGENTS.md ownership table: ${repo_relative_primary_doc}"
    expected_owner="${entry%%|*}"
    [[ "${expected_owner}" == "${skill_dir}" ]] || fail "${skill_dir} primary doc mismatch. AGENTS.md says '${expected_owner}' owns ${repo_relative_primary_doc}."
  fi
done < <(find "${skills_root}" -mindepth 2 -maxdepth 2 -name 'SKILL.md' | sort)

for doc in "${!owner_by_doc[@]}"; do
  owner_and_notes="${owner_by_doc[$doc]}"
  owner="${owner_and_notes%%|*}"
  if [[ "${owner}" == "doc-only" ]]; then
    continue
  fi

  [[ -n "${primary_owner_by_doc[$doc]:-}" ]] || fail "Owned doc has no matching skill primary doc: ${doc} -> ${owner}"
done

mapfile -t reference_files < <(find "${skills_root}" -type f -path '*/references/*')
if [[ "${#reference_files[@]}" -gt 0 ]]; then
  fail "Reference files are no longer expected. Remove: ${reference_files[*]}"
fi

echo "Skill/doc ownership map is valid."
