---
name: langoose-dictionary-imports
description: Work on Langoose dictionary, CSV import/export, and duplicate-merging behavior. Use when changing dictionary visibility, base-vs-custom merge rules, CSV validation, import/export flow, quick-add behavior, or custom-data clearing in apps/api and apps/web.
---

# Langoose Dictionary Imports

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill when dictionary rules or CSV workflows are involved.

## Use When

- The task changes dictionary visibility or merge behavior.
- The task changes quick add, CSV import/export, or duplicate handling.
- The task changes related frontend contract or UI behavior.

## Primary Doc

- [dictionary-rules.md](../../../docs/agent/dictionary-rules.md)

## Related Docs

- [api-contracts.md](../../../docs/agent/api-contracts.md)

## Critical Reminders

- Preserve dictionary visibility and duplicate-collapsing invariants unless the task explicitly changes them.
- Inspect `apps/api/src/Langoose.Core/Services/DictionaryService.cs`.
- Inspect `apps/api/src/Langoose.Api/Controllers/DictionaryController.cs`.
- Inspect the corresponding frontend calls in `apps/web/src/api.ts` and UI behavior in `apps/web/src/App.tsx`.
- Inspect the relevant backend tests under `apps/api/tests` before changing behavior.
