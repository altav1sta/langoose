---
name: langoose-dictionary-imports
description: Work on Langoose dictionary, CSV import/export, and duplicate-merging behavior. Use when changing dictionary visibility, base-vs-custom merge rules, CSV validation, import/export flow, quick-add behavior, or custom-data clearing in apps/api and apps/web.
---

# Langoose Dictionary Imports

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill when dictionary rules or CSV workflows are involved.

## Main Rule

- Preserve dictionary visibility and duplicate-collapsing invariants unless the task explicitly changes them.

## Workflow

- Inspect `apps/api/src/Langoose.Api/Services/DictionaryService.cs`.
- Inspect `apps/api/src/Langoose.Api/Controllers/DictionaryController.cs`.
- Inspect the corresponding frontend calls in `apps/web/src/api.ts` and UI behavior in `apps/web/src/App.tsx`.
- Inspect the relevant backend tests under `apps/api/tests` before changing behavior.

## Load Additional Detail Only When Needed

- For concrete repo rules and review points, read [dictionary-rules.md](../../../docs/agent/dictionary-rules.md).
