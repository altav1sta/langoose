#!/usr/bin/env bash
# Sync shared rules between CLAUDE.md and AGENTS.md.
# Shared rules include the Guidance Index in both files.
# AGENTS.md also keeps a Codex-only Skill Index appendix.
#
# Usage:
#   scripts/sync-rules.sh to-codex           # CLAUDE.md -> AGENTS.md
#   scripts/sync-rules.sh to-claude          # AGENTS.md -> CLAUDE.md
#   scripts/sync-rules.sh reconcile          # show diff, require explicit preference
#   scripts/sync-rules.sh reconcile claude   # prefer CLAUDE.md
#   scripts/sync-rules.sh reconcile codex    # prefer AGENTS.md
#   scripts/sync-rules.sh status             # report whether shared rules match
#   scripts/sync-rules.sh audit              # validate skill/guidance links on disk

set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

AGENTS_SKILL_MARKER="## Skill Mapping"
GUIDANCE_MARKER="## Guidance Index"
CLAUDE_SPECIFIC_MARKER="## Claude-Specific Notes"

trim_trailing_blank_lines() {
  sed -e :a -e '/^[[:space:]]*$/{$d;N;ba;}'
}

get_agents_shared() {
  sed "/^${AGENTS_SKILL_MARKER}/,\$d" AGENTS.md | trim_trailing_blank_lines
}

get_agents_skill_index() {
  sed -n "/^${AGENTS_SKILL_MARKER}/,\$p" AGENTS.md | trim_trailing_blank_lines
}

get_claude_shared() {
  if grep -q "^${CLAUDE_SPECIFIC_MARKER}$" CLAUDE.md; then
    sed "/^${CLAUDE_SPECIFIC_MARKER}/,\$d" CLAUDE.md | trim_trailing_blank_lines
  else
    trim_trailing_blank_lines < CLAUDE.md
  fi
}

write_agents_from_claude() {
  local skill_index=""

  if [ ! -f CLAUDE.md ]; then
    echo "CLAUDE.md not found"
    exit 1
  fi

  if [ -f AGENTS.md ]; then
    skill_index=$(get_agents_skill_index)
  fi

  local tmp_file
  tmp_file=$(mktemp)

  if [ -z "${skill_index}" ]; then
    echo "Warning: no Skill Index found in AGENTS.md, copying without it"
    cat CLAUDE.md > "${tmp_file}"
  else
    {
      get_claude_shared
      printf '\n\n%s\n' "${skill_index}"
    } > "${tmp_file}"
  fi

  mv "${tmp_file}" AGENTS.md
  echo "Synced CLAUDE.md -> AGENTS.md"
  audit
}

get_claude_specific() {
  if grep -q "^${CLAUDE_SPECIFIC_MARKER}$" CLAUDE.md; then
    sed -n "/^${CLAUDE_SPECIFIC_MARKER}/,\$p" CLAUDE.md | trim_trailing_blank_lines
  fi
}

write_claude_from_agents() {
  if [ ! -f AGENTS.md ]; then
    echo "AGENTS.md not found"
    exit 1
  fi

  local claude_specific=""
  if [ -f CLAUDE.md ]; then
    claude_specific=$(get_claude_specific)
  fi

  local tmp_file
  tmp_file=$(mktemp)

  if [ -z "${claude_specific}" ]; then
    get_agents_shared > "${tmp_file}"
  else
    {
      get_agents_shared
      printf '\n\n%s\n' "${claude_specific}"
    } > "${tmp_file}"
  fi

  mv "${tmp_file}" CLAUDE.md
  echo "Synced AGENTS.md -> CLAUDE.md"
  audit
}

report_status() {
  if [ ! -f AGENTS.md ]; then
    echo "AGENTS.md not found"
    exit 1
  fi

  if [ ! -f CLAUDE.md ]; then
    echo "CLAUDE.md not found"
    exit 1
  fi

  if diff -u <(get_agents_shared) <(get_claude_shared) > /dev/null; then
    echo "Shared rules are in sync"
  else
    echo "Shared rules differ"
    diff -u <(get_agents_shared) <(get_claude_shared) || true
    exit 2
  fi
}

audit_index_links() {
  local file_path="$1"
  local marker="$2"
  local label="$3"

  if [ ! -f "${file_path}" ]; then
    echo "${file_path} not found"
    exit 1
  fi

  if ! grep -q "^${marker}$" "${file_path}"; then
    echo "${label}: marker '${marker}' not found in ${file_path}"
    exit 2
  fi

  local links
  local section
  section=$(sed -n "/^${marker}/,\$p" "${file_path}")
  links=$(printf '%s\n' "${section}" | grep -oE '\]\(([^)]+)\)' | sed -E 's/^\]\((.*)\)$/\1/' || true)
  local backtick_links
  backtick_links=$(printf '%s\n' "${section}" | grep -oE '`[^`]+\.(md|yml|yaml|json|sh|ps1)`' | tr -d '`' || true)
  if [ -n "${backtick_links}" ]; then
    links=$(printf '%s\n%s' "${links}" "${backtick_links}")
  fi

  if [ -z "${links}" ]; then
    echo "${label}: no links found under ${marker}"
    exit 2
  fi

  local missing=0
  while IFS= read -r link; do
    [ -z "${link}" ] && continue

    if [[ "${link}" == http://* ]] || [[ "${link}" == https://* ]] || [[ "${link}" == \#* ]]; then
      continue
    fi

    if [ ! -e "${link}" ]; then
      echo "${label}: missing ${link}"
      missing=1
    fi
  done <<< "${links}"

  if [ "${missing}" -ne 0 ]; then
    exit 2
  fi

  echo "${label}: all indexed links exist"
}

audit() {
  audit_index_links AGENTS.md "${GUIDANCE_MARKER}" "AGENTS Guidance Index"
  audit_index_links AGENTS.md "${AGENTS_SKILL_MARKER}" "AGENTS Skill Mapping"
  audit_index_links AGENTS.md "## Skill Index" "AGENTS Skill Index"
  audit_index_links CLAUDE.md "${GUIDANCE_MARKER}" "CLAUDE Guidance Index"
  scripts/validate-skill-doc-map.sh
}

reconcile() {
  local preferred="${1:-}"

  if [ ! -f AGENTS.md ] && [ ! -f CLAUDE.md ]; then
    echo "Neither AGENTS.md nor CLAUDE.md exists"
    exit 1
  fi

  if [ ! -f AGENTS.md ]; then
    echo "AGENTS.md missing; syncing from CLAUDE.md"
    write_agents_from_claude
    return
  fi

  if [ ! -f CLAUDE.md ]; then
    echo "CLAUDE.md missing; syncing from AGENTS.md"
    write_claude_from_agents
    return
  fi

  if diff -u <(get_agents_shared) <(get_claude_shared) > /dev/null; then
    echo "Shared rules already in sync"
    return
  fi

  case "${preferred}" in
    claude)
      echo "Reconciling in favor of CLAUDE.md"
      write_agents_from_claude
      ;;
    codex|agents)
      echo "Reconciling in favor of AGENTS.md"
      write_claude_from_agents
      ;;
    "")
      echo "Shared rules differ; specify a preference to resolve:"
      echo "  scripts/sync-rules.sh reconcile claude"
      echo "  scripts/sync-rules.sh reconcile codex"
      echo ""
      diff -u <(get_agents_shared) <(get_claude_shared) || true
      exit 2
      ;;
    *)
      echo "Unknown reconcile preference: ${preferred}"
      echo "Usage: scripts/sync-rules.sh reconcile [claude|codex]"
      exit 1
      ;;
  esac
}

case "${1:-}" in
  to-codex)
    write_agents_from_claude
    ;;
  to-claude)
    write_claude_from_agents
    ;;
  reconcile)
    reconcile "${2:-}"
    ;;
  status)
    report_status
    ;;
  audit)
    audit
    ;;
  *)
    echo "Usage: scripts/sync-rules.sh [to-codex|to-claude|reconcile|status|audit]"
    exit 1
    ;;
esac
